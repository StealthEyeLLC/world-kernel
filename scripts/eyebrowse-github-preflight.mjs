import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, repositoryUrl, pipe = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkPath || !repositoryUrl) {
  throw new Error('usage: eyebrowse-github-preflight.mjs <sdk-path> <repository-url> [pipe]');
}

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const eye = await sdk.EyeBrowse.connect(pipe);
try {
  const status = await eye.status();
  const opened = await eye.open(repositoryUrl);
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
    user_login: document.querySelector('meta[name="user-login"]')?.content ?? '',
    sign_in_control_present: [...document.querySelectorAll('a')].some(a => /^sign in$/i.test((a.textContent ?? '').trim()))
  })`);
  const observation = await eye.observe(target);
  const signedIn = String(location?.user_login ?? '').length > 0;
  process.stdout.write(JSON.stringify({
    observer: 'eyeBROWSE.ProgramHost',
    observer_version: 'eyebrowse-build001',
    status,
    target,
    location,
    signed_in: signedIn,
    observation: {
      cursor: observation.Cursor ?? observation.cursor,
      target: observation.Target ?? observation.target,
      target_id: observation.TargetId ?? observation.targetId ?? observation.target_id,
      document: observation.Document ?? observation.document,
      url: observation.Url ?? observation.url,
      title: observation.Title ?? observation.title,
      captured_at: observation.CapturedAtUtc ?? observation.captured_at,
      providers: observation.Providers ?? observation.providers,
      semantic_element_count: (observation.Elements ?? observation.elements ?? []).length,
      semantic_controls: (observation.Elements ?? observation.elements ?? [])
        .filter(item => ['button', 'link', 'textbox'].includes(String(item.Role ?? item.role ?? '').toLowerCase()))
        .slice(0, 12)
        .map(item => ({ id: item.Id ?? item.id, role: item.Role ?? item.role, name: item.Name ?? item.name }))
    },
    operation_count: eye.operationCount,
    observed_at: new Date().toISOString()
  }) + '\n');
  if (!signedIn) process.exitCode = 3;
} finally {
  eye.close();
}
