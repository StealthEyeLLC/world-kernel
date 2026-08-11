param(
    [string] $RepositoryRoot = 'X:\WorldKernel\Build001\repo',
    [string] $SecretFile = 'C:\WorldKernel\Build001\runtime\secrets\connections.json',
    [string] $EvidenceRoot = 'X:\WorldKernel\Build001\evidence\blobs',
    [string] $WorkspaceRoot = 'X:\WorldKernel\Build001\workspaces',
    [int] $InitialBlocks = 24,
    [int] $MaximumBlocks = 36
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$campaignRoot = Join-Path $repoRoot 'experiments\build001\campaign-2'
$acquisitionRoot = Join-Path $campaignRoot 'acquisition'
$evaluatorHiddenRoot = 'C:\WorldKernel\Build001\evaluator\campaign2\acquisition'
$fixturePublicRevision = '519d05879314cab45280a9f58efbd8859ecd8d64'
$worker = Join-Path $repoRoot 'scripts\campaign2-user-worker.ps1'
$observe = Join-Path $repoRoot 'scripts\campaign2-observe-state.ps1'
$requestBuilder = Join-Path $repoRoot 'scripts\campaign2-new-subject-request.ps1'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$cliDll = Join-Path $repoRoot 'src\WorldKernel.Build001\bin\Release\net10.0\world-kernel-build-001.dll'
$node = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe'
$sdk = 'X:\AgentBrowser\repo\program-host\sdk\eyebrowse.mjs'
$preflight = Join-Path $repoRoot 'artifacts\campaign-2\preflight\preflight-gates.json'
$coldPackage = Join-Path $campaignRoot 'packages\cold.txt'
$configurationFingerprint = 'afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5'
$userId = 'STEALTHEYELLC\StealthEye'
if ($InitialBlocks -ne 24 -or $MaximumBlocks -ne 36) { throw 'Campaign 2 acquisition block bounds are frozen at 24 and 36.' }
foreach ($required in @($worker,$observe,$requestBuilder,$cliDll,$node,$sdk,$preflight,$coldPackage,$SecretFile)) {
    if (-not (Test-Path $required)) { throw "Required Campaign 2 acquisition dependency is absent: $required" }
}
[IO.Directory]::CreateDirectory($acquisitionRoot) | Out-Null
[IO.Directory]::CreateDirectory($evaluatorHiddenRoot) | Out-Null

function Write-NewJson([string] $Path, [object] $Value) {
    if (Test-Path $Path) { throw "Immutable Campaign 2 artifact already exists: $Path" }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
}

function Write-ReplaceJson([string] $Path, [object] $Value) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
    Move-Item -Force -LiteralPath $temporary -Destination $Path
}

function Invoke-Cli([string[]] $Arguments) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& $dotnet $cliDll @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    if ($exit -ne 0) { throw "Campaign 2 CLI failed: $($lines -join [Environment]::NewLine)" }
    $json = $lines | Where-Object { $_.Trim().StartsWith('{') } | Select-Object -Last 1
    if (-not $json) { throw 'Campaign 2 CLI returned no JSON result.' }
    return $json | ConvertFrom-Json
}

