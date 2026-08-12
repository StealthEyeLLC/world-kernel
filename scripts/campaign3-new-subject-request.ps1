param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('inspect','invoke')]
    [string] $Mode,
    [Parameter(Mandatory = $true)]
    [string] $TrialId,
    [Parameter(Mandatory = $true)]
    [string] $Arm,
    [Parameter(Mandatory = $true)]
    [string] $SemanticAction,
    [Parameter(Mandatory = $true)]
    [string] $Target,
    [Parameter(Mandatory = $true)]
    [string] $Task,
    [Parameter(Mandatory = $true)]
    [string] $CurrentObservations,
    [Parameter(Mandatory = $true)]
    [string] $ArmPackagePath,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string] $ConfigurationFingerprint,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,
    [int] $ResponseTimeoutMs = 900000
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-3')).TrimEnd('\') + '\'
$basePrompt = Join-Path $campaignRoot 'base-prompt.txt'
$toolContract = Join-Path $campaignRoot 'tool-contract.json'
$outputContract = Join-Path $campaignRoot 'trial-output-contract.json'
$armPackage = [IO.Path]::GetFullPath($ArmPackagePath)
$output = [IO.Path]::GetFullPath($OutputPath)
$evaluation = Get-Content -Raw -Path (Join-Path $repoRoot 'schemas\evaluation-spec-v1.json') | ConvertFrom-Json
$actionProperty = $evaluation.actions.PSObject.Properties[$SemanticAction]
if ($null -eq $actionProperty) { throw "Semantic action is not in the frozen evaluation spec: $SemanticAction" }
$propositions = @($actionProperty.Value)
if ($propositions.Count -eq 0 -or $propositions.Where({ $_ -isnot [string] }).Count -gt 0) {
    throw 'Frozen evaluation spec supplied an invalid proposition vector.'
}
foreach ($candidate in @($armPackage, $output)) {
    if (-not $candidate.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Campaign 3 request path is outside the experiment namespace: $candidate"
    }
}
if (Test-Path $output) { throw "Subject request already exists and will not be overwritten: $output" }

function Get-NormalizedSha256([string] $Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = $algorithm.ComputeHash($bytes) }
    finally { $algorithm.Dispose() }
    return -join ($hash | ForEach-Object { $_.ToString('x2') })
}

$request = [ordered]@{
    schema = 'world-kernel-build001-campaign3-subject-request-v1'
    mode = $Mode
    trial_id = $TrialId
    arm = $Arm
    expected_model = '5.6 Sol'
    expected_reasoning = 'Extra High'
    observable_configuration_fingerprint_sha256 = $ConfigurationFingerprint
    base_prompt_path = $basePrompt
    base_prompt_sha256 = Get-NormalizedSha256 $basePrompt
    tool_contract_path = $toolContract
    tool_contract_sha256 = Get-NormalizedSha256 $toolContract
    trial_output_contract_sha256 = Get-NormalizedSha256 $outputContract
    arm_package_path = $armPackage
    arm_package_sha256 = Get-NormalizedSha256 $armPackage
    semantic_action = $SemanticAction
    target = $Target
    task = $Task
    current_observations = $CurrentObservations
    propositions = $propositions
    extra_treatment_model_calls = 0
    response_timeout_ms = $ResponseTimeoutMs
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
[IO.File]::WriteAllText($output, ($request | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
[ordered]@{ path = $output; sha256 = Get-NormalizedSha256 $output; trial_id = $TrialId } | ConvertTo-Json -Compress
