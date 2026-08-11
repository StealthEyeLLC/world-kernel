param(
    [Parameter(Mandatory = $true)]
    [string] $RequestPath,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-2')).TrimEnd('\') + '\'
$request = [IO.Path]::GetFullPath($RequestPath)
$output = [IO.Path]::GetFullPath($OutputPath)
foreach ($candidate in @($request, $output)) {
    if (-not $candidate.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Campaign 2 subject path is outside the experiment namespace: $candidate"
    }
}
if (-not (Test-Path $request -PathType Leaf)) { throw "Subject request is absent: $request" }
if (Test-Path $output) { throw "Subject output already exists and will not be overwritten: $output" }

$node = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe'
$sdk = 'X:\AgentBrowser\repo\program-host\sdk\eyebrowse.mjs'
$script = Join-Path $PSScriptRoot 'campaign2-chatgpt-subject.mjs'
$expectedSdk = '72fef71df188fae2805580b0c3382543ac4eb47daa61f480ecda3fb0623e047b'
$expectedScript = '28d2ce6a73ce6ca96ef38151deabf7a13f2031d4e73e2421fb9ca68886c40195'

if ((Get-FileHash -Algorithm SHA256 -Path $sdk).Hash.ToLowerInvariant() -ne $expectedSdk) {
    throw 'Frozen eyeBROWSE SDK hash changed.'
}
if ((Get-FileHash -Algorithm SHA256 -Path $script).Hash.ToLowerInvariant() -ne $expectedScript) {
    throw 'Frozen Campaign 2 subject driver hash changed.'
}
if (-not (Test-Path $node -PathType Leaf)) { throw 'Pinned portable Node runtime is absent.' }

& $node $script $sdk $request $output
exit $LASTEXITCODE
