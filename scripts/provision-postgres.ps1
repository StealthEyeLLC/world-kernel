param(
    [switch] $SkipDownload
)

. (Join-Path $PSScriptRoot 'runtime-common.ps1')

New-Item -ItemType Directory -Force -Path $script:RuntimeRoot, $script:DownloadRoot, $script:SecretsRoot | Out-Null

if (-not (Test-Path $script:PostgresArchive)) {
    if ($SkipDownload) { throw "PostgreSQL archive is absent and -SkipDownload was specified." }
    $job = Get-BitsTransfer -AllUsers -ErrorAction SilentlyContinue | Where-Object DisplayName -eq 'WorldKernel-Build001-PostgreSQL18'
    if ($job) {
        if ($job.JobState -eq 'Transferred') {
            Complete-BitsTransfer -BitsJob $job
        }
        elseif ($job.JobState -in @('Error','TransientError','Cancelled')) {
            throw "PostgreSQL BITS job is $($job.JobState): $($job.ErrorDescription)"
        }
        else {
            throw "PostgreSQL BITS download is not complete: $($job.JobState) $($job.BytesTransferred)/$($job.BytesTotal)"
        }
    }
    else {
        Start-BitsTransfer -Source $script:PostgresUrl -Destination $script:PostgresArchive -DisplayName 'WorldKernel-Build001-PostgreSQL18'
    }
}

$archiveHash = (Get-FileHash -Algorithm SHA256 -Path $script:PostgresArchive).Hash.ToLowerInvariant()

if (-not (Test-Path (Join-Path $script:PostgresBin 'postgres.exe'))) {
    $extractRoot = Join-Path $script:RuntimeRoot ('extract-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    try {
        $tar = Get-Command tar.exe -ErrorAction SilentlyContinue
        if ($tar) {
            & $tar.Source '-xf' $script:PostgresArchive '-C' $extractRoot
            if ($LASTEXITCODE -ne 0) { throw "tar.exe extraction exited with $LASTEXITCODE" }
        }
        else {
            Expand-Archive -LiteralPath $script:PostgresArchive -DestinationPath $extractRoot
        }
        $expanded = Join-Path $extractRoot 'pgsql'
        if (-not (Test-Path (Join-Path $expanded 'bin\postgres.exe'))) { throw 'PostgreSQL archive layout was not recognized.' }
        if (Test-Path $script:PostgresRoot) { throw "Partial PostgreSQL runtime already exists: $script:PostgresRoot" }
        Move-Item -LiteralPath $expanded -Destination $script:PostgresRoot
    }
    finally {
        if (Test-Path $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force }
    }
}

