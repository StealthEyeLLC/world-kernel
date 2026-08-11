param(
    [ValidateSet('smart','fast','immediate')]
    [string] $Mode = 'fast'
)

. (Join-Path $PSScriptRoot 'runtime-common.ps1')

if (Test-Path (Join-Path $script:PostgresBin 'pg_ctl.exe')) {
    & (Join-Path $script:PostgresBin 'pg_ctl.exe') status '-D' $script:PostgresData *> $null
    if ($LASTEXITCODE -eq 0) {
        & (Join-Path $script:PostgresBin 'pg_ctl.exe') stop '-D' $script:PostgresData '-m' $Mode '-w' '-t' '30'
        if ($LASTEXITCODE -ne 0) { throw "pg_ctl stop exited with $LASTEXITCODE" }
    }
}
$task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
if ($task -and $task.State -eq 'Running') {
    Stop-ScheduledTask -TaskName $script:PostgresTaskName
}
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(10)
do {
    Start-Sleep -Milliseconds 100
    $task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
    $pidFilePresent = Test-Path (Join-Path $script:PostgresData 'postmaster.pid')
} while ((($task -and $task.State -eq 'Running') -or $pidFilePresent) -and [DateTimeOffset]::UtcNow -lt $deadline)
if (($task -and $task.State -eq 'Running') -or $pidFilePresent) {
    throw 'PostgreSQL runtime did not settle after stop.'
}
[pscustomobject]@{ running = $false; mode = $Mode; windows_scm_service_registered = $false; on_demand_scheduled_task = $script:PostgresTaskName; observed_at = [DateTimeOffset]::UtcNow.ToString('O') } | ConvertTo-Json -Compress