function Invoke-UserJob([string] $Operation, [object] $Arguments, [ValidateSet('Limited','Highest')] [string] $RunLevel, [string] $JobDirectory, [int] $TimeoutSeconds) {
    [IO.Directory]::CreateDirectory($JobDirectory) | Out-Null
    $jobId = [Guid]::NewGuid().ToString('N')
    $jobPath = Join-Path $JobDirectory ("job-$jobId.json")
    $statusPath = Join-Path $JobDirectory ("job-$jobId.status.json")
    $job = [ordered]@{
        schema = 'world-kernel-build001-campaign2-user-job-v1'
        job_id = $jobId
        operation = $Operation
        status_path = $statusPath
        arguments = $Arguments
        created_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    Write-NewJson $jobPath $job
    $taskName = "WKBuild001-C2-$jobId"
    $powerShell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    $taskArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$worker`" -JobPath `"$jobPath`""
    $action = New-ScheduledTaskAction -Execute $powerShell -Argument $taskArguments -WorkingDirectory $repoRoot
    $principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel $RunLevel
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds ($TimeoutSeconds + 60)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    try {
        Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
        Start-ScheduledTask -TaskName $taskName
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 500
            $state = (Get-ScheduledTask -TaskName $taskName).State
            if ($state -notin @('Running','Queued')) { break }
        } while ([DateTimeOffset]::UtcNow -lt $deadline)
        if ($state -in @('Running','Queued')) { throw "Campaign 2 user job timed out: $Operation" }
        if (-not (Test-Path $statusPath)) { throw "Campaign 2 user job wrote no status: $Operation" }
        $status = Get-Content -Raw -LiteralPath $statusPath | ConvertFrom-Json
        if (-not $status.ok) { throw "Campaign 2 user job failed ($Operation): $($status.error)" }
        return $status
    }
    finally {
        if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }
    }
}

function Get-Code([string] $Action) {
    switch ($Action) {
        'git:create_local_commit' { 'lc' }
        'git:create_branch' { 'br' }
        'git:push_ref' { 'ps' }
        'github:create_remote_commit' { 'rc' }
        'git:fetch_remote' { 'ft' }
        'git:integrate_fast_forward' { 'ff' }
    }
}

function Get-PushRegime([int] $Block) {
    if ($Block -le 12) { return 'accepted' }
    return 'rejected_by_provider_policy'
}

function Get-CheckRegime([int] $Block) {
    if ($Block -le 8) { return 'success' }
    return 'no_check'
}

function Get-IntegrationRegime([int] $Block) {
    if ($Block -le 12) { return 'accepted' }
    return 'rejected_non_fast_forward'
}

$actions = @(
    'git:create_local_commit',
    'git:create_branch',
    'git:push_ref',
    'github:create_remote_commit',
    'git:fetch_remote',
    'git:integrate_fast_forward'
)

Invoke-Cli @('phase-authorize','--preflight-manifest',$preflight,'--phase','acquisition') | Out-Null
$controllerStatus = Join-Path $acquisitionRoot 'controller-status.json'
$coveragePath = Join-Path $acquisitionRoot 'coverage.json'

