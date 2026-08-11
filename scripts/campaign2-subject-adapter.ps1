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

$script = Join-Path $PSScriptRoot 'campaign2-edge-subject.ps1'
$expectedScript = 'e2de076fd500d4e11a09d83aaa8d4db65b417345e13d9ef3b3b1cfe2facc98a0'

function Get-NormalizedSha256([string] $Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) }
    finally { $algorithm.Dispose() }
    return -join ($hash | ForEach-Object { $_.ToString('x2') })
}

if ((Get-NormalizedSha256 $script) -ne $expectedScript) {
    throw 'Frozen Campaign 2 subject driver hash changed.'
}

& $script -RequestPath $request -OutputPath $output
exit $LASTEXITCODE
