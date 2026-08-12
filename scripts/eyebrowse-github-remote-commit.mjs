import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, branchName, filePath, replacementText, commitMessage, pipe = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkPath || !branchName || !filePath || replacementText === undefined || !commitMessage) {
  throw new Error('usage: eyebrowse-github-remote-commit.mjs <sdk> <branch> <file> <replacement> <message> [pipe]');
}
if (!/^wk-b001-[a-z0-9][a-z0-9-]{0,62}$/.test(branchName)) {
  throw new Error('branch is outside the Build 001 disposable namespace');
}
if (filePath !== 'fixture/state.txt') {
  throw new Error('remote commit action may edit only fixture/state.txt');
}

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const eye = await sdk.EyeBrowse.connect(pipe);
const editUrl = `https://github.com/StealthEyeLLC/world-kernel-build-001-fixture/edit/${encodeURIComponent(branchName)}/${filePath}`;
const startedAt = new Date().toISOString();
try {
  const opened = await eye.open(editUrl);
  const targetValue = opened.target ?? opened;
  const target = typeof targetValue === 'string'
    ? targetValue
    : targetValue?.id ?? targetValue?.Id ?? targetValue?.targetId ?? targetValue?.TargetId ?? targetValue?.target_id;
  if (!target) throw new Error(`eyeBROWSE target.open returned no target identity: ${JSON.stringify(opened)}`);
  await eye.wait(target, "location.hostname === 'github.com' && document.readyState === 'complete'", 30000, 100);
  const location = await eye.jsValue(target, `({
    href: location.href,
    pathname: location.pathname,
    title: document.title,
    user_login: document.querySelector('meta[name="user-login"]')?.content ?? ''
  })`);
  if (String(location?.user_login ?? '').length === 0) {
    throw new Error('authenticated eyeBROWSE GitHub profile required');
  }

  const before = await eye.observe(target);
  const elementName = item => String(item?.name ?? item?.Name ?? item?.label ?? item?.Label ?? '');
  const queryItems = result => Array.isArray(result) ? result : (result?.items ?? result?.Items ?? result?.results ?? result?.Results ?? result?.elements ?? result?.Elements ?? []);
  const elementId = item => item?.id ?? item?.Id ?? item?.elementId ?? item?.ElementId ?? item?.element_id;
  const editors = await eye.query({ target, role: 'textbox', limit: 20 });
  const editorItems = queryItems(editors);
  const editor = editorItems.find(item => /edit file|file contents|code/i.test(elementName(item))) ?? editorItems[0];
  const editorId = elementId(editor);
  if (editorId) {
    await eye.fill(editorId, replacementText);
  } else {
    const focused = await eye.jsValue(target, `(() => {
      const candidate = document.querySelector('.cm-content[contenteditable="true"], [contenteditable="true"][role="textbox"]');
      if (!candidate || !(candidate.offsetWidth || candidate.offsetHeight || candidate.getClientRects().length)) return false;
      candidate.focus();
      const range = document.createRange();
      range.selectNodeContents(candidate);
      const selection = window.getSelection();
      selection.removeAllRanges();
      selection.addRange(range);
      return true;
    })()`);
    if (!focused) throw new Error('GitHub visible file editor was not available through eyeBROWSE semantics or browser-side DOM state');
    const inserted = await eye.jsValue(target, `(() => {
      const candidate = document.activeElement;
      if (!candidate || !candidate.isContentEditable) return false;
      return document.execCommand('insertText', false, ${JSON.stringify(replacementText)});
    })()`);
    if (!inserted) throw new Error('GitHub visible CodeMirror editor rejected browser-side text insertion');
  }

  const buttons = await eye.query({ target, role: 'button', limit: 60 });
  const buttonItems = queryItems(buttons);
  const commitChanges = buttonItems.find(item => /^commit changes/i.test(elementName(item)));
  const commitChangesId = elementId(commitChanges);
  if (!commitChangesId) throw new Error('GitHub Commit changes control was not found');
  await eye.click(commitChangesId);
  await eye.wait(target, "document.body && /commit changes/i.test(document.body.innerText)", 10000, 100);

  const dialogInputs = await eye.query({ target, role: 'textbox', limit: 20 });
  const inputItems = queryItems(dialogInputs);
  const messageInput = inputItems.find(item => /commit message/i.test(elementName(item)));
  const messageId = elementId(messageInput);
  if (messageId) await eye.fill(messageId, commitMessage);

  const finalButtons = await eye.query({ target, role: 'button', limit: 60 });
  const finalItems = queryItems(finalButtons);
  const finalCommit = finalItems.filter(item => /^commit changes/i.test(elementName(item))).at(-1);
  const finalId = elementId(finalCommit);
  if (!finalId) throw new Error('GitHub final Commit changes control was not found');
  await eye.click(finalId);
  await eye.wait(target, "location.pathname.includes('/blob/') && document.readyState === 'complete'", 30000, 100);
  const after = await eye.observe(target);
  const finalLocation = await eye.jsValue(target, '({href: location.href, pathname: location.pathname, title: document.title})');
  process.stdout.write(JSON.stringify({
    semantic_action: 'github:create_remote_commit',
    provider: 'GitHub web through eyeBROWSE',
    started_at: startedAt,
    completed_at: new Date().toISOString(),
    target,
    branch: branchName,
    file: filePath,
    before,
    after,
    final_location: finalLocation,
    operation_count: eye.operationCount,
    provider_receipt_only: true
  }) + '\n');
} finally {
  eye.close();
}
