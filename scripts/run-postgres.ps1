param(
    [Parameter(Mandatory = $true)][string] $PostgresExecutable,
    [Parameter(Mandatory = $true)][string] $DataRoot,
    [Parameter(Mandatory = $true)][int] $Port,
    [Parameter(Mandatory = $true)][string] $LogPath
)

$ErrorActionPreference = 'Continue'
"launcher_started=$([DateTimeOffset]::UtcNow.ToString('O'))" | Out-File -FilePath $LogPath -Append -Encoding utf8
& $PostgresExecutable '-D' $DataRoot '-p' $Port '-h' '127.0.0.1' 2>&1 | Out-File -FilePath $LogPath -Append -Encoding utf8
exit $LASTEXITCODE
