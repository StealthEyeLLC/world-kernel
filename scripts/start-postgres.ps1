. (Join-Path $PSScriptRoot 'runtime-common.ps1')

if (-not (Test-Path (Join-Path $script:PostgresBin 'pg_ctl.exe'))) { throw 'PostgreSQL runtime is not provisioned.' }
if (-not (Test-Path (Join-Path $script:PostgresData 'PG_VERSION'))) { throw 'PostgreSQL data directory is not initialized.' }

$pidFile = Join-Path $script:PostgresData 'postmaster.pid'
function Get-PostmasterPidSnapshot {
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if (-not (Test-Path $pidFile)) { return }
        try { return [IO.File]::ReadAllLines($pidFile) }
        catch [IO.IOException] { Start-Sleep -Milliseconds 100 }
    }
}

$initialPidLines = @(Get-PostmasterPidSnapshot)
if ($initialPidLines.Count -gt 0) {
    $recordedPid = [int]$initialPidLines[0]
    $recordedProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$recordedPid" -ErrorAction SilentlyContinue
    # Windows can redact ExecutablePath for a process owned by the limited
    # interactive principal even when this launcher is inspecting as SYSTEM.
    # The PID came from this experiment-owned data directory, so process name
    # is the stable cross-principal check here.
    $isExpectedProcess = $recordedProcess -and $recordedProcess.Name -eq 'postgres.exe'
    if (-not $isExpectedProcess) {
        $staleRoot = Join-Path $script:RuntimeRoot 'stale-runtime-records'
        New-Item -ItemType Directory -Force -Path $staleRoot | Out-Null
        $stalePath = Join-Path $staleRoot ("postmaster-{0}-{1}.pid" -f $recordedPid, [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
        Move-Item -LiteralPath $pidFile -Destination $stalePath
    }
}

function Get-LivePostgresListener {
    $pidLines = @(Get-PostmasterPidSnapshot)
    if ($pidLines.Count -lt 4) { return $null }
    $expectedPid = [int]$pidLines[0]
    $recordedDataRoot = [IO.Path]::GetFullPath($pidLines[1].Trim())
    $expectedDataRoot = [IO.Path]::GetFullPath($script:PostgresData)
    $recordedPort = [int]$pidLines[3]
    if ($recordedDataRoot -ne $expectedDataRoot -or $recordedPort -ne $script:PostgresPort) { return $null }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$expectedPid" -ErrorAction SilentlyContinue
    if (-not $process -or $process.Name -ne 'postgres.exe') { return $null }

    # Get-NetTCPConnection retained stale owner rows after a killed experimental
    # postmaster on this Windows build. A live protocol probe is the provider
    # truth and is also a stronger readiness check than a socket-table row.
    $readyOutput = & (Join-Path $script:PostgresBin 'pg_isready.exe') '-h' '127.0.0.1' '-p' ([string]$script:PostgresPort) '-t' '1' 2>&1
    if ($LASTEXITCODE -eq 0) { return $readyOutput }
    return $null
}

$listening = Get-LivePostgresListener
if (-not $listening) {
    $task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
    if ($task -and $task.State -eq 'Running') {
        # A just-stopped postmaster can leave its task wrapper in Running for a
        # short interval. Wait for either genuine readiness or task settlement
        # before issuing the next trigger.
        $settleDeadline = [DateTimeOffset]::UtcNow.AddSeconds(10)
        do {
            Start-Sleep -Milliseconds 200
            $listening = Get-LivePostgresListener
            if ($listening) { break }
            $task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
        } while ($task -and $task.State -eq 'Running' -and [DateTimeOffset]::UtcNow -lt $settleDeadline)
        if (-not $listening -and $task -and $task.State -eq 'Running') {
            Stop-ScheduledTask -TaskName $script:PostgresTaskName
            $forceSettleDeadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
            do {
                Start-Sleep -Milliseconds 100
                $task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
            } while ($task -and $task.State -eq 'Running' -and [DateTimeOffset]::UtcNow -lt $forceSettleDeadline)
        }
    }
    if ($listening) {
        $task = Get-ScheduledTask -TaskName $script:PostgresTaskName -ErrorAction SilentlyContinue
    }
    elseif (-not $task -or $task.State -ne 'Running') {
        $launcherRoot = Join-Path $script:RuntimeRoot 'launcher'
        New-Item -ItemType Directory -Force -Path $launcherRoot | Out-Null
        $launcherPath = Join-Path $launcherRoot 'run-postgres.ps1'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'run-postgres.ps1') -Destination $launcherPath -Force
        $taskLogPath = Join-Path $script:RuntimeRoot 'postgresql-task.log'
        if (-not (Test-Path $taskLogPath)) { New-Item -ItemType File -Path $taskLogPath | Out-Null }
        & icacls.exe $script:PostgresData '/grant:r' 'STEALTHEYELLC\StealthEye:(OI)(CI)(F)' | Out-Null
        & icacls.exe $launcherRoot '/grant:r' 'STEALTHEYELLC\StealthEye:(OI)(CI)(RX)' | Out-Null
        & icacls.exe $taskLogPath '/grant:r' 'STEALTHEYELLC\StealthEye:(M)' | Out-Null
        & icacls.exe $script:PostgresData '/grant:r' 'NT AUTHORITY\LOCAL SERVICE:(OI)(CI)(F)' | Out-Null
        & icacls.exe $launcherRoot '/grant:r' 'NT AUTHORITY\LOCAL SERVICE:(OI)(CI)(RX)' | Out-Null
        & icacls.exe $taskLogPath '/grant:r' 'NT AUTHORITY\LOCAL SERVICE:(M)' | Out-Null
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$launcherPath`" -PostgresExecutable `"$(Join-Path $script:PostgresBin 'postgres.exe')`" -DataRoot `"$script:PostgresData`" -Port $script:PostgresPort -LogPath `"$(Join-Path $script:RuntimeRoot 'postgresql-task.log')`"" -WorkingDirectory $launcherRoot
        $principal = New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\LOCAL SERVICE' -LogonType ServiceAccount -RunLevel Limited
        $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable
        Register-ScheduledTask -TaskName $script:PostgresTaskName -Action $action -Principal $principal -Settings $settings -Description 'Disposable on-demand PostgreSQL 18.4 process for StealthEye World Kernel Build 001; not a Windows service.' -Force | Out-Null
    }
    if (-not $listening) { Start-ScheduledTask -TaskName $script:PostgresTaskName }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $listening = Get-LivePostgresListener
    } while (-not $listening -and [DateTimeOffset]::UtcNow -lt $deadline)
    if (-not $listening) {
        $taskLog = Join-Path $script:RuntimeRoot 'postgresql-task.log'
        $tail = if (Test-Path $taskLog) { (Get-Content $taskLog -Tail 20) -join [Environment]::NewLine } else { 'no task log' }
        throw "PostgreSQL scheduled task did not become ready: $tail"
    }
}

$pidValue = if (Test-Path (Join-Path $script:PostgresData 'postmaster.pid')) { (Get-Content (Join-Path $script:PostgresData 'postmaster.pid') -First 1).Trim() } else { $null }
[pscustomobject]@{
    running = $true
    pid = $pidValue
    port = $script:PostgresPort
    data_root = $script:PostgresData
    windows_scm_service_registered = $false
    on_demand_scheduled_task = $script:PostgresTaskName
    observed_at = [DateTimeOffset]::UtcNow.ToString('O')
} | ConvertTo-Json -Compress
