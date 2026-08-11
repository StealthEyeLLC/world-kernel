import path from 'node:path';
import { pathToFileURL } from 'node:url';

const [sdkPath, solutionPath, pipe = 'codeeye-dev'] = process.argv.slice(2);
if (!sdkPath || !solutionPath) {
  throw new Error('usage: codeeye-observe.mjs <sdk-path> <solution-path> [pipe]');
}

const sdk = await import(pathToFileURL(path.resolve(sdkPath)).href);
const client = new sdk.CODEeyeClient({ pipe, timeoutMs: 300000 });
const startedAt = new Date().toISOString();
try {
  const attachment = await client.workspaceAttach(path.resolve(solutionPath));
  const workspaceId = attachment.workspaceId ?? attachment.workspace_id ?? attachment.id;
  if (!workspaceId) throw new Error('CODEeye workspace.attach returned no workspace identity');
  const [repositoryStatus, worldSync, gitDiff] = await Promise.all([
    client.repoStatus(workspaceId),
    client.worldSync(workspaceId),
    client.gitDiff(workspaceId, false)
  ]);
  process.stdout.write(JSON.stringify({
    observer: 'CODEeye.ProgramHost',
    observer_version: 'codeeye/1',
    started_at: startedAt,
    observed_at: new Date().toISOString(),
    solution_path: path.resolve(solutionPath),
    attachment,
    repository_status: repositoryStatus,
    world_sync: worldSync,
    git_diff: gitDiff,
    operation_count: client.operations
  }) + '\n');
} finally {
  await client.close();
}

