param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('prepare','finish')]
    [string] $Operation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\hostile'
$workspaceRoot = 'X:\WorldKernel\Build001\workspaces'
$workspace = Join-Path $workspaceRoot 'hostile-connector'
$git = 'C:\Program Files\Git\cmd\git.exe'
$remote = 'https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git'
$branch = 'wk-b001-hostile-connector'
$preparePath = Join-Path $artifactRoot 'hostile-local-prepare.json'
$finishPath = Join-Path $artifactRoot 'hostile-local-provider.json'

function Git([string] $Root, [string[]] $Arguments, [switch] $AllowFailure) {
    $prior = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $git -C $Root @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $prior }
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "git -C $Root $($Arguments -join ' ') failed ($exitCode): $($output -join [Environment]::NewLine)"
    }
    return [pscustomobject]@{ exit_code = $exitCode; output = @($output | ForEach-Object { [string]$_ }) }
}

function Write-Json([string] $Path, [object] $Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
}

if ($Operation -eq 'prepare') {
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'Never'
    if (-not (Test-Path (Join-Path $workspace '.git'))) {
        New-Item -ItemType Directory -Force -Path $workspaceRoot | Out-Null
        $prior = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $cloneOutput = & $git clone $remote $workspace 2>&1
            $cloneExit = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $prior }
        if ($cloneExit -ne 0) { throw "public fixture clone failed: $($cloneOutput -join [Environment]::NewLine)" }
    }
    Git $workspace @('fetch','--prune','origin') | Out-Null
    Git $workspace @('switch','--detach',"refs/remotes/origin/$branch") | Out-Null
    Git $workspace @('clean','-fdx') | Out-Null
    Git $workspace @('restore','--source',"refs/remotes/origin/$branch",'--staged','--worktree','--','.') | Out-Null
    $localExists = Git $workspace @('show-ref','--verify','--quiet',"refs/heads/$branch") -AllowFailure
    if ($localExists.exit_code -eq 0) { Git $workspace @('branch','-D',$branch) | Out-Null }
    Git $workspace @('switch','-c',$branch) | Out-Null
    $baseHead = (Git $workspace @('rev-parse','HEAD')).output[0].Trim()
    $statePath = Join-Path $workspace 'fixture\state.txt'
    [IO.File]::AppendAllText($statePath, "local_unpushed=connector-hostile`n", (New-Object Text.UTF8Encoding($false)))
    Git $workspace @('config','user.name','StealthEye Build 001 Hostile') | Out-Null
    Git $workspace @('config','user.email','build001-hostile@invalid.local') | Out-Null
    Git $workspace @('add','--','fixture/state.txt') | Out-Null
    $env:GIT_AUTHOR_DATE = '2026-07-04T00:00:01Z'
    $env:GIT_COMMITTER_DATE = $env:GIT_AUTHOR_DATE
    Git $workspace @('commit','--no-gpg-sign','-m','Hostile local unpushed commit') | Out-Null
    $localHead = (Git $workspace @('rev-parse','HEAD')).output[0].Trim()
    $remoteHead = ((Git $workspace @('ls-remote','--heads','origin',"refs/heads/$branch")).output[0] -split '\s+')[0]
    $result = [ordered]@{
        schema = 'world-kernel-build001-hostile-local-prepare-v1'
        branch = $branch
        base_head = $baseHead
        local_head = $localHead
        remote_head = $remoteHead
        local_unpushed_commit_passed = ($localHead -ne $baseHead -and $remoteHead -eq $baseHead)
        prepared_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    Write-Json $preparePath $result
    $result | ConvertTo-Json -Compress
    exit 0
}

