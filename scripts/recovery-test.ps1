. (Join-Path $PSScriptRoot 'runtime-common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\preflight'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
$runtimeSecrets = Get-RuntimeSecrets
$psql = Join-Path $script:PostgresBin 'psql.exe'
$oldPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $runtimeSecrets.owner_password
    $beforeRaw = & $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-tA' '-F' ',' '-c' "SELECT (SELECT count(*) FROM wk.evidence),(SELECT count(*) FROM wk.observation),(SELECT count(*) FROM wk.action_attempt),(SELECT count(*) FROM wk.transition_episode);"
    if ($LASTEXITCODE -ne 0) { throw 'recovery pre-count query failed' }
    $before = $beforeRaw.Trim()

    & (Join-Path $PSScriptRoot 'stop-postgres.ps1') -Mode immediate | Out-Null
    & (Join-Path $PSScriptRoot 'start-postgres.ps1') | Out-Null

    $afterRaw = & $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-tA' '-F' ',' '-c' "SELECT (SELECT count(*) FROM wk.evidence),(SELECT count(*) FROM wk.observation),(SELECT count(*) FROM wk.action_attempt),(SELECT count(*) FROM wk.transition_episode);"
    if ($LASTEXITCODE -ne 0) { throw 'recovery post-count query failed' }
    $after = $afterRaw.Trim()
    if ($before -ne $after) { throw "Durable-history counts changed across PostgreSQL crash recovery: $before -> $after" }

    $episodeBefore = (& $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-tAc' 'SELECT count(*) FROM wk.transition_episode;').Trim()
    $recoverySqlPath = Join-Path $artifactRoot 'recovery-mark-interrupted.sql'
    $recoverySql = @"
INSERT INTO wk.action_phase(action_phase_id,action_id,phase,payload)
SELECT gen_random_uuid(), a.action_id, 'interrupted', '{"recovery":"provider reobservation required; live process continuity not claimed"}'::jsonb
FROM wk.action_attempt a
WHERE EXISTS (SELECT 1 FROM wk.action_phase p WHERE p.action_id=a.action_id AND p.phase='dispatched')
  AND NOT EXISTS (SELECT 1 FROM wk.transition_episode e WHERE e.action_id=a.action_id)
  AND NOT EXISTS (SELECT 1 FROM wk.action_phase p WHERE p.action_id=a.action_id AND p.phase='interrupted');
"@
    [IO.File]::WriteAllText($recoverySqlPath, $recoverySql, (New-Object Text.UTF8Encoding($false)))
    & $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-v' 'ON_ERROR_STOP=1' '-f' $recoverySqlPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'interrupted-action recovery marking failed' }
    $episodeAfter = (& $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-tAc' 'SELECT count(*) FROM wk.transition_episode;').Trim()
    if ($episodeBefore -ne $episodeAfter) { throw 'Recovery fabricated a TransitionEpisode for interrupted work.' }
    $interrupted = (& $psql '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'wk_owner' '-d' 'world_kernel' '-tAc' "SELECT count(*) FROM wk.action_phase WHERE phase='interrupted';").Trim()

    $result = [ordered]@{
        test = 'postgres-crash-recovery-v1'
        mode = 'immediate stop then WAL recovery restart'
        counts_before = $before
        counts_after = $after
        interrupted_actions_marked = [int]$interrupted
        episodes_before_recovery_mark = [int]$episodeBefore
        episodes_after_recovery_mark = [int]$episodeAfter
        impossible_process_continuity_claimed = $false
        live_provider_reobservation_required = $true
        passed = $true
        observed_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $resultPath = Join-Path $artifactRoot 'recovery-test.json'
    [IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    $hash = (Get-FileHash -Algorithm SHA256 -Path $resultPath).Hash.ToLowerInvariant()
    "$hash  recovery-test.json" | Set-Content -Encoding ascii -Path ($resultPath + '.sha256')
    $result | ConvertTo-Json -Compress
}
finally {
    $env:PGPASSWORD = $oldPassword
}
