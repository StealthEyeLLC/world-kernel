param(
    [string] $RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $RuntimeRoot = $(if ($env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT) { $env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT } else { 'C:\WorldKernel\Build001\campaign3-runtime' }),
    [string] $OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$RepoRoot=[IO.Path]::GetFullPath($RepoRoot)
if(-not $OutputPath){$OutputPath=Join-Path $RepoRoot 'experiments\build001\campaign-3\preregistration-freeze-manifest.json'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path $OutputPath){throw 'Campaign 3 already has an execution freeze manifest. Replacement freezes are prohibited.'}
$branch=(& git -C $RepoRoot branch --show-current).Trim()
if($LASTEXITCODE -ne 0 -or $branch -ne 'build001-campaign-3'){throw "Campaign 3 freeze requires branch build001-campaign-3; observed $branch"}
$dirty=@(& git -C $RepoRoot status --porcelain)
if($LASTEXITCODE -ne 0){throw 'Unable to inspect Git worktree.'}
if($dirty.Count -ne 0){throw ('Campaign 3 freeze requires a clean pre-freeze commit. Dirty paths: '+($dirty -join '; '))}
$preflightPath=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\preflight-gates.json'
$p0Path=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\p0-baseline.json'
$p5Path=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\p5-fresh-invocation.json'
$headsPath=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\external-eye-heads.json'
foreach($required in @($preflightPath,$p0Path,$p5Path,$headsPath)){if(-not(Test-Path $required)){throw "Required prospective evidence is absent: $required"}}
$preflight=Get-Content $preflightPath -Raw|ConvertFrom-Json
if(-not $preflight.all_preflight_gates_passed -or -not $preflight.acquisition_authorized){throw 'P0-P6 are not all passed/authorized.'}
$gateIds=@($preflight.gates|ForEach-Object{$_.id})
if(@(@('P0','P1','P2','P3','P4','P5','P6')|Where-Object{$gateIds -notcontains $_}).Count -ne 0){throw 'P0-P6 gate set is incomplete.'}
if(@($preflight.gates|Where-Object{-not $_.passed}).Count -ne 0){throw 'At least one P0-P6 gate is not passed.'}
$p0=Get-Content $p0Path -Raw|ConvertFrom-Json
$p5=Get-Content $p5Path -Raw|ConvertFrom-Json
$fingerprint=[string]$p0.observable_configuration_fingerprint_sha256
if($fingerprint -notmatch '^[0-9a-f]{64}$'){throw 'Fresh P0 subject fingerprint is absent or invalid.'}
if([string]$p5.observable_configuration_fingerprint_sha256 -ne $fingerprint){throw 'P5 subject fingerprint differs from P0.'}
$heads=Get-Content $headsPath -Raw|ConvertFrom-Json
$expectedHeads=[ordered]@{eye='53948b74701f51c29c9322dfa9f017ba6b45f4a4';codeeye='1ca0f93d64bc20bccb3b96dbcda43a2232783609';eyebrowse='2e27f44ebd3522d0d26b036dc57f790535df3533'}
foreach($name in $expectedHeads.Keys){if([string]$heads.heads.$name -ne $expectedHeads[$name]){throw "External Eye head mismatch for $name"}}
$zeroPath=Join-Path $RepoRoot 'artifacts\campaign-3\preflight\scientific-boundary-zero.json'
& (Join-Path $PSScriptRoot 'campaign3-zero-state.ps1') -RepoRoot $RepoRoot -RuntimeRoot $RuntimeRoot -OutputPath $zeroPath | Out-Null
$zero=Get-Content $zeroPath -Raw|ConvertFrom-Json
if(-not $zero.scientific_counts_zero){throw 'Campaign 3 scientific state is nonzero.'}
$head=(& git -C $RepoRoot rev-parse HEAD).Trim()
$tree=(& git -C $RepoRoot rev-parse 'HEAD^{tree}').Trim()
if($head -notmatch '^[0-9a-f]{40}$' -or $tree -notmatch '^[0-9a-f]{40}$'){throw 'Unable to resolve pre-freeze commit/tree.'}
$genericScripts=@('codeeye-observe.mjs','eyebrowse-github-check-observe.mjs','eyebrowse-github-preflight.mjs','eyebrowse-github-ref-observe.mjs','eyebrowse-github-remote-commit.mjs','eyebrowse-stale-page-probe.mjs','finalize-hostile-suite.ps1','finalize-preflight.ps1','fixture-provider-admin.ps1','fixture-reset.ps1','hostile-local-provider.ps1','provision-postgres.ps1','recovery-test.ps1','run-experiment.ps1','run-eyebrowse-stale-probe.ps1','run-postgres.ps1','runtime-common.ps1','start-postgres.ps1','stop-postgres.ps1','test.ps1','campaign3-zero-state.ps1','campaign3-freeze.ps1')
$campaignFiles=@('experiments/build001/campaign-3/base-prompt.txt','experiments/build001/campaign-3/tool-contract.json','experiments/build001/campaign-3/trial-output-contract.json','experiments/build001/campaign-3/packages/attestation-probe.txt','experiments/build001/campaign-3/packages/cold.txt')
$tracked=@(& git -C $RepoRoot ls-files)
$selected=@($tracked|Where-Object{
    $_ -in @('AGENTS.md','Directory.Build.props','WorldKernel.Build001.slnx','docs/00-BUILD-001-AUTHORITY.md','docs/01-BUILD-001-SPEC.md','docs/02-PREREGISTRATION.md','docs/03-LIVE-BASELINE.md','docs/04-EXPERIMENT-PROTOCOL.md','docs/preregistration/original/StealthEye_World_Kernel_Build_001_Preregistration.json','docs/preregistration/original/StealthEye_World_Kernel_Build_001_Spec_and_Preregistration.md') -or
    $_.StartsWith('schemas/') -or $_.StartsWith('src/WorldKernel.Build001/') -or $_.StartsWith('tests/WorldKernel.Build001.Tests/') -or
    $_.StartsWith('scripts/campaign3-') -or (($_.StartsWith('scripts/')) -and ([IO.Path]::GetFileName($_) -in $genericScripts)) -or $_ -in $campaignFiles
}|Sort-Object -Unique)
if($selected.Count -lt 30){throw "Frozen execution file discovery is implausibly small: $($selected.Count)"}
function Hash-NormalizedText([string]$Relative){
    $full=Join-Path $RepoRoot ($Relative.Replace('/','\'))
    if(-not(Test-Path $full)){throw "Frozen file disappeared: $Relative"}
    $text=[IO.File]::ReadAllText($full).Replace("`r`n","`n")
    $sha=[Security.Cryptography.SHA256]::Create(); try{return -join($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))|ForEach-Object{$_.ToString('x2')})} finally{$sha.Dispose()}
}
$frozen=[ordered]@{}
foreach($relative in $selected){$frozen[$relative]=Hash-NormalizedText $relative}
$authority=[ordered]@{}
foreach($relative in @('AGENTS.md','docs/00-BUILD-001-AUTHORITY.md','docs/01-BUILD-001-SPEC.md','docs/02-PREREGISTRATION.md','docs/04-EXPERIMENT-PROTOCOL.md','docs/preregistration/original/StealthEye_World_Kernel_Build_001_Preregistration.json','docs/preregistration/original/StealthEye_World_Kernel_Build_001_Spec_and_Preregistration.md')){$authority[$relative]=Hash-NormalizedText $relative}
$manifest=[ordered]@{
    schema='world-kernel-build001-campaign3-execution-freeze-v2'
    campaign_id='build001-campaign-3'
    valid=$true
    single_prospective_freeze=$true
    frozen_before_acquisition=$true
    frozen_before_first_subject_invocation=$true
    scientific_subjects_run=$false
    frozen_at=[DateTimeOffset]::UtcNow.ToString('O')
    implementation=[ordered]@{commit=$head;tree=$tree;frozen_file_count=$selected.Count;frozen_files=$frozen}
    authority_hashes=$authority
    external_eye_heads=$expectedHeads
    subject_configuration_fingerprint_sha256=$fingerprint
    preflight_manifest_sha256=(Get-FileHash -Algorithm SHA256 $preflightPath).Hash.ToLowerInvariant()
    scientific_zero_state_sha256=(Get-FileHash -Algorithm SHA256 $zeroPath).Hash.ToLowerInvariant()
    constraints=[ordered]@{replacement_freeze_allowed=$false;post_freeze_source_repair_allowed=$false;campaign2_science_reuse_allowed=$false}
}
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath)|Out-Null
[IO.File]::WriteAllText($OutputPath,($manifest|ConvertTo-Json -Depth 20),(New-Object Text.UTF8Encoding($false)))
$hash=(Get-FileHash -Algorithm SHA256 $OutputPath).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($OutputPath+'.sha256',($hash+'  '+[IO.Path]::GetFileName($OutputPath)+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{ok=$true;manifest=$OutputPath;sha256=$hash;implementation_commit=$head;tree=$tree;frozen_files=$selected.Count;subject_fingerprint=$fingerprint}|ConvertTo-Json -Compress