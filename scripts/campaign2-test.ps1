param(
    [string] $ArtifactDirectory
)

. (Join-Path $PSScriptRoot 'runtime-common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactDirectory) {
    $ArtifactDirectory = Join-Path $repoRoot 'experiments\build001\campaign-2\preflight\regression'
}
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\WorldKernel.Build001\WorldKernel.Build001.csproj'
$testProject = Join-Path $repoRoot 'tests\WorldKernel.Build001.Tests\WorldKernel.Build001.Tests.csproj'

& (Join-Path $PSScriptRoot 'start-postgres.ps1') | Out-Null
& $dotnet restore (Join-Path $repoRoot 'WorldKernel.Build001.slnx') '--locked-mode'
if ($LASTEXITCODE -ne 0) { throw "dotnet restore exited with $LASTEXITCODE" }
& $dotnet build (Join-Path $repoRoot 'WorldKernel.Build001.slnx') '--configuration' 'Release' '--no-restore'
if ($LASTEXITCODE -ne 0) { throw "dotnet build exited with $LASTEXITCODE" }

& $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
    'migrate' '--repo-root' $repoRoot '--secret-file' $script:SecretsFile
if ($LASTEXITCODE -ne 0) { throw 'kernel migration failed' }
& $dotnet run '--project' $project '--configuration' 'Release' '--no-build' '--' `
    'migrate-evaluator' '--repo-root' $repoRoot '--secret-file' $script:SecretsFile
if ($LASTEXITCODE -ne 0) { throw 'evaluator migration failed' }

$runtimeSecrets = Get-RuntimeSecrets
$oldPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $runtimeSecrets.owner_password
    & (Join-Path $script:PostgresBin 'psql.exe') '-h' '127.0.0.1' '-p' $script:PostgresPort `
        '-U' 'wk_owner' '-d' 'world_kernel' '-v' 'ON_ERROR_STOP=1' `
        '-f' (Join-Path $repoRoot 'schemas\003-operator-grants.sql')
    if ($LASTEXITCODE -ne 0) { throw 'operator grant migration failed' }
}
finally {
    $env:PGPASSWORD = $oldPassword
}

& $dotnet run '--project' $testProject '--configuration' 'Release' '--no-build' '--' `
    '--repo-root' $repoRoot '--secret-file' $script:SecretsFile '--artifact-directory' $ArtifactDirectory
if ($LASTEXITCODE -ne 0) { throw "Campaign 2 regression tests exited with $LASTEXITCODE" }
