param(
    [Parameter(Mandatory = $true)]
    [string] $WorkingCopy,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^wk-b001-[a-z0-9][a-z0-9-]{0,62}$')]
    [string] $Branch,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-2')).TrimEnd('\') + '\'
$output = [IO.Path]::GetFullPath($OutputPath)
$workspace = [IO.Path]::GetFullPath($WorkingCopy)
$git = 'C:\Program Files\Git\cmd\git.exe'
if (-not $output.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'State observation output escapes Campaign 2.' }
if (Test-Path $output) { throw "State observation already exists: $output" }
if (-not (Test-Path (Join-Path $workspace '.git'))) { throw 'State observation target is not a Git working copy.' }
if (-not (Test-Path $git)) { throw 'Pinned git.exe is unavailable.' }

function Invoke-Git([string[]] $Arguments, [switch] $AllowFailure) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& $git -C $workspace @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    if ($exit -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') exited $exit`: $($lines -join [Environment]::NewLine)"
    }
    return [ordered]@{ arguments = $Arguments; exit_code = $exit; output = $lines }
}

function Get-Sha256([byte[]] $Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return -join ($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) }
    finally { $algorithm.Dispose() }
}

function Test-Ancestor([string] $Ancestor, [string] $Descendant) {
    $result = Invoke-Git -Arguments @('merge-base','--is-ancestor',$Ancestor,$Descendant) -AllowFailure
    return $result.exit_code -eq 0
}

$headCommand = Invoke-Git @('rev-parse','HEAD')
$branchCommand = Invoke-Git @('branch','--show-current')
$treeCommand = Invoke-Git @('rev-parse','HEAD^{tree}')
$statusCommand = Invoke-Git @('status','--porcelain=v2','--untracked-files=all')
$remoteCommand = Invoke-Git @('remote','get-url','origin')
$localBranchesCommand = Invoke-Git @('for-each-ref','--format=%(refname:short)','refs/heads')
$trackingCommand = Invoke-Git @('rev-parse','--verify',"refs/remotes/origin/$Branch") -AllowFailure
$remoteHeadCommand = Invoke-Git @('ls-remote','--heads','origin',"refs/heads/$Branch")
$parentsCommand = Invoke-Git @('rev-list','--parents','-n','1','HEAD')

$localHead = ([string]$headCommand.output[0]).Trim()
$currentBranch = ([string]$branchCommand.output[0]).Trim()
$localTree = ([string]$treeCommand.output[0]).Trim()
$remoteUrl = ([string]$remoteCommand.output[0]).Trim()
$trackingHead = if ($trackingCommand.exit_code -eq 0) { ([string]$trackingCommand.output[0]).Trim() } else { $null }
$remoteHead = $null
if ($remoteHeadCommand.output.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$remoteHeadCommand.output[0])) {
    $remoteHead = (([string]$remoteHeadCommand.output[0]) -split '\s+')[0]
}
$remoteReachable = $false
if ($remoteHead) {
    $reachable = Invoke-Git @('cat-file','-e',"$remoteHead`^{commit}") -AllowFailure
    $remoteReachable = $reachable.exit_code -eq 0
}

$tracked = @()
$trackedCommand = Invoke-Git @('ls-files')
foreach ($relative in @($trackedCommand.output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)) {
    $path = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    $prefix = $workspace.TrimEnd('\') + '\'
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Tracked path escaped the working copy.' }
    $bytes = [IO.File]::ReadAllBytes($path)
    $tracked += [ordered]@{ path = $relative.Replace('\','/'); sha256 = Get-Sha256 $bytes; byte_length = $bytes.Length }
}
$trackedJson = $tracked | ConvertTo-Json -Compress -Depth 5
$worktreeFingerprint = Get-Sha256 ([Text.Encoding]::UTF8.GetBytes($trackedJson))
$parentTokens = (([string]$parentsCommand.output[0]).Trim() -split '\s+')
$parentCount = [Math]::Max(0, $parentTokens.Count - 1)

$topology = if (-not $remoteHead) {
    'remote_ref_absent'
}
elseif ($localHead -eq $remoteHead) {
    'synchronized'
}
elseif ($remoteReachable -and (Test-Ancestor $remoteHead $localHead)) {
    'local_ahead'
}
elseif ($remoteReachable -and (Test-Ancestor $localHead $remoteHead)) {
    'remote_ahead'
}
elseif ($remoteReachable) {
    'diverged_non_fast_forward'
}
else {
    'remote_ahead_or_diverged_unfetched'
}

$result = [ordered]@{
    schema = 'world-kernel-build001-campaign2-state-observation-v1'
    observed_at = [DateTimeOffset]::UtcNow.ToString('O')
    branch = $Branch
    local_head = $localHead
    current_branch = $currentBranch
    local_tree = $localTree
    worktree_content_sha256 = $worktreeFingerprint
    worktree_clean = $statusCommand.output.Count -eq 0
    local_branches = @($localBranchesCommand.output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    remote_head = $remoteHead
    remote_tracking_head = $trackingHead
    remote_head_reachable_locally = $remoteReachable
    local_head_parent_count = $parentCount
    remote_url = $remoteUrl
    public_topology_class = $topology
    commands = @($headCommand,$branchCommand,$treeCommand,$statusCommand,$remoteCommand,$localBranchesCommand,$trackingCommand,$remoteHeadCommand,$parentsCommand,$trackedCommand)
}
$directory = Split-Path -Parent $output
[IO.Directory]::CreateDirectory($directory) | Out-Null
[IO.File]::WriteAllText($output, ($result | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
$result | ConvertTo-Json -Compress -Depth 12
