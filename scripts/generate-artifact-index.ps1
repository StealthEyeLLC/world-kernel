param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\ARTIFACT-INDEX.json')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$output = [IO.Path]::GetFullPath($OutputPath)
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$excludedSegments = @('.git', 'bin', 'obj')

$files = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $full = $_.FullName
    if ($full -eq $output) { return $false }
    if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    $relative = $full.Substring($rootPrefix.Length)
    $segments = $relative -split '[\\/]'
    -not ($segments | Where-Object { $excludedSegments -contains $_ })
} | Sort-Object FullName

$entries = foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
    [ordered]@{
        path = $relative
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        byte_length = $file.Length
    }
}

$index = [ordered]@{
    schema = 'world-kernel-build001-artifact-index-v1'
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
    excludes = @('artifacts/ARTIFACT-INDEX.json', '.git/**', '**/bin/**', '**/obj/**')
    file_count = @($entries).Count
    files = @($entries)
}

$parent = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $parent | Out-Null
[IO.File]::WriteAllText($output, ($index | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $output).Hash.ToLowerInvariant()
[ordered]@{ path = $output; sha256 = $hash; file_count = $index.file_count } | ConvertTo-Json -Compress
