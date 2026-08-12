param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('git:create_local_commit','git:create_branch','git:push_ref','github:create_remote_commit','git:fetch_remote','git:integrate_fast_forward')]
    [string] $SemanticAction,

    [Parameter(Mandatory = $true)]
    [string] $WorkingCopy,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^wk-b001-[a-z0-9][a-z0-9-]{0,62}$')]
    [string] $ResetBranch,

    [Parameter(Mandatory = $true)]
    [string] $Seed,

    [ValidateSet('accepted','rejected_non_fast_forward')]
    [string] $IntegrationRegime = 'accepted',

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-3')).TrimEnd('\') + '\'
$output = [IO.Path]::GetFullPath($OutputPath)
$workspace = [IO.Path]::GetFullPath($WorkingCopy)
$git = 'C:\Program Files\Git\cmd\git.exe'
$remote = 'https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git'
if (-not $output.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Action preparation output escapes Campaign 3.' }
if (Test-Path $output) { throw "Action preparation already exists: $output" }
if (-not (Test-Path (Join-Path $workspace '.git'))) { throw 'Action preparation target is not a Git working copy.' }

function Invoke-Git([string] $Directory, [string[]] $Arguments, [switch] $AllowFailure) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& $git -C $Directory @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    if ($exit -ne 0 -and -not $AllowFailure) { throw "git $($Arguments -join ' ') exited $exit`: $($lines -join [Environment]::NewLine)" }
    return [ordered]@{ working_directory = $Directory; arguments = $Arguments; exit_code = $exit; output = $lines }
}

function Get-Sha256Text([string] $Text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return -join ($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)) | ForEach-Object { $_.ToString('x2') }) }
    finally { $algorithm.Dispose() }
}

$seedHash = Get-Sha256Text "$SemanticAction|$ResetBranch|$Seed"
$seconds = [Convert]::ToInt64($seedHash.Substring(0,8),16) % (180 * 24 * 60 * 60)
$timestamp = [DateTimeOffset]::Parse('2026-01-01T00:00:00Z').AddSeconds($seconds).ToString('O')
$commands = @()

function Set-State([string] $Directory, [string] $Role) {
    $text = "campaign=build001-campaign-3`nseed=$Seed`naction=$SemanticAction`nrole=$Role`ngeneration=$($seedHash.Substring(0,16))`n"
    [IO.File]::WriteAllText((Join-Path $Directory 'fixture\state.txt'), $text, (New-Object Text.UTF8Encoding($false)))
    return $text
}