for ($block = 1; $block -le $MaximumBlocks; $block++) {
    $configurationBlockId = 'c2-acq-{0:d2}' -f $block
    $blockSeed = 'campaign2-acquisition-block-seed-{0:d2}' -f $block
    $blockRoot = Join-Path $acquisitionRoot ("blocks\$configurationBlockId")
    [IO.Directory]::CreateDirectory($blockRoot) | Out-Null
    foreach ($semanticAction in $actions) {
        $code = Get-Code $semanticAction
        $seed = "$blockSeed-$code"
        $trialId = "$configurationBlockId-$code"
        $resetBlockId = "c2-a{0:d2}-$code" -f $block
        $resetBranch = "wk-b001-c2-a{0:d2}-$code" -f $block
        $episodeRoot = Join-Path $acquisitionRoot ("blocks\$configurationBlockId\$code")
        [IO.Directory]::CreateDirectory($episodeRoot) | Out-Null
        $closePath = Join-Path $episodeRoot 'close.json'
        if (Test-Path $closePath) { continue }
        $beginPath = Join-Path $episodeRoot 'begin.json'
        if (Test-Path $beginPath) { throw "Sealed but unclosed acquisition action requires explicit recovery: $trialId" }

        $pushRegime = if ($semanticAction -eq 'git:push_ref') { Get-PushRegime $block } else { 'accepted' }
        $checkRegime = if ($semanticAction -eq 'git:push_ref') { Get-CheckRegime $block } else { 'no_check' }
        $integrationRegime = if ($semanticAction -eq 'git:integrate_fast_forward') { Get-IntegrationRegime $block } else { 'accepted' }
        $registrationPath = Join-Path $episodeRoot 'seed-registration.json'
        if (-not (Test-Path $registrationPath)) {
            $hiddenInputPath = Join-Path $evaluatorHiddenRoot ("$configurationBlockId-$code.hidden.json")
            if (Test-Path $hiddenInputPath) { throw "Hidden action-slot registration exists without public registration: $trialId" }
            $hiddenConfiguration = [ordered]@{
                schedule_version = 'campaign2-acquisition-action-slot-v2'
                configuration_block_id = $configurationBlockId
                block_seed = $blockSeed
                seed_id = $seed
                semantic_action = $semanticAction
                reset_block_id = $resetBlockId
                branch = $resetBranch
                push_regime = $pushRegime
                check_regime = $checkRegime
                integration_regime = $integrationRegime
                browser_freshness = 'fresh'
                policy_epoch = 'stable-acquisition'
            }
            Write-NewJson $hiddenInputPath ([ordered]@{
                schema = 'world-kernel-build001-campaign2-block-registration-input-v1'
                campaign_id = 'build001-campaign-2'
                phase = 'acquisition'
                configuration_block_id = $configurationBlockId
                seed_id = $seed
                commitment_sha256 = ''
                sealed_payload_ref = $hiddenInputPath
                public_fixture_revision = $fixturePublicRevision
                hidden_configuration = $hiddenConfiguration
                expected_configuration_fingerprint = ''
            })
            Invoke-Cli @('campaign2-register-block','--repo-root',$repoRoot,'--secret-file',$SecretFile,
                '--input',$hiddenInputPath,'--output',$registrationPath) | Out-Null
        }

        Write-ReplaceJson $controllerStatus ([ordered]@{
            schema = 'world-kernel-build001-campaign2-controller-status-v1'
            state = 'running'
            block = $block
            trial_id = $trialId
            semantic_action = $semanticAction
            updated_at = [DateTimeOffset]::UtcNow.ToString('O')
        })

        $resetPath = Join-Path $episodeRoot 'reset.json'
        if (-not (Test-Path $resetPath)) {
            Invoke-UserJob 'reset' ([ordered]@{
                block_id = $resetBlockId; seed = $seed; branch = $resetBranch; push_regime = $pushRegime
                check_regime = $checkRegime; browser_freshness = 'fresh'; workspace_root = $WorkspaceRoot; output_path = $resetPath
            }) Limited $episodeRoot 300 | Out-Null
        }
        $workingCopy = Join-Path $WorkspaceRoot ("reset-acquisition-$resetBlockId")
        $independentResetPath = Join-Path $episodeRoot 'reset-independent-verification.json'
        if (-not (Test-Path $independentResetPath)) {
            Invoke-UserJob 'verify-reset' ([ordered]@{
                working_copy = $workingCopy; branch = $resetBranch; browser_freshness = 'fresh'; output_path = $independentResetPath
            }) Limited $episodeRoot 180 | Out-Null
        }
        $resetRegistrationPath = Join-Path $episodeRoot 'reset-registration.json'
        if (-not (Test-Path $resetRegistrationPath)) {
            Invoke-Cli @('campaign2-register-reset','--repo-root',$repoRoot,'--secret-file',$SecretFile,
                '--configuration-block',$configurationBlockId,'--seed-id',$seed,'--reset-manifest',$resetPath,
                '--verification',$independentResetPath,'--output',$resetRegistrationPath) | Out-Null
        }
        $preparePath = Join-Path $episodeRoot 'prepare.json'
        if (-not (Test-Path $preparePath)) {
            Invoke-UserJob 'prepare' ([ordered]@{
                semantic_action = $semanticAction; working_copy = $workingCopy; reset_branch = $resetBranch; seed = $seed
                integration_regime = $integrationRegime
                output_path = $preparePath
            }) Limited $episodeRoot 300 | Out-Null
        }
        $prepare = Get-Content -Raw -LiteralPath $preparePath | ConvertFrom-Json
        $actionBranch = [string]$prepare.action_branch

        $prePath = Join-Path $episodeRoot 'pre-observation.json'
        if (-not (Test-Path $prePath)) { & $observe -WorkingCopy $workingCopy -Branch $actionBranch -OutputPath $prePath | Out-Null }
        $requestPath = Join-Path $episodeRoot 'subject-request.json'
        if (-not (Test-Path $requestPath)) {
            & $requestBuilder -Mode invoke -TrialId $trialId -Arm acquisition -SemanticAction $semanticAction `
                -Target "fixture:$actionBranch" -Task ([string]$prepare.task + ' Return parameters exactly as: ' + ($prepare.scheduled_parameters | ConvertTo-Json -Compress -Depth 8)) `
                -CurrentObservations (Get-Content -Raw -LiteralPath $prePath) -ArmPackagePath $coldPackage `
                -ConfigurationFingerprint $configurationFingerprint -OutputPath $requestPath | Out-Null
        }
        $subjectPath = Join-Path $episodeRoot 'subject-result.json'
        if (-not (Test-Path $subjectPath)) {
            Invoke-UserJob 'subject' ([ordered]@{ request_path = $requestPath; output_path = $subjectPath }) Highest $episodeRoot 960 | Out-Null
        }

        $beginInputPath = Join-Path $episodeRoot 'begin-input.json'
        if (-not (Test-Path $beginInputPath)) {
            Write-NewJson $beginInputPath ([ordered]@{
                schema = 'world-kernel-build001-campaign2-begin-input-v1'
                campaign_id = 'build001-campaign-2'
                phase = 'acquisition'
                trial_id = $trialId
                configuration_block_id = $configurationBlockId
                evaluator_seed_id = $seed
                arm = 'acquisition'
                semantic_action = $semanticAction
                target = "fixture:$actionBranch"
                parameters = $prepare.scheduled_parameters
                working_copy = $workingCopy
                reset_branch = $resetBranch
                branch = $actionBranch
                reset_manifest_path = $resetPath
                pre_observation_path = $prePath
                subject_request_path = $requestPath
                subject_result_path = $subjectPath
            })
        }
        Invoke-Cli @('campaign2-begin','--repo-root',$repoRoot,'--secret-file',$SecretFile,'--evidence-root',$EvidenceRoot,
            '--input',$beginInputPath,'--output',$beginPath) | Out-Null

        $receiptPath = Join-Path $episodeRoot 'receipt.json'
        if ($semanticAction -eq 'github:create_remote_commit') {
            Invoke-UserJob 'remote-commit' ([ordered]@{
                node = $node; sdk = $sdk; parameters = $prepare.scheduled_parameters; output_path = $receiptPath
            }) Highest $episodeRoot 300 | Out-Null
        }
        else {
            Invoke-UserJob 'git-action' ([ordered]@{
                dotnet = $dotnet; cli_dll = $cliDll; semantic_action = $semanticAction; fixture_root = $WorkspaceRoot
                working_copy = $workingCopy; parameters = $prepare.scheduled_parameters; output_path = $receiptPath
            }) Limited $episodeRoot 300 | Out-Null
        }

        $postPath = Join-Path $episodeRoot 'post-observation.json'
        & $observe -WorkingCopy $workingCopy -Branch $actionBranch -OutputPath $postPath | Out-Null
        $post = Get-Content -Raw -LiteralPath $postPath | ConvertFrom-Json
        $receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json

        $browserPath = Join-Path $episodeRoot 'browser-observation.json'
        if ($semanticAction -in @('git:push_ref','github:create_remote_commit')) {
            Invoke-UserJob 'browser-observe' ([ordered]@{ node = $node; sdk = $sdk; branch = $actionBranch; output_path = $browserPath }) Highest $episodeRoot 180 | Out-Null
            $browserRaw = Get-Content -Raw -LiteralPath $browserPath | ConvertFrom-Json
            $browser = [ordered]@{
                observed = $true; presented_head = $browserRaw.presented_head; href = $browserRaw.href; evidence = $browserRaw
            }
        }
        else {
            $browser = [ordered]@{ observed = $false; presented_head = $null; href = $null; evidence = [ordered]@{} }
        }

        $checkPath = Join-Path $episodeRoot 'check-observation.json'
        if ($semanticAction -eq 'git:push_ref') {
            $expectCheck = [bool]$receipt.receipt_accepted -and $checkRegime -eq 'success'
            Invoke-UserJob 'provider-check' ([ordered]@{
                branch = $actionBranch; expected_head = $post.remote_head; expect_check = $expectCheck
                timeout_seconds = if ($expectCheck) { 180 } else { 25 }; output_path = $checkPath
            }) Limited $episodeRoot 240 | Out-Null
            $checkRaw = Get-Content -Raw -LiteralPath $checkPath | ConvertFrom-Json
            $check = [ordered]@{
                observed = [bool]$checkRaw.observed; started = [bool]$checkRaw.started
                terminal_success = [bool]$checkRaw.terminal_success; conclusion = $checkRaw.conclusion; runs = @($checkRaw.runs)
            }
        }
        else {
            $check = [ordered]@{ observed = $false; started = $false; terminal_success = $false; conclusion = $null; runs = @() }
        }

        $providerPath = Join-Path $episodeRoot 'provider-outcome.json'
        Write-NewJson $providerPath ([ordered]@{
            schema = 'world-kernel-build001-campaign2-provider-outcome-v1'
            observed_at = [DateTimeOffset]::UtcNow.ToString('O')
            check = $check
            browser = $browser
        })
        Invoke-Cli @('campaign2-close','--repo-root',$repoRoot,'--secret-file',$SecretFile,'--evidence-root',$EvidenceRoot,
            '--begin',$beginPath,'--receipt',$receiptPath,'--post-observation',$postPath,'--provider-outcome',$providerPath,'--output',$closePath) | Out-Null
    }

    Invoke-Cli @('campaign2-coverage','--repo-root',$repoRoot,'--secret-file',$SecretFile,'--output',$coveragePath) | Out-Null
    $coverage = Get-Content -Raw -LiteralPath $coveragePath | ConvertFrom-Json
    if ($block -ge $InitialBlocks -and $coverage.stop_rule_satisfied) { break }
}

$finalCoverage = Get-Content -Raw -LiteralPath $coveragePath | ConvertFrom-Json
if (-not $finalCoverage.stop_rule_satisfied) {
    Write-ReplaceJson $controllerStatus ([ordered]@{
        schema = 'world-kernel-build001-campaign2-controller-status-v1'; state = 'blocked_at_maximum'
        coverage = $finalCoverage; updated_at = [DateTimeOffset]::UtcNow.ToString('O')
    })
    throw 'Campaign 2 acquisition coverage was not achieved by the frozen 36-block maximum.'
}
$coverageBytes = [IO.File]::ReadAllBytes($coveragePath)
$hasher = [Security.Cryptography.SHA256]::Create()
try { $coverageHash = -join ($hasher.ComputeHash($coverageBytes) | ForEach-Object { $_.ToString('x2') }) }
finally { $hasher.Dispose() }
$completePath = Join-Path $acquisitionRoot 'acquisition-complete.json'
Write-NewJson $completePath ([ordered]@{
    schema = 'world-kernel-build001-campaign2-acquisition-complete-v1'
    campaign_id = 'build001-campaign-2'
    stop_rule_satisfied = $true
    configuration_blocks = $finalCoverage.configuration_blocks
    coverage_path = $coveragePath
    coverage_sha256 = $coverageHash
    pilot_started = $false
    confirmatory_started = $false
    drift_started = $false
    completed_at = [DateTimeOffset]::UtcNow.ToString('O')
})
Write-ReplaceJson $controllerStatus ([ordered]@{
    schema = 'world-kernel-build001-campaign2-controller-status-v1'; state = 'completed'
    configuration_blocks = $finalCoverage.configuration_blocks; coverage_sha256 = $coverageHash
    updated_at = [DateTimeOffset]::UtcNow.ToString('O')
})
$finalCoverage | ConvertTo-Json -Compress -Depth 20
