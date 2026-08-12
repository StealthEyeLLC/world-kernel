param(
    [Parameter(Mandatory=$true)][string]$WorkingCopy,
    [Parameter(Mandatory=$true)][ValidatePattern('^wk-b001-[a-z0-9][a-z0-9-]{0,62}$')][string]$Branch,
    [Parameter(Mandatory=$true)][ValidateSet('fresh','stale_until_reobserve')][string]$BrowserFreshness,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-3')).TrimEnd('\')+'\'
$output=[IO.Path]::GetFullPath($OutputPath)
$workspace=[IO.Path]::GetFullPath($WorkingCopy)
$git='C:\Program Files\Git\cmd\git.exe'
$admin=Join-Path $PSScriptRoot 'fixture-provider-admin.ps1'
if(-not $output.StartsWith($campaignRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Reset verification output escapes Campaign 3.'}
if(Test-Path $output){throw "Reset verification already exists: $output"}
if(-not (Test-Path (Join-Path $workspace '.git'))){throw 'Reset verification target is not a Git working copy.'}
function G([string[]]$Arguments){
  $saved=$ErrorActionPreference
  try{$ErrorActionPreference='Continue';$lines=@(& $git -C $workspace @Arguments 2>&1|%{[string]$_});$exit=$LASTEXITCODE}
  finally{$ErrorActionPreference=$saved}
  if($exit -ne 0){throw "git $($Arguments -join ' ') exited $exit`: $($lines -join [Environment]::NewLine)"}
  [ordered]@{arguments=$Arguments;exit_code=$exit;output=$lines}
}
function Get-Sha256Bytes([byte[]]$Bytes){$a=[Security.Cryptography.SHA256]::Create();try{-join($a.ComputeHash($Bytes)|%{$_.ToString('x2')})}finally{$a.Dispose()}}
$head=G @('rev-parse','HEAD')
$tree=G @('rev-parse','HEAD^{tree}')
$remote=G @('ls-remote','--heads','origin',"refs/heads/$Branch")
$base=G @('rev-parse','origin/main')
if($remote.output.Count -eq 0){throw 'Reset verification found no provider branch.'}
$remoteHead=(([string]$remote.output[0])-split '\s+')[0]
$statePath=Join-Path $workspace 'fixture\state.txt'
if(-not(Test-Path $statePath)){throw 'Reset verification state file is absent.'}
$stateHash=(Get-FileHash -Algorithm SHA256 $statePath).Hash.ToLowerInvariant()
$provider=& $admin -Operation inspect -Branch $Branch | ConvertFrom-Json
$config=$provider.configuration
$material=[ordered]@{
  repository='StealthEyeLLC/world-kernel-build-001-fixture'
  provider_native_repository_id=1330898503
  base_head=([string]$base.output[0]).Trim()
  local_head=([string]$head.output[0]).Trim()
  remote_head=$remoteHead
  tree=([string]$tree.output[0]).Trim()
  branch=$Branch
  state_sha256=$stateHash
  push_policy=[ordered]@{
    branch_protected=[bool]$config.branch_protected
    required_approving_reviews=[int]$config.required_approving_reviews
    admins_enforced=[bool]$config.admins_enforced
  }
  check_provider=[ordered]@{
    workflow_state=[string]$config.workflow_state
    encrypted_check_secret_present=[bool]$config.encrypted_check_secret_present
  }
  browser_freshness_setup=$BrowserFreshness
}
$materialJson=$material|ConvertTo-Json -Compress -Depth 6
$fingerprint=Get-Sha256Bytes ([Text.Encoding]::UTF8.GetBytes($materialJson))
$result=[ordered]@{
  schema='world-kernel-build001-campaign3-independent-reset-verification-v1'
  observed_at=[DateTimeOffset]::UtcNow.ToString('O')
  branch=$Branch
  browser_freshness=$BrowserFreshness
  material=$material
  expected_fingerprint=$fingerprint
  exact_local_remote_match=(([string]$head.output[0]).Trim() -eq $remoteHead)
  provider_configuration_fingerprint=[string]$provider.configuration_fingerprint
  commands=@($head,$tree,$remote,$base)
}
[IO.Directory]::CreateDirectory((Split-Path -Parent $output))|Out-Null
[IO.File]::WriteAllText($output,($result|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
$result|ConvertTo-Json -Compress -Depth 12
