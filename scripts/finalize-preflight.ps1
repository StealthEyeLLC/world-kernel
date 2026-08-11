. (Join-Path $PSScriptRoot 'runtime-common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\preflight'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\WorldKernel.Build001\WorldKernel.Build001.csproj'
$freezeArtifact = Join-Path $artifactRoot 'preregistration-freeze-manifest.json'
$gateManifest = Join-Path $artifactRoot 'preflight-gates.json'
$probeArtifact = Join-Path $artifactRoot 'phase-refusal-proof.json'

& (Join-Path $PSScriptRoot 'start-postgres.ps1') | Out-Null

& $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
    'freeze-preregistration' '--repo-root' $repoRoot '--secret-file' $script:SecretsFile '--output' $freezeArtifact
if ($LASTEXITCODE -ne 0) { throw 'Failed to freeze the original preregistration in the evaluator.' }

& $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
    'preflight-evaluate' '--artifact-directory' $artifactRoot '--output' $gateManifest
if ($LASTEXITCODE -ne 0) { throw 'Failed to evaluate P0-P6.' }

$manifest = Get-Content -Raw -Path $gateManifest | ConvertFrom-Json
$probes = @()
foreach ($phase in @('acquisition','pilot','confirmatory','drift')) {
    $priorErrorPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'SilentlyContinue'
        $captured = & $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
            'phase-authorize' '--preflight-manifest' $gateManifest '--phase' $phase 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $priorErrorPreference
    }
    $expectedAuthorization = [bool]$manifest.all_preflight_gates_passed
    $probePassed = if ($expectedAuthorization) { $exitCode -eq 0 } else { $exitCode -ne 0 }
    $probes += [ordered]@{
        phase = $phase
        expected_authorized = $expectedAuthorization
        exit_code = $exitCode
        refusal_or_authorization_matched_manifest = $probePassed
        output = $captured.Trim()
    }
    if (-not $probePassed) { throw "Phase gate behaved incorrectly for $phase." }
}

$proof = [ordered]@{
    schema = 'world-kernel-build001-phase-refusal-proof-v1'
    preflight_manifest_sha256 = (Get-FileHash -Algorithm SHA256 -Path $gateManifest).Hash.ToLowerInvariant()
    all_preflight_gates_passed = [bool]$manifest.all_preflight_gates_passed
    first_confirmatory_block_started = $false
    probes = $probes
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
}
[IO.File]::WriteAllText($probeArtifact, ($proof | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
$proof | ConvertTo-Json -Depth 8 -Compress
