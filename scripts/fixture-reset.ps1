param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('acquisition','pilot','confirmatory','drift','hostile','preflight')]
    [string] $Phase,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')]
    [string] $BlockId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('cold','memory','structured','acquisition','pilot','drift','hostile','preflight')]
    [string] $Arm,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9._:-]{1,128}$')]
    [string] $Seed,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^wk-b001-[a-z0-9][a-z0-9-]{0,62}$')]
    [string] $Branch,

    [ValidateSet('accepted','rejected_by_provider_policy')]
    [string] $PushRegime = 'accepted',

    [ValidateSet('no_check','success','failure')]
    [string] $CheckRegime = 'success',

    [ValidateSet('fresh','stale_until_reobserve')]
    [string] $BrowserFreshness = 'fresh',

    [string] $WorkspaceRoot = 'X:\WorldKernel\Build001\workspaces',

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$git = 'C:\Program Files\Git\cmd\git.exe'
$repository = 'StealthEyeLLC/world-kernel-build-001-fixture'
$remote = "https://github.com/$repository.git"
$workspace = Join-Path $WorkspaceRoot ("reset-{0}-{1}" -f $Phase, $BlockId)
$admin = Join-Path $PSScriptRoot 'fixture-provider-admin.ps1'
if (-not (Test-Path $git)) { throw 'Pinned git.exe is unavailable.' }

function Invoke-Git([string[]] $Arguments, [switch] $AllowFailure) {
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $git -C $workspace @Arguments 2>&1
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($exit -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') exited $exit`: $($output -join [Environment]::NewLine)"
    }
    return [pscustomobject]@{ exit = $exit; output = @($output) }
}

New-Item -ItemType Directory -Force -Path $WorkspaceRoot | Out-Null
if (Test-Path $workspace) {
    if (-not (Test-Path (Join-Path $workspace '.git'))) { throw 'Reset workspace exists but is not a Git working copy.' }
    $actualRemote = (Invoke-Git -Arguments @('remote','get-url','origin')).output[0].Trim()
    if ($actualRemote -ne $remote) { throw "Reset workspace remote mismatch: $actualRemote" }
}
else {
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'Never'
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $cloneOutput = & $git clone $remote $workspace 2>&1
        $cloneExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($cloneExit -ne 0) { throw "Fixture clone failed: $($cloneOutput -join [Environment]::NewLine)" }
}

Invoke-Git -Arguments @('fetch','--prune','origin') | Out-Null

# Reset controller authority is evaluator-only and limited to an exact
# disposable branch. Remove any prior protection before deleting/recreating it.
& $admin -Operation set-push-regime -Branch $Branch -PushRegime accepted | Out-Null
$remoteRef = (Invoke-Git -Arguments @('ls-remote','--heads','origin',"refs/heads/$Branch")).output
if ($remoteRef.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$remoteRef[0])) {
    Invoke-Git -Arguments @('push','origin',":refs/heads/$Branch") | Out-Null
}

Invoke-Git -Arguments @('switch','--detach','origin/main') | Out-Null
Invoke-Git -Arguments @('clean','-fdx') | Out-Null
Invoke-Git -Arguments @('restore','--source','origin/main','--staged','--worktree','--','.') | Out-Null
$localBranch = Invoke-Git -Arguments @('show-ref','--verify','--quiet',"refs/heads/$Branch") -AllowFailure
if ($localBranch.exit -eq 0) { Invoke-Git -Arguments @('branch','-D',$Branch) | Out-Null }
Invoke-Git -Arguments @('switch','-c',$Branch) | Out-Null

$seedBytes = [Text.Encoding]::UTF8.GetBytes("$Phase|$BlockId|$Seed")
$sha = [Security.Cryptography.SHA256]::Create()
try { $seedHash = ([BitConverter]::ToString($sha.ComputeHash($seedBytes)) -replace '-', '').ToLowerInvariant() }
finally { $sha.Dispose() }
$value = [Convert]::ToInt32($seedHash.Substring(0, 6), 16) % 1000000
$stateText = "seed_version=build001-fixture-v1`ngeneration=$($seedHash.Substring(0,16))`nvalue=$value`n"
[IO.File]::WriteAllText((Join-Path $workspace 'fixture\state.txt'), $stateText, (New-Object Text.UTF8Encoding($false)))