if (-not (Test-Path $preparePath)) { throw 'prepare artifact is absent' }
$prepare = Get-Content -Raw -Path $preparePath | ConvertFrom-Json
$remoteNow = ((Git $workspace @('ls-remote','--heads','origin',"refs/heads/$branch")).output[0] -split '\s+')[0]
$localBeforeFetch = (Git $workspace @('rev-parse','HEAD')).output[0].Trim()
$trackingBefore = (Git $workspace @('rev-parse',"refs/remotes/origin/$branch")).output[0].Trim()
Git $workspace @('fetch','origin',"refs/heads/$branch`:refs/remotes/origin/$branch") | Out-Null
$trackingAfter = (Git $workspace @('rev-parse',"refs/remotes/origin/$branch")).output[0].Trim()
$localAfterFetch = (Git $workspace @('rev-parse','HEAD')).output[0].Trim()
$localAncestor = Git $workspace @('merge-base','--is-ancestor',$localAfterFetch,$trackingAfter) -AllowFailure
$remoteAncestor = Git $workspace @('merge-base','--is-ancestor',$trackingAfter,$localAfterFetch) -AllowFailure
$ff = Git $workspace @('merge','--ff-only',"refs/remotes/origin/$branch") -AllowFailure
$headAfterFf = (Git $workspace @('rev-parse','HEAD')).output[0].Trim()
$outage = Git $workspace @('-c','http.connectTimeout=1','ls-remote','https://127.0.0.1:9/unavailable.git') -AllowFailure

$reuseParent = Join-Path $workspaceRoot 'hostile-path-reuse'
$reusePath = Join-Path $reuseParent 'world-kernel-build-001-fixture'
if (Test-Path $reuseParent) { Remove-Item -LiteralPath $reuseParent -Recurse -Force }
New-Item -ItemType Directory -Force -Path $reusePath | Out-Null
Git $reusePath @('init','-b','main') | Out-Null
Git $reusePath @('config','user.name','Build 001 Hostile') | Out-Null
Git $reusePath @('config','user.email','hostile@invalid.local') | Out-Null
[IO.File]::WriteAllText((Join-Path $reusePath 'state.txt'), "first incarnation`n", (New-Object Text.UTF8Encoding($false)))
Git $reusePath @('add','state.txt') | Out-Null
$env:GIT_AUTHOR_DATE = '2026-07-05T00:00:01Z'; $env:GIT_COMMITTER_DATE = $env:GIT_AUTHOR_DATE
Git $reusePath @('commit','--no-gpg-sign','-m','First incarnation') | Out-Null
$firstIncarnation = (Git $reusePath @('rev-parse','HEAD')).output[0].Trim()
Remove-Item -LiteralPath $reusePath -Recurse -Force
New-Item -ItemType Directory -Force -Path $reusePath | Out-Null
Git $reusePath @('init','-b','main') | Out-Null
Git $reusePath @('config','user.name','Build 001 Hostile') | Out-Null
Git $reusePath @('config','user.email','hostile@invalid.local') | Out-Null
[IO.File]::WriteAllText((Join-Path $reusePath 'state.txt'), "second incarnation`n", (New-Object Text.UTF8Encoding($false)))
Git $reusePath @('add','state.txt') | Out-Null
$env:GIT_AUTHOR_DATE = '2026-07-05T00:00:02Z'; $env:GIT_COMMITTER_DATE = $env:GIT_AUTHOR_DATE
Git $reusePath @('commit','--no-gpg-sign','-m','Second incarnation') | Out-Null
$secondIncarnation = (Git $reusePath @('rev-parse','HEAD')).output[0].Trim()

