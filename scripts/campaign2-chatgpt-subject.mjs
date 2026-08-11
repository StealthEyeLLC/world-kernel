import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, requestPath, outputPath, pipe = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkPath || !requestPath || !outputPath) {
  throw new Error('usage: campaign2-chatgpt-subject.mjs <eyebrowse-sdk> <request-json> <output-json> [pipe]');
}

const sha256 = value => crypto.createHash('sha256').update(value).digest('hex');
const canonical = value => {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`;
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(',')}}`;
  }
  return JSON.stringify(value);
};
const hashJson = value => sha256(Buffer.from(canonical(value), 'utf8'));
const isSha256 = value => typeof value === 'string' && /^[0-9a-f]{64}$/.test(value);
const readUtf8 = file => fs.readFileSync(path.resolve(file), 'utf8').replace(/\r\n/g, '\n');
const atomicWrite = (file, value) => {
  const full = path.resolve(file);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  const temp = `${full}.${process.pid}.tmp`;
  fs.writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, { encoding: 'utf8', flag: 'wx' });
  fs.renameSync(temp, full);
};

const requestBytes = fs.readFileSync(path.resolve(requestPath));
const request = JSON.parse(requestBytes.toString('utf8'));
if (request.schema !== 'world-kernel-build001-campaign2-subject-request-v1') throw new Error('invalid request schema');
if (!['inspect', 'invoke'].includes(request.mode)) throw new Error('invalid request mode');
for (const key of ['base_prompt_sha256', 'tool_contract_sha256', 'arm_package_sha256', 'trial_output_contract_sha256', 'observable_configuration_fingerprint_sha256']) {
  if (!isSha256(request[key])) throw new Error(`request ${key} is not SHA-256`);
}
if (sha256(Buffer.from(readUtf8(request.base_prompt_path), 'utf8')) !== request.base_prompt_sha256) throw new Error('base prompt hash mismatch');
if (sha256(Buffer.from(readUtf8(request.tool_contract_path), 'utf8')) !== request.tool_contract_sha256) throw new Error('tool contract hash mismatch');
if (sha256(Buffer.from(readUtf8(request.arm_package_path), 'utf8')) !== request.arm_package_sha256) throw new Error('arm package hash mismatch');
if (request.expected_model !== '5.6 Sol' || request.expected_reasoning !== 'Extra High') throw new Error('unlocked product selection');
if (request.arm === 'structured' && Number(request.extra_treatment_model_calls ?? 0) !== 0) throw new Error('Structured extra model call prohibited');

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const eye = await sdk.EyeBrowse.connect(pipe);
const startedAt = new Date().toISOString();
let target;
let rawTargetId;

const targetIdentity = opened => {
  const value = opened.target ?? opened;
  return typeof value === 'string' ? value : value?.id ?? value?.Id ?? value?.targetId ?? value?.TargetId ?? value?.target_id;
};

async function uiState() {
  return eye.jsValue(target, `(() => {
    const compact = element => ({
      role: element.getAttribute('role') || element.tagName.toLowerCase(),
      name: (element.getAttribute('aria-label') || element.getAttribute('title') || element.innerText || element.textContent || '').trim().replace(/\\s+/g, ' ').slice(0, 180),
      testid: element.getAttribute('data-testid') || ''
    });
    const controls = [...document.querySelectorAll('button,[role="button"],[role="menuitem"],[role="option"],a')]
      .filter(element => {
        const box = element.getBoundingClientRect();
        return box.width > 0 && box.height > 0;
      })
      .map(compact)
      .slice(0, 160);
    const messages = [...document.querySelectorAll('[data-message-author-role]')];
    const attachments = document.querySelectorAll('[data-testid*="attachment"], [aria-label*="attachment" i]').length;
    return {
      href: location.href,
      origin: location.origin,
      title: document.title,
      ready_state: document.readyState,
      message_count: messages.length,
      user_message_count: messages.filter(item => item.getAttribute('data-message-author-role') === 'user').length,
      assistant_message_count: messages.filter(item => item.getAttribute('data-message-author-role') === 'assistant').length,
      attachment_marker_count: attachments,
      login_control_present: controls.some(item => /^log in$/i.test(item.name)),
      temporary_markers: controls.filter(item => /temporary chat/i.test(item.name)).map(item => item.name),
      model_markers: controls.filter(item => /5\\.6 sol|model selector/i.test(item.name)).map(item => item.name),
      reasoning_markers: controls.filter(item => /extra high|reasoning/i.test(item.name)).map(item => item.name),
      project_markers: controls.filter(item => /project/i.test(item.name)).map(item => item.name),
      controls
    };
  })()`);
}

