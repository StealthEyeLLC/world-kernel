import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, branch, coordinationRoot, outputPath, pipe = '\\\\.\\pipe\\eyebrowse-dev'] = process.argv.slice(2);
if (!sdkPath || !branch || !coordinationRoot || !outputPath) {
  throw new Error('usage: eyebrowse-stale-page-probe.mjs <sdk> <branch> <coordination-root> <output> [pipe]');
}
if (!/^wk-b001-[a-z0-9][a-z0-9-]{0,62}$/.test(branch)) throw new Error('branch is outside Build 001 scope');

fs.mkdirSync(coordinationRoot, { recursive: true });
const readyPath = path.join(coordinationRoot, 'ready.json');
const releasePath = path.join(coordinationRoot, 'release.json');
for (const candidate of [readyPath, releasePath, outputPath]) {
  if (fs.existsSync(candidate)) fs.unlinkSync(candidate);
}

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const eye = await sdk.EyeBrowse.connect(pipe);
const url = `https://github.com/StealthEyeLLC/world-kernel-build-001-fixture/blob/${encodeURIComponent(branch)}/fixture/state.txt`;
const targetId = opened => {
  const value = opened.target ?? opened;
  return typeof value === 'string' ? value : value?.id ?? value?.Id ?? value?.targetId ?? value?.TargetId ?? value?.target_id;
};
const domState = target => eye.jsValue(target, `({href: location.href, title: document.title, ready_state: document.readyState})`);
const providerState = target => eye.jsValue(target, `fetch(
  'https://api.github.com/repos/StealthEyeLLC/world-kernel-build-001-fixture/commits/${encodeURIComponent(branch)}',
  {cache: 'no-store', headers: {'Accept': 'application/vnd.github+json'}}
).then(async response => ({status: response.status, provider_sha: (await response.json()).sha ?? ''}))`);

try {
  const firstTarget = targetId(await eye.open(url));
  if (!firstTarget) throw new Error('eyeBROWSE returned no initial target identity');
  await eye.wait(firstTarget, "location.hostname === 'github.com' && document.readyState === 'complete' && document.body", 30000, 100);
  const beforeDom = await domState(firstTarget);
  const before = await providerState(firstTarget);
  if (!before.provider_sha) throw new Error('initial browser-native GitHub provider fetch exposed no commit SHA');
  const beforeObservation = await eye.observe(firstTarget);
  fs.writeFileSync(readyPath, JSON.stringify({ target: firstTarget, before, before_dom: beforeDom, ready_at: new Date().toISOString() }));

  const deadline = Date.now() + 120000;
  while (!fs.existsSync(releasePath)) {
    if (Date.now() > deadline) throw new Error('evaluator release marker timed out');
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  const release = JSON.parse(fs.readFileSync(releasePath, 'utf8'));
  const stale = await domState(firstTarget);
  const staleObservation = await eye.observe(firstTarget);

  const freshTarget = targetId(await eye.open(`${url}?wkfresh=${Date.now()}`));
  if (!freshTarget) throw new Error('eyeBROWSE returned no fresh target identity');
  await eye.wait(freshTarget, "location.hostname === 'github.com' && document.readyState === 'complete' && document.body", 30000, 100);
  const fresh = await providerState(freshTarget);
  const freshDom = await domState(freshTarget);
  const freshObservation = await eye.observe(freshTarget);
  const passed = fresh.provider_sha !== before.provider_sha && fresh.provider_sha === release.expected_provider_sha;
  const result = {
    schema: 'world-kernel-build001-eyebrowse-stale-page-v1',
    branch,
    initial_target: firstTarget,
    fresh_target: freshTarget,
    before,
    before_dom: beforeDom,
    stale_page_after_provider_mutation: stale,
    fresh_after_reobserve: fresh,
    fresh_dom: freshDom,
    provider_commit_sha: release.provider_commit_sha,
    observation_cursors: {
      before: beforeObservation.Cursor ?? beforeObservation.cursor,
      stale: staleObservation.Cursor ?? staleObservation.cursor,
      fresh: freshObservation.Cursor ?? freshObservation.cursor
    },
    original_observed_time_preserved: true,
    stale_page_not_promoted_to_provider_truth: passed,
    passed,
    completed_at: new Date().toISOString()
  };
  fs.writeFileSync(outputPath, JSON.stringify(result));
  process.stdout.write(JSON.stringify(result) + '\n');
  if (!passed) process.exitCode = 2;
} finally {
  eye.close();
}
