import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, branchName, pipe = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkPath || !branchName) {
  throw new Error('usage: eyebrowse-github-ref-observe.mjs <sdk> <branch> [pipe]');
}
if (!/^wk-b001-[a-z0-9][a-z0-9-]{0,62}$/.test(branchName)) {
  throw new Error('branch is outside the Build 001 disposable namespace');
}

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const eye = await sdk.EyeBrowse.connect(pipe);
const url = `https://github.com/StealthEyeLLC/world-kernel-build-001-fixture/commits/${encodeURIComponent(branchName)}`;
const startedAt = new Date().toISOString();
try {
  const opened = await eye.open(url);
  const targetValue = opened.target ?? opened;
  const target = typeof targetValue === 'string'
    ? targetValue
    : targetValue?.id ?? targetValue?.Id ?? targetValue?.targetId ?? targetValue?.TargetId ?? targetValue?.target_id;
  if (!target) throw new Error(`eyeBROWSE target.open returned no target identity: ${JSON.stringify(opened)}`);
  await eye.wait(target, "location.hostname === 'github.com' && document.readyState === 'complete'", 30000, 100);
  const location = await eye.jsValue(target, `(() => {
    const hrefs = [...document.querySelectorAll('a[href*="/commit/"]')].map(a => a.href);
    const match = hrefs.map(href => href.match(/\\/commit\\/([0-9a-f]{40})(?:$|[?#/])/i)).find(Boolean);
    return {
      href: location.href,
      pathname: location.pathname,
      title: document.title,
      user_login: document.querySelector('meta[name="user-login"]')?.content ?? '',
      presented_head: match?.[1]?.toLowerCase() ?? null,
      commit_link_count: hrefs.length
    };
  })()`);
  if (String(location?.user_login ?? '').length === 0) {
    throw new Error('authenticated eyeBROWSE GitHub profile required');
  }
  if (!location?.presented_head) {
    throw new Error('GitHub branch presentation did not expose a full head commit link');
  }
  const observation = await eye.observe(target);
  process.stdout.write(JSON.stringify({
    schema: 'world-kernel-build001-campaign2-browser-ref-observation-v1',
    branch: branchName,
    started_at: startedAt,
    observed_at: new Date().toISOString(),
    presented_head: location.presented_head,
    href: location.href,
    location,
    observation,
    operation_count: eye.operationCount
  }) + '\n');
} finally {
  eye.close();
}