if (-not (Test-Path $script:SecretsFile)) {
    $postgresPassword = Get-RandomHex
    $ownerPassword = Get-RandomHex
    $operatorPassword = Get-RandomHex
    $evaluatorPassword = Get-RandomHex
    $memoryPassword = Get-RandomHex
    $coldPassword = Get-RandomHex
    $secrets = [ordered]@{
        postgres_password = $postgresPassword
        owner_password = $ownerPassword
        operator_password = $operatorPassword
        evaluator_password = $evaluatorPassword
        memory_password = $memoryPassword
        cold_password = $coldPassword
        postgres_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=postgres;Username=postgres;Password=$postgresPassword;Application Name=WorldKernel.Build001.Provision"
        owner_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=world_kernel;Username=wk_owner;Password=$ownerPassword;Application Name=WorldKernel.Build001.Owner"
        operator_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=world_kernel;Username=wk_operator;Password=$operatorPassword;Application Name=WorldKernel.Build001.Operator"
        evaluator_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=world_kernel_evaluator;Username=wk_eval_owner;Password=$evaluatorPassword;Application Name=WorldKernel.Build001.Evaluator"
        memory_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=world_kernel;Username=wk_memory;Password=$memoryPassword;Application Name=WorldKernel.Build001.MemoryIsolationProbe"
        cold_connection = "Host=127.0.0.1;Port=$script:PostgresPort;Database=world_kernel;Username=wk_cold;Password=$coldPassword;Application Name=WorldKernel.Build001.ColdIsolationProbe"
    }
    [IO.File]::WriteAllText($script:SecretsFile, ($secrets | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    & icacls.exe $script:SecretsFile '/inheritance:r' '/grant:r' 'SYSTEM:(F)' 'Administrators:(F)' | Out-Null
}

$runtimeSecrets = Get-RuntimeSecrets
if (-not (Test-Path (Join-Path $script:PostgresData 'PG_VERSION'))) {
    $pwFile = Join-Path $script:SecretsRoot 'initdb-password.txt'
    [string]$runtimeSecrets.postgres_password | Set-Content -Encoding ascii -NoNewline -Path $pwFile
    try {
        & (Join-Path $script:PostgresBin 'initdb.exe') '--pgdata' $script:PostgresData '--username' 'postgres' '--pwfile' $pwFile '--encoding' 'UTF8' '--locale' 'C' '--auth-host' 'scram-sha-256' '--auth-local' 'scram-sha-256'
        if ($LASTEXITCODE -ne 0) { throw "initdb exited with $LASTEXITCODE" }
    }
    finally {
        if (Test-Path $pwFile) { Remove-Item -LiteralPath $pwFile -Force }
    }
}

& (Join-Path $PSScriptRoot 'start-postgres.ps1') | Out-Null

$previousPgPassword = $env:PGPASSWORD
$env:PGPASSWORD = $runtimeSecrets.postgres_password

function Ensure-Role([string] $Role, [string] $Password) {
    $exists = & (Join-Path $script:PostgresBin 'psql.exe') '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'postgres' '-d' 'postgres' '-tAc' "SELECT 1 FROM pg_roles WHERE rolname='$Role'" 2>$null
    if ($exists -ne '1') {
        Invoke-PostgresTool 'psql.exe' @('-h','127.0.0.1','-p',"$script:PostgresPort",'-U','postgres','-d','postgres','-v','ON_ERROR_STOP=1','-c',"CREATE ROLE $Role LOGIN PASSWORD '$Password'") $runtimeSecrets.postgres_password
    }
    else {
        Invoke-PostgresTool 'psql.exe' @('-h','127.0.0.1','-p',"$script:PostgresPort",'-U','postgres','-d','postgres','-v','ON_ERROR_STOP=1','-c',"ALTER ROLE $Role LOGIN PASSWORD '$Password'") $runtimeSecrets.postgres_password
    }
}

function Ensure-Database([string] $Database, [string] $Owner) {
    $exists = & (Join-Path $script:PostgresBin 'psql.exe') '-h' '127.0.0.1' '-p' $script:PostgresPort '-U' 'postgres' '-d' 'postgres' '-tAc' "SELECT 1 FROM pg_database WHERE datname='$Database'" 2>$null
    if ($exists -ne '1') {
        Invoke-PostgresTool 'createdb.exe' @('-h','127.0.0.1','-p',"$script:PostgresPort",'-U','postgres','-O',$Owner,$Database) $runtimeSecrets.postgres_password
    }
}

try {
    Ensure-Role 'wk_owner' $runtimeSecrets.owner_password
    Ensure-Role 'wk_operator' $runtimeSecrets.operator_password
    Ensure-Role 'wk_eval_owner' $runtimeSecrets.evaluator_password
    Ensure-Role 'wk_memory' $runtimeSecrets.memory_password
    Ensure-Role 'wk_cold' $runtimeSecrets.cold_password
    Ensure-Database 'world_kernel' 'wk_owner'
    Ensure-Database 'world_kernel_evaluator' 'wk_eval_owner'
    Invoke-PostgresTool 'psql.exe' @('-h','127.0.0.1','-p',"$script:PostgresPort",'-U','postgres','-d','postgres','-v','ON_ERROR_STOP=1','-c','REVOKE CONNECT ON DATABASE world_kernel FROM PUBLIC; GRANT CONNECT ON DATABASE world_kernel TO wk_owner, wk_operator;') $runtimeSecrets.postgres_password
    Invoke-PostgresTool 'psql.exe' @('-h','127.0.0.1','-p',"$script:PostgresPort",'-U','postgres','-d','postgres','-v','ON_ERROR_STOP=1','-c','REVOKE CONNECT ON DATABASE world_kernel_evaluator FROM PUBLIC; GRANT CONNECT ON DATABASE world_kernel_evaluator TO wk_eval_owner;') $runtimeSecrets.postgres_password
}
finally {
    $env:PGPASSWORD = $previousPgPassword
}

$version = & (Join-Path $script:PostgresBin 'postgres.exe') '--version'
$manifest = [ordered]@{
    provisioned_at = [DateTimeOffset]::UtcNow.ToString('O')
    topology = 'portable loopback process hosted by a manual triggerless experiment-owned scheduled task; no Windows SCM service'
    source_url = $script:PostgresUrl
    archive_path = $script:PostgresArchive
    archive_sha256 = $archiveHash
    postgres_version = $version.Trim()
    bin_root = $script:PostgresBin
    data_root = $script:PostgresData
    port = $script:PostgresPort
    listen_addresses = '127.0.0.1'
    on_demand_scheduled_task = $script:PostgresTaskName
    auth = 'scram-sha-256'
    secret_file = $script:SecretsFile
}
$manifestPath = Join-Path $script:RuntimeRoot 'postgres-runtime-manifest.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
$manifest | ConvertTo-Json -Compress