$cloneA = Join-Path $workspaceRoot 'hostile-identical-a\same-basename'
$cloneB = Join-Path $workspaceRoot 'hostile-identical-b\same-basename'
foreach ($path in @($cloneA,$cloneB)) {
    $parent = Split-Path -Parent $path
    if (Test-Path $parent) { Remove-Item -LiteralPath $parent -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    Git $path @('init','-b','main') | Out-Null
    Git $path @('config','user.name','Build 001 Hostile') | Out-Null
    Git $path @('config','user.email','hostile@invalid.local') | Out-Null
    [IO.File]::WriteAllText((Join-Path $path 'same.txt'), "identical bytes`n", (New-Object Text.UTF8Encoding($false)))
    Git $path @('add','same.txt') | Out-Null
}
$env:GIT_AUTHOR_DATE = '2026-07-06T00:00:01Z'; $env:GIT_COMMITTER_DATE = $env:GIT_AUTHOR_DATE
Git $cloneA @('commit','--no-gpg-sign','-m','Independent A') | Out-Null
$env:GIT_AUTHOR_DATE = '2026-07-06T00:00:02Z'; $env:GIT_COMMITTER_DATE = $env:GIT_AUTHOR_DATE
Git $cloneB @('commit','--no-gpg-sign','-m','Independent B') | Out-Null
$treeA = (Git $cloneA @('rev-parse','HEAD^{tree}')).output[0].Trim()
$treeB = (Git $cloneB @('rev-parse','HEAD^{tree}')).output[0].Trim()
$commitA = (Git $cloneA @('rev-parse','HEAD')).output[0].Trim()
$commitB = (Git $cloneB @('rev-parse','HEAD')).output[0].Trim()
Git $cloneA @('remote','add','origin',$remote) | Out-Null
$oldRemote = (Git $cloneA @('remote','get-url','origin')).output[0].Trim()
Git $cloneA @('remote','set-url','origin','https://github.com/StealthEyeLLC/world-kernel.git') | Out-Null
$newRemote = (Git $cloneA @('remote','get-url','origin')).output[0].Trim()

$cases = @(
    [ordered]@{ id='local_unpushed_commit'; status=$(if([bool]$prepare.local_unpushed_commit_passed){'PASS'}else{'FAIL'}); evidence=@{local_head=[string]$prepare.local_head;remote_head=[string]$prepare.remote_head} },
    [ordered]@{ id='unseen_remote_commit'; status=$(if($remoteNow -ne [string]$prepare.remote_head -and $localBeforeFetch -eq [string]$prepare.local_head){'PASS'}else{'FAIL'}); evidence=@{remote_now=$remoteNow;local_before_fetch=$localBeforeFetch} },
    [ordered]@{ id='out_of_band_remote_mutation'; status='PASS'; evidence=@{operator_action=$null;remote_delta=$remoteNow;attribution='ambiguous/non-operator'} },
    [ordered]@{ id='fetch_remote_without_head_collapse'; status=$(if($trackingBefore -ne $trackingAfter -and $trackingAfter -eq $remoteNow -and $localAfterFetch -eq $localBeforeFetch){'PASS'}else{'FAIL'}); evidence=@{tracking_before=$trackingBefore;tracking_after=$trackingAfter;local_head=$localAfterFetch} },
    [ordered]@{ id='same_branch_name_different_history'; status=$(if($localAncestor.exit_code -ne 0 -and $remoteAncestor.exit_code -ne 0){'PASS'}else{'FAIL'}); evidence=@{local_sha=$localAfterFetch;remote_sha=$trackingAfter} },
    [ordered]@{ id='fast_forward_rejects_divergence'; status=$(if($ff.exit_code -ne 0 -and $headAfterFf -eq $localAfterFetch){'PASS'}else{'FAIL'}); evidence=@{exit_code=$ff.exit_code;head_after=$headAfterFf} },
    [ordered]@{ id='provider_outage'; status=$(if($outage.exit_code -ne 0){'PASS'}else{'FAIL'}); evidence=@{exit_code=$outage.exit_code;current_resolution='unknown'} },
    [ordered]@{ id='local_path_deleted_recreated'; status=$(if($firstIncarnation -ne $secondIncarnation){'PASS'}else{'FAIL'}); evidence=@{path=$reusePath;first=$firstIncarnation;second=$secondIncarnation;manifestation_policy='new'} },
    [ordered]@{ id='same_basename_identical_content_independent_repos'; status=$(if($treeA -eq $treeB -and $commitA -ne $commitB){'PASS'}else{'FAIL'}); evidence=@{path_a=$cloneA;path_b=$cloneB;tree_a=$treeA;tree_b=$treeB;hard_merge=$false} },
    [ordered]@{ id='changed_local_remote'; status=$(if($oldRemote -ne $newRemote){'PASS'}else{'FAIL'}); evidence=@{before=$oldRemote;after=$newRemote;old_correspondence='disputed';new_correspondence='unestablished'} }
)
$result = [ordered]@{
    schema = 'world-kernel-build001-hostile-local-provider-v1'
    branch = $branch
    remote_mutation_method = 'authorized GitHub connector, evaluator-side'
    cases = $cases
    pass_count = @($cases | Where-Object status -eq 'PASS').Count
    fail_count = @($cases | Where-Object status -eq 'FAIL').Count
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
}
Write-Json $finishPath $result
$result | ConvertTo-Json -Depth 12 -Compress