function validateCleanState(state) {
  const reasons = [];
  if (state.origin !== 'https://chatgpt.com') reasons.push('wrong_product_origin');
  if (state.login_control_present) reasons.push('chatgpt_not_signed_in');
  if (state.message_count !== 0) reasons.push('prior_transcript_present');
  if (state.attachment_marker_count !== 0) reasons.push('prior_attachment_marker_present');
  if (!state.temporary_markers.some(value => /temporary chat/i.test(value)) && !/[?&]temporary-chat=true(?:&|$)/.test(state.href)) {
    reasons.push('temporary_chat_not_observable');
  }
  if (!state.model_markers.some(value => /5\.6 sol/i.test(value))) reasons.push('wrong_or_unobservable_model_selection');
  if (!state.reasoning_markers.some(value => /extra high/i.test(value))) reasons.push('wrong_or_unobservable_reasoning_selection');
  if (state.project_markers.length > 0) reasons.push('project_context_marker_present');
  return reasons;
}

function stripJsonFence(text) {
  const trimmed = String(text ?? '').trim();
  const fenced = trimmed.match(/^```(?:json)?\s*([\s\S]*?)\s*```$/i);
  return fenced ? fenced[1].trim() : trimmed;
}

function validateSubjectOutput(value) {
  const required = ['action_class', 'target', 'parameters', 'prediction', 'requested_observations', 'material_action'];
  const keys = Object.keys(value ?? {}).sort();
  if (JSON.stringify(keys) !== JSON.stringify([...required].sort())) throw new Error('subject output keys differ from locked contract');
  if (value.action_class !== request.semantic_action) throw new Error('subject changed the locked semantic action');
  if (value.target !== request.target) throw new Error('subject changed the locked target');
  if (!value.parameters || typeof value.parameters !== 'object' || Array.isArray(value.parameters)) throw new Error('invalid parameters object');
  if (!Array.isArray(value.requested_observations)) throw new Error('invalid requested_observations');
  if (typeof value.material_action !== 'string') throw new Error('invalid material_action');
  const expected = [...request.propositions].sort();
  const supplied = Object.keys(value.prediction ?? {}).sort();
  if (JSON.stringify(expected) !== JSON.stringify(supplied)) throw new Error('prediction proposition set differs from locked vector');
  for (const proposition of expected) {
    const probability = value.prediction[proposition];
    if (typeof probability !== 'number' || !Number.isFinite(probability) || probability < 0 || probability > 1) {
      throw new Error(`invalid probability for ${proposition}`);
    }
  }
}

