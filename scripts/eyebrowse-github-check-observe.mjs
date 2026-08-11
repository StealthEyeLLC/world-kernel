import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkArgument, branchName, expectedHead, expectCheckArgument = 'false', timeoutArgument = '180', pipeArgument = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkArgument || !branchName || !expectedHead) {
  throw new Error('usage: node eyebrowse-github-check-observe.mjs <sdk-path> <branch> <expected-head> [expect-check] [timeout-seconds] [pipe]');
}
if (!/^wk-b001-[a-z0-9][a-z0-9-]{0,62}$/.test(branchName)) throw new Error('branch is outside the Build 001 disposable namespace');
if (!/^[0-9a-f]{40}$/.test(expectedHead)) throw new Error('expected head must be a lowercase 40-character Git SHA-1');
const expectCheck = /^(1|true|yes)$/i.test(expectCheckArgument);
const timeoutSeconds = Math.max(15, Math.min(240, Number.parseInt(timeoutArgument, 10) || 180));
const repository = 'StealthEyeLLC/world-kernel-build-001-fixture';
const url = `https://github.com/${repository}/commit/${expectedHead}/checks`;
const sdk = await import(pathToFileURL(path.resolve(sdkArgument)).href);
const eye = await sdk.EyeBrowse.connect(pipeArgument);
const startedAt = new Date();
const deadline = Date.now() + timeoutSeconds * 1000;
let last = null;
let attempts = 0;

function targetOf(opened) {
  const value = opened?.target ?? opened;
  if (typeof value === 'string') return value;
  return value?.id ?? value?.Id ?? value?.targetId ?? value?.TargetId ?? value?.target_id;
}

function classify(text) {
  const normalized = String(text ?? '').replace(/\r/g, '');
  const noChecks = /There are no checks for this commit/i.test(normalized);
  const success = /deterministic-check\s+succeeded\s+in\s+/i.test(normalized);
  const failure = /deterministic-check\s+failed\s+in\s+/i.test(normalized);
  const cancelled = /deterministic-check\s+cancelled/i.test(normalized);
  const timedOut = /deterministic-check\s+timed out/i.test(normalized);
  const skipped = /deterministic-check\s+skipped/i.test(normalized);
  const hasWorkflow = /Build 001 fixture check/i.test(normalized) || /deterministic-check/i.test(normalized);
  const terminal = success || failure || cancelled || timedOut || skipped;
  let conclusion = null;
  if (success) conclusion = 'success';
  else if (failure) conclusion = 'failure';
  else if (cancelled) conclusion = 'cancelled';
  else if (timedOut) conclusion = 'timed_out';
  else if (skipped) conclusion = 'skipped';
  return { noChecks, success, failure, hasWorkflow, terminal, conclusion };
}

try {
  while (true) {
    attempts += 1;
    const opened = await eye.open(url);
    const target = targetOf(opened);
    if (!target) throw new Error('eyeBROWSE did not return a target');
    await eye.wait(target, "location.hostname === 'github.com' && document.readyState === 'complete'", 30000, 100);
    const state = await eye.jsValue(target, `({href: location.href, title: document.title, user_login: document.querySelector('meta[name="user-login"]')?.content ?? '', body: document.body?.innerText ?? ''})`);
    if (!state?.user_login) throw new Error('GitHub browser target is not authenticated');
    if (state.user_login !== 'StealthEyeLLC') throw new Error(`unexpected GitHub identity: ${state.user_login}`);
    if (state.href !== url) throw new Error(`GitHub checks target redirected unexpectedly: ${state.href}`);
    const body = String(state.body ?? '');
    if (!body.includes(branchName)) throw new Error('GitHub checks page does not identify the expected disposable branch');
    if (!body.toLowerCase().includes(expectedHead.slice(0, 7))) throw new Error('GitHub checks page does not identify the expected commit');
    const classification = classify(body);
    last = {
      target,
      href: state.href,
      title: state.title,
      user_login: state.user_login,
      branch_confirmed: true,
      expected_head_confirmed: true,
      no_checks: classification.noChecks,
      workflow_presented: classification.hasWorkflow,
      terminal: classification.terminal,
      conclusion: classification.conclusion,
      salient_text: body.split(/\r?\n/).map(value => value.trim()).filter(Boolean).filter(value =>
        value === branchName || value.includes(expectedHead.slice(0, 7)) || /Build 001 fixture check|deterministic-check|succeeded|failed|There are no checks for this commit|cancelled|timed out|skipped/i.test(value)
      ).slice(0, 80)
    };
    if (classification.terminal) break;
    if (!expectCheck && classification.noChecks) break;
    if (Date.now() >= deadline) break;
    await new Promise(resolve => setTimeout(resolve, 2000));
  }

  const observedAt = new Date();
  const started = Boolean(last?.workflow_presented && !last?.no_checks);
  const terminalSuccess = last?.conclusion === 'success';
  const result = {
    schema: 'world-kernel-build001-campaign2-eyebrowse-check-observation-v1',
    provider: 'GitHub Actions web',
    repository,
    branch: branchName,
    expected_head: expectedHead,
    expected_check: expectCheck,
    observed: true,
    started,
    terminal_success: terminalSuccess,
    conclusion: last?.conclusion ?? null,
    attempts,
    started_at: startedAt.toISOString(),
    observed_at: observedAt.toISOString(),
    elapsed_ms: observedAt.getTime() - startedAt.getTime(),
    evidence: last,
    operations: eye.operationCount
  };
  console.log(JSON.stringify(result));
} finally {
  eye.close();
}
