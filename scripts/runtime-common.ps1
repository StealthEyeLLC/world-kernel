$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:BuildRoot = if ($env:WORLD_KERNEL_BUILD001_ROOT) { $env:WORLD_KERNEL_BUILD001_ROOT } else { 'X:\WorldKernel\Build001' }
$script:RuntimeRoot = if ($env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT) { $env:WORLD_KERNEL_BUILD001_RUNTIME_ROOT } else { 'C:\WorldKernel\Build001\runtime' }
$script:PostgresRoot = Join-Path $script:RuntimeRoot 'postgresql-18.4'
$script:PostgresBin = Join-Path $script:PostgresRoot 'bin'
$script:PostgresData = Join-Path $script:RuntimeRoot 'pgdata'
$script:PostgresLog = Join-Path $script:RuntimeRoot 'postgresql.log'
$script:SecretsRoot = Join-Path $script:RuntimeRoot 'secrets'
$script:SecretsFile = Join-Path $script:SecretsRoot 'connections.json'
$script:DownloadRoot = Join-Path $script:BuildRoot 'downloads'
$script:PostgresArchive = Join-Path $script:DownloadRoot 'postgresql-18.4-2-windows-x64-binaries.zip'
$script:PostgresUrl = 'https://get.enterprisedb.com/postgresql/postgresql-18.4-2-windows-x64-binaries.zip'
$script:PostgresPort = 55431
$script:PostgresTaskName = 'world-kernel-build001-postgres'

function Get-RandomHex([int] $ByteCount = 24) {
    $bytes = New-Object byte[] $ByteCount
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    return ([BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}

function Get-RuntimeSecrets {
    if (-not (Test-Path $script:SecretsFile)) { throw "Runtime secret file does not exist: $script:SecretsFile" }
    return Get-Content -Raw -Path $script:SecretsFile | ConvertFrom-Json
}

function Invoke-PostgresTool([string] $Tool, [string[]] $Arguments, [string] $Password) {
    $oldPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $Password
        & (Join-Path $script:PostgresBin $Tool) @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Tool exited with $LASTEXITCODE" }
    }
    finally {
        $env:PGPASSWORD = $oldPassword
    }
}
