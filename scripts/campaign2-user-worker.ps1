param(
    [Parameter(Mandatory = $true)]
    [string] $JobPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-2')).TrimEnd('\') + '\'
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

function Get-GitHubToken {
    $git = 'C:\Program Files\Git\cmd\git.exe'
    $credentialInput = "protocol=https`nhost=github.com`n`n"
    $credentialOutput = $credentialInput | & $git credential fill
    if ($LASTEXITCODE -ne 0) { throw 'Configured Git credential provider failed.' }
    $passwordLine = $credentialOutput | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
    if (-not $passwordLine) { throw 'Configured GitHub credential is unavailable.' }
    return $passwordLine.Substring('password='.Length)
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
        }        'prepare' {
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
        }        'git-action' {
            $dotnet = [string]$a.dotnet
            $args = @([string]$a.cli_dll,'git-action','--semantic-action',[string]$a.semantic_action,
                '--git-executable','C:\Program Files\Git\cmd\git.exe','--fixture-root',[string]$a.fixture_root,
                '--working-copy',[string]$a.working_copy)
            switch ([string]$a.semantic_action) {
                'git:create_local_commit' {
                    $args += @('--relative-path',[string]$a.parameters.relative_path,'--message',[string]$a.parameters.message,'--timestamp',[string]$a.parameters.timestamp)
                }
                'git:create_branch' { $args += @('--branch',[string]$a.parameters.branch) }
                'git:push_ref' { $args += @('--branch',[string]$a.parameters.branch) }
                'git:integrate_fast_forward' { $args += @('--branch',[string]$a.parameters.branch) }
            }
            $result = Invoke-ExternalJson $dotnet $args
            Write-NewJson $a.output_path $result
        }
        'remote-commit' {
            $nodeResult = Invoke-ExternalJson $a.node @((Join-Path $PSScriptRoot 'eyebrowse-github-remote-commit.mjs'),
                [string]$a.sdk,[string]$a.parameters.branch,[string]$a.parameters.file,[string]$a.parameters.text,[string]$a.parameters.message)
            $receipt = [ordered]@{
                ok = $true
                semantic_action = 'github:create_remote_commit'
                receipt_accepted = $true
                exit_code = 0
                started_at = $nodeResult.started_at
                completed_at = $nodeResult.completed_at
                receipt = $nodeResult
            }
            Write-NewJson $a.output_path $receipt
        }
        'browser-observe' {
            $nodeResult = Invoke-ExternalJson $a.node @((Join-Path $PSScriptRoot 'eyebrowse-github-ref-observe.mjs'),[string]$a.sdk,[string]$a.branch)
            Write-NewJson $a.output_path $nodeResult
        }
        'provider-check' {
            $git = 'C:\Program Files\Git\cmd\git.exe'
            $gh = 'C:\WorldKernel\Build001\runtime\gh-2.97.0\gh.exe'
            $env:GH_TOKEN = Get-GitHubToken
            $deadline = [DateTimeOffset]::UtcNow.AddSeconds([int]$a.timeout_seconds)
            $matching = @()
            do {
                $json = & $gh run list --repo StealthEyeLLC/world-kernel-build-001-fixture --branch $a.branch --event push --limit 50 `
                    --json databaseId,headSha,status,conclusion,createdAt,updatedAt,workflowName
                if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect GitHub Actions runs.' }
                $runs = @($json | ConvertFrom-Json)
                $matching = @($runs | Where-Object { $_.headSha -eq $a.expected_head })
                $terminal = @($matching | Where-Object { $_.status -eq 'completed' })
                if ($matching.Count -gt 0 -and ((-not [bool]$a.expect_check) -or $terminal.Count -gt 0)) { break }
                if (-not [bool]$a.expect_check -and [DateTimeOffset]::UtcNow -ge $started.AddSeconds(15)) { break }
                Start-Sleep -Seconds 2
            } while ([DateTimeOffset]::UtcNow -lt $deadline)
            $terminal = @($matching | Where-Object { $_.status -eq 'completed' })
            $success = @($terminal | Where-Object { $_.conclusion -eq 'success' }).Count -gt 0
            $check = [ordered]@{
                schema = 'world-kernel-build001-campaign2-check-observation-v1'
                branch = $a.branch
                expected_head = $a.expected_head
                observed = $true
                started = $matching.Count -gt 0
                terminal_success = $success
                conclusion = if ($terminal.Count -gt 0) { [string]$terminal[0].conclusion } else { $null }
                runs = $matching
                observed_at = [DateTimeOffset]::UtcNow.ToString('O')
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