$seconds = [Convert]::ToInt64($seedHash.Substring(0, 8), 16) % (180 * 24 * 60 * 60)
$commitTime = [DateTimeOffset]::Parse('2026-01-01T00:00:00Z').AddSeconds($seconds).ToString('O')
$oldEnvironment = @{}
foreach ($name in @('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL','GIT_AUTHOR_DATE','GIT_COMMITTER_DATE')) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:GIT_AUTHOR_NAME = 'StealthEye Build 001 Evaluator'
    $env:GIT_AUTHOR_EMAIL = 'build001-evaluator@invalid.local'
    $env:GIT_COMMITTER_NAME = $env:GIT_AUTHOR_NAME
    $env:GIT_COMMITTER_EMAIL = $env:GIT_AUTHOR_EMAIL
    $env:GIT_AUTHOR_DATE = $commitTime
    $env:GIT_COMMITTER_DATE = $commitTime
    Invoke-Git -Arguments @('add','--','fixture/state.txt') | Out-Null
    Invoke-Git -Arguments @('commit','--no-gpg-sign','-m',"Build 001 deterministic reset $BlockId") | Out-Null
}
finally {
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name]) }
}

Invoke-Git -Arguments @('push','origin',"HEAD:refs/heads/$Branch") | Out-Null
& $admin -Operation set-check-regime -CheckRegime $CheckRegime | Out-Null
& $admin -Operation set-push-regime -Branch $Branch -PushRegime $PushRegime | Out-Null
$providerConfiguration = (& $admin -Operation inspect -Branch $Branch | ConvertFrom-Json).configuration

$head = (Invoke-Git -Arguments @('rev-parse','HEAD')).output[0].Trim()
$tree = (Invoke-Git -Arguments @('rev-parse','HEAD^{tree}')).output[0].Trim()
$remoteHeadLine = (Invoke-Git -Arguments @('ls-remote','--heads','origin',"refs/heads/$Branch")).output[0]
$remoteHead = ([string]$remoteHeadLine -split '\s+')[0]
$stateHash = (Get-FileHash -Algorithm SHA256 -Path (Join-Path $workspace 'fixture\state.txt')).Hash.ToLowerInvariant()
$baseHead = (Invoke-Git -Arguments @('rev-parse','origin/main')).output[0].Trim()

$material = [ordered]@{
    repository = $repository
    provider_native_repository_id = 1330898503
    base_head = $baseHead
    local_head = $head
    remote_head = $remoteHead
    tree = $tree
    branch = $Branch
    state_sha256 = $stateHash
    push_policy = [ordered]@{
        branch_protected = $providerConfiguration.branch_protected
        required_approving_reviews = $providerConfiguration.required_approving_reviews
        admins_enforced = $providerConfiguration.admins_enforced
    }
    check_provider = [ordered]@{
        workflow_state = $providerConfiguration.workflow_state
        encrypted_check_secret_present = $providerConfiguration.encrypted_check_secret_present
    }
    browser_freshness_setup = $BrowserFreshness
}
$materialJson = $material | ConvertTo-Json -Compress -Depth 6
$materialBytes = [Text.Encoding]::UTF8.GetBytes($materialJson)
$fingerprintHasher = [Security.Cryptography.SHA256]::Create()
try { $fingerprint = ([BitConverter]::ToString($fingerprintHasher.ComputeHash($materialBytes)) -replace '-', '').ToLowerInvariant() }
finally { $fingerprintHasher.Dispose() }

$result = [ordered]@{
    reset_version = 'build001-fixture-reset-v1'
    phase = $Phase
    block_id = $BlockId
    arm = $Arm
    generation_id = [Guid]::NewGuid()
    seed_commitment_sha256 = $seedHash
    material = $material
    actual_fingerprint = $fingerprint
    exact_local_remote_match = $head -eq $remoteHead
    reset_verified = ($head -eq $remoteHead)
    provider_evidence = [ordered]@{
        configured_remote = $remote
        provider_configuration_fingerprint = (& $admin -Operation inspect -Branch $Branch | ConvertFrom-Json).configuration_fingerprint
    }
    reset_at = [DateTimeOffset]::UtcNow.ToString('O')
}

if ($OutputPath) {
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    [IO.File]::WriteAllText($OutputPath, ($result | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
}
$result | ConvertTo-Json -Compress -Depth 8
