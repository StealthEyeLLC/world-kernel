param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('acquisition','pilot','confirmatory','drift')]
    [string] $Phase
)

. (Join-Path $PSScriptRoot 'runtime-common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\preflight'
$gateManifest = Join-Path $artifactRoot 'preflight-gates.json'
$p5Artifact = Join-Path $artifactRoot 'p5-fresh-invocation-blocker.json'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\WorldKernel.Build001\WorldKernel.Build001.csproj'

if (-not (Test-Path $gateManifest)) { throw 'P0-P6 have not been finalized.' }
& $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
    'phase-authorize' '--preflight-manifest' $gateManifest '--phase' $Phase
if ($LASTEXITCODE -ne 0) { throw "Build 001 $Phase is not authorized by P0-P6." }

# P5 owns the exact disposable invocation adapter. This wrapper will not substitute a
# same-conversation prompt, unrecorded API call, or arbitrary executable for that contract.
$p5 = Get-Content -Raw -Path $p5Artifact | ConvertFrom-Json
if (-not [bool]$p5.passed) { throw 'P5 is not attested.' }
if (-not $p5.invocation_adapter_path -or -not $p5.invocation_adapter_sha256) {
    throw 'P5 does not identify a frozen invocation adapter and hash.'
}
$adapter = [IO.Path]::GetFullPath([string]$p5.invocation_adapter_path)
$allowedRoot = [IO.Path]::GetFullPath($script:BuildRoot).TrimEnd('\') + '\'
if (-not $adapter.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The P5 invocation adapter is outside the experiment-owned Build 001 root.'
}
if (-not (Test-Path $adapter -PathType Leaf)) { throw "P5 invocation adapter is absent: $adapter" }
$actualHash = (Get-FileHash -Algorithm SHA256 -Path $adapter).Hash.ToLowerInvariant()
if ($actualHash -ne ([string]$p5.invocation_adapter_sha256).ToLowerInvariant()) {
    throw 'P5 invocation adapter hash changed after attestation.'
}

& $adapter '-Phase' $Phase '-RepositoryRoot' $repoRoot '-PreflightManifest' $gateManifest
if ($LASTEXITCODE -ne 0) { throw "The frozen P5 invocation adapter failed for $Phase." }