try {
  const status = await eye.status();
  const opened = await eye.open('https://chatgpt.com/?temporary-chat=true');
  target = targetIdentity(opened);
  if (!target) throw new Error(`target.open returned no target: ${JSON.stringify(opened)}`);
  await eye.wait(target, "location.hostname === 'chatgpt.com' && document.readyState === 'complete'", 30000, 100);
  await new Promise(resolve => setTimeout(resolve, 1500));
  const observation = await eye.observe(target);
  rawTargetId = observation.TargetId ?? observation.targetId ?? observation.target_id;
  const before = await uiState();
  const invalidationReasons = validateCleanState(before);
  const uiEvidence = {
    target,
    raw_target_id: rawTargetId,
    document: observation.Document ?? observation.document,
    browser_status: status,
    before: {
      href: before.href,
      title: before.title,
      ready_state: before.ready_state,
      message_count: before.message_count,
      user_message_count: before.user_message_count,
      assistant_message_count: before.assistant_message_count,
      attachment_marker_count: before.attachment_marker_count,
      login_control_present: before.login_control_present,
      temporary_markers: before.temporary_markers,
      model_markers: before.model_markers,
      reasoning_markers: before.reasoning_markers,
      project_markers: before.project_markers
    }
  };

  if (request.mode === 'inspect' || invalidationReasons.length > 0) {
    const result = {
      schema: 'world-kernel-build001-campaign2-subject-adapter-result-v1',
      passed: invalidationReasons.length === 0,
      mode: request.mode,
      trial_id: request.trial_id,
      started_at: startedAt,
      completed_at: new Date().toISOString(),
      invalidation_reasons: invalidationReasons,
      request_sha256: sha256(requestBytes),
      ui_evidence: uiEvidence,
      ui_evidence_sha256: hashJson(uiEvidence),
      operation_count: eye.operationCount
    };
    atomicWrite(outputPath, result);
    process.stdout.write(`${JSON.stringify(result)}\n`);
    process.exitCode = result.passed ? 0 : 4;
  } else {
    const basePrompt = readUtf8(request.base_prompt_path).trim();
    const toolContract = readUtf8(request.tool_contract_path).trim();
    const armPackage = readUtf8(request.arm_package_path).trim();
    const prompt = `${basePrompt}\n\nCOMMON TOOL CONTRACT\n${toolContract}\n\nARM PACKAGE (${request.arm})\n${armPackage}\n\nCURRENT PROVIDER OBSERVATIONS\n${request.current_observations}\n\nLOCKED TASK\nSemantic action: ${request.semantic_action}\nTarget: ${request.target}\nTask: ${request.task}\nPropositions: ${JSON.stringify(request.propositions)}\n\nReturn exactly the locked JSON object now.`;
    const textboxes = await eye.query({ target, role: 'textbox', limit: 20 });
    const items = textboxes.items ?? textboxes.results ?? textboxes.elements ?? [];
    const textbox = items.find(item => /chat|message|prompt/i.test(String(item.name ?? item.Name ?? item.label ?? ''))) ?? items[0];
    const textboxId = textbox?.id ?? textbox?.Id ?? textbox?.elementId ?? textbox?.element_id;
    if (!textboxId) throw new Error('ChatGPT prompt textbox was not available through eyeBROWSE semantics');
    await eye.fill(textboxId, prompt);
    await eye.key(target, 'Enter');
    await eye.wait(target, `document.querySelectorAll('[data-message-author-role="user"]').length === 1`, 30000, 100);
    await eye.wait(target, `document.querySelectorAll('[data-message-author-role="assistant"]').length === 1 && ![...document.querySelectorAll('button')].some(button => /stop generating/i.test(button.getAttribute('aria-label') || button.innerText || ''))`, Number(request.response_timeout_ms ?? 900000), 500);
    const responseText = await eye.jsValue(target, `(() => {
      const messages = [...document.querySelectorAll('[data-message-author-role="assistant"]')];
      return messages.at(-1)?.innerText ?? messages.at(-1)?.textContent ?? '';
    })()`);
    const parsed = JSON.parse(stripJsonFence(responseText));
    validateSubjectOutput(parsed);
    const after = await uiState();
    const fallback = !after.model_markers.some(value => /5\\.6 sol/i.test(value)) ||
      !after.reasoning_markers.some(value => /extra high/i.test(value));
    const completedUiEvidence = {
      ...uiEvidence,
      after: {
        href: after.href,
        title: after.title,
        user_message_count: after.user_message_count,
        assistant_message_count: after.assistant_message_count,
        model_markers: after.model_markers,
        reasoning_markers: after.reasoning_markers
      }
    };
    const result = {
      schema: 'world-kernel-build001-campaign2-subject-adapter-result-v1',
      passed: !fallback,
      mode: 'invoke',
      trial_id: request.trial_id,
      arm: request.arm,
      started_at: startedAt,
      completed_at: new Date().toISOString(),
      request_sha256: sha256(requestBytes),
      prompt_sha256: sha256(Buffer.from(prompt, 'utf8')),
      raw_response_sha256: sha256(Buffer.from(String(responseText), 'utf8')),
      subject_output: parsed,
      machine_readable_response_parsed: true,
      observable_product_fallback: fallback,
      invalidation_reasons: fallback ? ['observable_product_fallback'] : [],
      ui_evidence: completedUiEvidence,
      ui_evidence_sha256: hashJson(completedUiEvidence),
      operation_count: eye.operationCount
    };
    atomicWrite(outputPath, result);
    process.stdout.write(`${JSON.stringify(result)}\n`);
    process.exitCode = result.passed ? 0 : 4;
  }
} catch (error) {
  const result = {
    schema: 'world-kernel-build001-campaign2-subject-adapter-result-v1',
    passed: false,
    mode: request.mode,
    trial_id: request.trial_id,
    started_at: startedAt,
    completed_at: new Date().toISOString(),
    invalidation_reasons: ['adapter_error'],
    error_type: error?.constructor?.name ?? 'Error',
    error: String(error?.message ?? error),
    request_sha256: sha256(requestBytes),
    operation_count: eye.operationCount
  };
  atomicWrite(outputPath, result);
  process.stdout.write(`${JSON.stringify(result)}\n`);
  process.exitCode = 5;
} finally {
  if (rawTargetId) {
    try { await eye.cdp('Target.closeTarget', { targetId: rawTargetId }); } catch { }
  }
  eye.close();
}
