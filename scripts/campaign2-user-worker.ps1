param(
    [Parameter(Mandatory = $true)]
    [string] $JobPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-2r')).TrimEnd('\') + '\'
$jobFile = [IO.Path]::GetFullPath($JobPath)
if (-not $jobFile.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Campaign 2 worker job path escapes the experiment namespace.' }
$job = Get-Content -Raw -LiteralPath $jobFile | ConvertFrom-Json
if ($job.schema -ne 'world-kernel-build001-campaign2-user-job-v1') { throw 'Campaign 2 worker job schema mismatch.' }
$statusPath = [IO.Path]::GetFullPath([string]$job.status_path)
if (-not $statusPath.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Campaign 2 worker status path escapes the experiment namespace.' }
if (Test-Path $statusPath) { throw "Campaign 2 worker status already exists: $statusPath" }

function Write-Status([object] $Value) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $statusPath)) | Out-Null
    $temporary = $statusPath + '.tmp-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $statusPath
}

function Write-NewJson([string] $Path, [object] $Value) {
    $target = [IO.Path]::GetFullPath($Path)
    if (-not $target.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Campaign 2 worker output escapes the experiment namespace.' }
    if (Test-Path $target) { throw "Campaign 2 worker output already exists: $target" }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
    [IO.File]::WriteAllText($target, ($Value | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
}

function Invoke-ExternalJson([string] $Executable, [string[]] $Arguments) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& $Executable @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    if ($exit -ne 0) { throw "$Executable exited $exit`: $($lines -join [Environment]::NewLine)" }
    $jsonLine = $lines | Where-Object { $_.Trim().StartsWith('{') } | Select-Object -Last 1
    if (-not $jsonLine) { throw "$Executable returned no JSON object." }
    return $jsonLine | ConvertFrom-Json
}

$started = [DateTimeOffset]::UtcNow
try {
    $a = $job.arguments
    switch ([string]$job.operation) {
        'reset' {
            & (Join-Path $PSScriptRoot 'fixture-reset.ps1') -Phase acquisition -BlockId $a.block_id -Arm acquisition -Seed $a.seed `
                -Branch $a.branch -PushRegime $a.push_regime -CheckRegime $a.check_regime -BrowserFreshness $a.browser_freshness `
                -WorkspaceRoot $a.workspace_root -OutputPath $a.output_path | Out-Null
        }
        'verify-reset' {
            & (Join-Path $PSScriptRoot 'campaign2-verify-reset.ps1') -WorkingCopy $a.working_copy -Branch $a.branch `
                -BrowserFreshness $a.browser_freshness -OutputPath $a.output_path | Out-Null
        }
        'prepare' {
            & (Join-Path $PSScriptRoot 'campaign2-prepare-action.ps1') -SemanticAction $a.semantic_action -WorkingCopy $a.working_copy `
                -ResetBranch $a.reset_branch -Seed $a.seed -IntegrationRegime $a.integration_regime -OutputPath $a.output_path | Out-Null
        }
        'subject' {
            $powershell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
            $result = Invoke-ExternalJson $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',
                (Join-Path $PSScriptRoot 'campaign2-subject-adapter.ps1'),'-RequestPath',$a.request_path,'-OutputPath',$a.output_path)
            if (-not $result.passed) { throw 'Campaign 2 subject invocation was invalid.' }
        }
        'material-action' {
            & (Join-Path $PSScriptRoot 'campaign2-recover-action.ps1') `
                -SemanticAction $a.semantic_action -WorkingCopy $a.working_copy `
                -PreObservationPath $a.pre_observation_path -PreparePath $a.prepare_path -OutputPath $a.output_path `
                -Dotnet $a.dotnet -CliDll $a.cli_dll -FixtureRoot $a.fixture_root -Node $a.node -Sdk $a.sdk | Out-Null
        }
        'browser-observe' {
            $nodeResult = Invoke-ExternalJson $a.node @((Join-Path $PSScriptRoot 'eyebrowse-github-ref-observe.mjs'),[string]$a.sdk,[string]$a.branch)
            Write-NewJson $a.output_path $nodeResult
        }
        'provider-check' {
            $nodeResult = Invoke-ExternalJson $a.node @(
                (Join-Path $PSScriptRoot 'eyebrowse-github-check-observe.mjs'),
                [string]$a.sdk,
                [string]$a.branch,
                [string]$a.expected_head,
                ([bool]$a.expect_check).ToString().ToLowerInvariant(),
                ([int]$a.timeout_seconds).ToString()
            )
            $check = [ordered]@{
                schema = 'world-kernel-build001-campaign2-check-observation-v1'
                branch = [string]$nodeResult.branch
                expected_head = [string]$nodeResult.expected_head
                observed = [bool]$nodeResult.observed
                started = [bool]$nodeResult.started
                terminal_success = [bool]$nodeResult.terminal_success
                conclusion = $nodeResult.conclusion
                runs = @($nodeResult.evidence)
                observer = 'eyeBROWSE/GitHub-checks-web'
                observer_receipt = $nodeResult
                observed_at = [string]$nodeResult.observed_at
            }
            Write-NewJson $a.output_path $check
        }
        default { throw "Unknown Campaign 2 worker operation: $($job.operation)" }
    }
    Write-Status ([ordered]@{
        schema = 'world-kernel-build001-campaign2-user-job-status-v1'
        ok = $true
        operation = $job.operation
        job_id = $job.job_id
        started_at = $started.ToString('O')
        completed_at = [DateTimeOffset]::UtcNow.ToString('O')
    })
    exit 0
}
catch {
    Write-Status ([ordered]@{
        schema = 'world-kernel-build001-campaign2-user-job-status-v1'
        ok = $false
        operation = $job.operation
        job_id = $job.job_id
        started_at = $started.ToString('O')
        completed_at = [DateTimeOffset]::UtcNow.ToString('O')
        error_type = $_.Exception.GetType().FullName
        error = $_.Exception.Message
    })
    exit 1
}