function New-Commit([string] $Directory, [string] $Message, [string] $CommitTimestamp) {
    $old = @{}
    foreach ($name in @('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL','GIT_AUTHOR_DATE','GIT_COMMITTER_DATE')) {
        $old[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    try {
        $env:GIT_AUTHOR_NAME = 'StealthEye Build 001 Evaluator'
        $env:GIT_AUTHOR_EMAIL = 'build001-evaluator@invalid.local'
        $env:GIT_COMMITTER_NAME = $env:GIT_AUTHOR_NAME
        $env:GIT_COMMITTER_EMAIL = $env:GIT_AUTHOR_EMAIL
        $env:GIT_AUTHOR_DATE = $CommitTimestamp
        $env:GIT_COMMITTER_DATE = $CommitTimestamp
        $script:commands += Invoke-Git $Directory @('add','--','fixture/state.txt')
        $script:commands += Invoke-Git $Directory @('commit','--no-gpg-sign','-m',$Message)
    }
    finally {
        foreach ($name in $old.Keys) { [Environment]::SetEnvironmentVariable($name, $old[$name]) }
    }
}

function New-RemoteAhead([string] $Role) {
    $controllerRoot = Join-Path (Split-Path -Parent $workspace) ("controller-{0}-{1}" -f $ResetBranch,$seedHash.Substring(0,8))
    if (Test-Path $controllerRoot) { throw "Controller clone already exists: $controllerRoot" }
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $cloneOutput = @(& $git clone --branch $ResetBranch --single-branch $remote $controllerRoot 2>&1 | ForEach-Object { [string]$_ })
        $cloneExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    $script:commands += [ordered]@{ working_directory = (Split-Path -Parent $controllerRoot); arguments = @('clone','--branch',$ResetBranch,'--single-branch',$remote,$controllerRoot); exit_code = $cloneExit; output = $cloneOutput }
    if ($cloneExit -ne 0) { throw "Controller clone failed: $($cloneOutput -join [Environment]::NewLine)" }
    Set-State $controllerRoot $Role | Out-Null
    New-Commit $controllerRoot "Campaign 3 evaluator remote setup $ResetBranch" ([DateTimeOffset]::Parse($timestamp).AddSeconds(2).ToString('O'))
    $script:commands += Invoke-Git $controllerRoot @('push','origin',"HEAD:refs/heads/$ResetBranch")
    return $controllerRoot
}

$actionBranch = $ResetBranch
$scheduled = [ordered]@{}
$task = ''
switch ($SemanticAction) {
    'git:create_local_commit' {
        Set-State $workspace 'operator-pending-local-commit' | Out-Null
        $scheduled = [ordered]@{ relative_path = 'fixture/state.txt'; message = "Campaign 3 measured local commit $ResetBranch"; timestamp = $timestamp }
        $task = 'Create one local commit from the evaluator-prepared tracked fixture/state.txt change using the exact message and timestamp parameters.'
    }
    'git:create_branch' {
        $actionBranch = "$ResetBranch-new"
        if ($actionBranch.Length -gt 63) { throw 'Derived action branch exceeds the frozen disposable namespace.' }
        $scheduled = [ordered]@{ branch = $actionBranch }
        $task = 'Create and switch to the exact new local branch parameter without pushing it.'
    }
    'git:push_ref' {
        Set-State $workspace 'operator-local-ahead-for-push' | Out-Null
        New-Commit $workspace "Campaign 3 evaluator push setup $ResetBranch" $timestamp
        $scheduled = [ordered]@{ branch = $ResetBranch }
        $task = 'Push the current local HEAD once to the exact remote branch parameter without force.'
    }
    'github:create_remote_commit' {
        $replacement = "campaign=build001-campaign-3`nseed=$Seed`naction=$SemanticAction`nrole=operator-remote-commit`ngeneration=$($seedHash.Substring(0,16))`n"
        $scheduled = [ordered]@{ branch = $ResetBranch; file = 'fixture/state.txt'; text = $replacement; message = "Campaign 3 measured remote commit $ResetBranch" }
        $task = 'Create one hosted commit through eyeBROWSE on the exact branch and file using the exact replacement text and commit message.'
    }
    'git:fetch_remote' {
        New-RemoteAhead 'evaluator-remote-ahead-for-fetch' | Out-Null
        $scheduled = [ordered]@{}
        $task = 'Fetch and prune the configured origin once; do not integrate or change the checked-out branch.'
    }
    'git:integrate_fast_forward' {
        if ($IntegrationRegime -eq 'rejected_non_fast_forward') {
            Set-State $workspace 'evaluator-local-divergent-for-integration' | Out-Null
            New-Commit $workspace "Campaign 3 evaluator local divergence $ResetBranch" $timestamp
        }
        New-RemoteAhead 'evaluator-remote-target-for-integration' | Out-Null
        $commands += Invoke-Git $workspace @('fetch','--prune','origin')
        $scheduled = [ordered]@{ branch = $ResetBranch }
        $task = 'Attempt exactly one fast-forward-only integration of the exact origin remote-tracking branch; do not merge or rebase on rejection.'
    }
}

$head = Invoke-Git $workspace @('rev-parse','HEAD')
$remoteHead = Invoke-Git $workspace @('ls-remote','--heads','origin',"refs/heads/$ResetBranch")
$commands += $head
$commands += $remoteHead
$result = [ordered]@{
    schema = 'world-kernel-build001-campaign3-action-preparation-v1'
    semantic_action = $SemanticAction
    reset_branch = $ResetBranch
    action_branch = $actionBranch
    seed = $Seed
    integration_regime = $IntegrationRegime
    scheduled_parameters = $scheduled
    task = $task
    working_copy = $workspace
    commands = $commands
    prepared_at = [DateTimeOffset]::UtcNow.ToString('O')
}
[IO.Directory]::CreateDirectory((Split-Path -Parent $output)) | Out-Null
[IO.File]::WriteAllText($output, ($result | ConvertTo-Json -Depth 14), (New-Object Text.UTF8Encoding($false)))
$result | ConvertTo-Json -Compress -Depth 14
