$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\hostile'
$coordinationRoot = 'X:\WorldKernel\Build001\workspaces\stale-page-coordination'
$statusPath = Join-Path $artifactRoot 'eyebrowse-stale-page-status.json'
$outputPath = Join-Path $artifactRoot 'eyebrowse-stale-page.json'
$node = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe'
$sdk = 'X:\AgentBrowser\repo\program-host\sdk\eyebrowse.mjs'
$branch = 'wk-b001-hostile-connector'
New-Item -ItemType Directory -Force -Path $artifactRoot, $coordinationRoot | Out-Null

$status = [ordered]@{ state='running'; started_at=[DateTimeOffset]::UtcNow.ToString('O'); completed_at=$null; exit_code=$null; error=$null; node_output=$null }
[IO.File]::WriteAllText($statusPath, ($status | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
try {
    $prior = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $nodeOutput = & $node (Join-Path $PSScriptRoot 'eyebrowse-stale-page-probe.mjs') $sdk $branch $coordinationRoot $outputPath 2>&1
        $nodeExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $prior }
    $status.node_output = @($nodeOutput | ForEach-Object { [string]$_ })
    if ($nodeExit -ne 0) { throw "eyeBROWSE stale-page probe exited $nodeExit" }
    $status.state = 'completed'
    $status.exit_code = 0
}
catch {
    $status.state = 'failed'
    $status.exit_code = 1
    $status.error = $_.Exception.ToString()
}
finally {
    $status.completed_at = [DateTimeOffset]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($statusPath, ($status | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
}
exit $status.exit_code
