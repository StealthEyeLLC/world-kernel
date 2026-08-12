param(
    [string] $RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $RuntimeRoot = $(if ($env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT) { $env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT } else { 'C:\WorldKernel\Build001\campaign3-runtime' }),
    [string] $OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$RepoRoot=[IO.Path]::GetFullPath($RepoRoot)
if (-not $OutputPath) { $OutputPath=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\scientific-boundary-zero.json' }
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
$campaignRoot=[IO.Path]::GetFullPath((Join-Path $RepoRoot 'experiments\build001\campaign-3')).TrimEnd('\')+'\'
if (-not $OutputPath.StartsWith([IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\campaign-3')), [StringComparison]::OrdinalIgnoreCase)) { throw 'Zero-state output escapes Campaign 3 artifacts.' }
$secrets=Get-Content (Join-Path $RuntimeRoot 'secrets\connections.json') -Raw | ConvertFrom-Json
$psql=Join-Path $RuntimeRoot 'postgresql-18.4\bin\psql.exe'
function Invoke-Scalar([string]$Database,[string]$User,[string]$Password,[string]$Sql){
    $old=$env:PGPASSWORD
    try { $env:PGPASSWORD=$Password; $v=& $psql -h 127.0.0.1 -p 55431 -U $User -d $Database -tAc $Sql; if($LASTEXITCODE -ne 0){throw "psql failed for $Database"}; return [int64]([string]$v).Trim() }
    finally { $env:PGPASSWORD=$old }
}
$k=[ordered]@{}
$k.action_attempts=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.action_attempt WHERE trial_id LIKE 'c3-%';"
$k.predictions=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.prediction p JOIN wk.action_attempt a ON a.action_id=p.action_id WHERE a.trial_id LIKE 'c3-%';"
$k.outcomes=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.outcome o JOIN wk.action_attempt a ON a.action_id=o.action_id WHERE a.trial_id LIKE 'c3-%';"
$k.prediction_evaluations=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.prediction_evaluation pe JOIN wk.prediction p ON p.prediction_id=pe.prediction_id JOIN wk.action_attempt a ON a.action_id=p.action_id WHERE a.trial_id LIKE 'c3-%';"
$k.transition_episodes=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.transition_episode WHERE public_environment_scope->>'campaign_id'='build001-campaign-3';"
$k.claims=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.claim WHERE scope->>'campaign_id'='build001-campaign-3';"
$k.correspondences=Invoke-Scalar 'world_kernel' 'wk_owner' $secrets.owner_password "SELECT count(*) FROM wk.correspondence_claim WHERE producer->>'campaign_id'='build001-campaign-3';"
$e=[ordered]@{}
$seedFilter="configuration_block_id LIKE 'c3-%' OR sealed_payload_ref ILIKE '%campaign-3%'"
$e.seed_commitments=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.seed_commitment WHERE $seedFilter;"
$e.hidden_configurations=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.hidden_configuration h WHERE EXISTS (SELECT 1 FROM eval001.seed_commitment s WHERE s.seed_id=h.seed_id AND ($seedFilter));"
$e.reset_verifications=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.reset_verification r WHERE EXISTS (SELECT 1 FROM eval001.seed_commitment s WHERE s.seed_id=r.seed_id AND ($seedFilter));"
$e.ground_truth=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.ground_truth WHERE configuration_block_id LIKE 'c3-%';"
$e.arm_randomization=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.arm_randomization WHERE configuration_block_id LIKE 'c3-%';"
$e.invocation_attestations=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.invocation_attestation WHERE configuration_block_id LIKE 'c3-%';"
$e.boundary_events=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.boundary_event WHERE details->>'campaign_id'='build001-campaign-3' AND event_type NOT IN ('prospective_freeze','science_authorization');"
$e.aggregate_results=Invoke-Scalar 'world_kernel_evaluator' 'wk_eval_owner' $secrets.evaluator_password "SELECT count(*) FROM eval001.aggregate_result WHERE statistics->>'campaign_id'='build001-campaign-3';"
$scientificRoots=@('acquisition','pilot','confirmatory','drift','hostile') | ForEach-Object { Join-Path $campaignRoot $_ }
$scientificFiles=@($scientificRoots | Where-Object {Test-Path $_} | ForEach-Object {Get-ChildItem -LiteralPath $_ -Recurse -File -ErrorAction Stop})
$subjectResults=@(Get-ChildItem -LiteralPath $campaignRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -match '^subject-result.*\.json$'})
$episodeExports=@(Get-ChildItem -LiteralPath $campaignRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {$_.Name -eq 'episode-public.json'})
$hiddenRoots=@('C:\WorldKernel\Build001\campaign3-evaluator','C:\WorldKernel\Build001\evaluator\campaign3')
$hiddenResultFiles=@($hiddenRoots | Where-Object {Test-Path $_} | ForEach-Object {Get-ChildItem -LiteralPath $_ -Recurse -File -ErrorAction Stop})
$allCounts=@($k.Values)+@($e.Values)+@($scientificFiles.Count,$subjectResults.Count,$episodeExports.Count,$hiddenResultFiles.Count)
$zero=@($allCounts | Where-Object {$_ -ne 0}).Count -eq 0
$result=[ordered]@{
    schema='world-kernel-build001-campaign3-scientific-zero-state-v1'
    campaign_id='build001-campaign-3'
    observed_at=[DateTimeOffset]::UtcNow.ToString('O')
    kernel_counts=$k
    evaluator_counts=$e
    files=[ordered]@{scientific_files=$scientificFiles.Count;subject_results=$subjectResults.Count;episode_exports=$episodeExports.Count;hidden_evaluator_result_files=$hiddenResultFiles.Count}
    generic_regression_rows_excluded_by_campaign_scope=$true
    scientific_counts_zero=$zero
}
if(-not $zero){throw ('Campaign 3 scientific state is nonzero: '+($result|ConvertTo-Json -Compress -Depth 8))}
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null
[IO.File]::WriteAllText($OutputPath,($result|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
$hash=(Get-FileHash -Algorithm SHA256 $OutputPath).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($OutputPath+'.sha256',($hash+'  '+[IO.Path]::GetFileName($OutputPath)+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
$result | Add-Member -NotePropertyName sha256 -NotePropertyValue $hash
$result | ConvertTo-Json -Compress -Depth 8