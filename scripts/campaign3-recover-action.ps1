param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('git:create_local_commit','git:create_branch','git:push_ref','github:create_remote_commit','git:fetch_remote','git:integrate_fast_forward')]
    [string]$SemanticAction,
    [Parameter(Mandatory=$true)][string]$WorkingCopy,
    [Parameter(Mandatory=$true)][string]$PreObservationPath,
    [Parameter(Mandatory=$true)][string]$PreparePath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [Parameter(Mandatory=$true)][string]$Dotnet,
    [Parameter(Mandatory=$true)][string]$CliDll,
    [Parameter(Mandatory=$true)][string]$FixtureRoot,
    [Parameter(Mandatory=$true)][string]$Node,
    [Parameter(Mandatory=$true)][string]$Sdk
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-3')).TrimEnd('\')+'\'
$output=[IO.Path]::GetFullPath($OutputPath)
$preFile=[IO.Path]::GetFullPath($PreObservationPath)
$prepareFile=[IO.Path]::GetFullPath($PreparePath)
$workspace=[IO.Path]::GetFullPath($WorkingCopy)
$git='C:\Program Files\Git\cmd\git.exe'
$gh='C:\WorldKernel\Build001\runtime\gh-2.97.0\gh.exe'
foreach($candidate in @($output,$preFile,$prepareFile)){
    if(-not $candidate.StartsWith($campaignRoot,[StringComparison]::OrdinalIgnoreCase)){throw "Recovery artifact escapes Campaign 3: $candidate"}
}
if(Test-Path $output){throw "Recovery output already exists: $output"}
if(-not(Test-Path (Join-Path $workspace '.git'))){throw 'Recovery working copy is not a Git repository.'}
$pre=Get-Content -Raw -LiteralPath $preFile|ConvertFrom-Json
$prepare=Get-Content -Raw -LiteralPath $prepareFile|ConvertFrom-Json
if([string]$prepare.semantic_action -ne $SemanticAction){throw 'Recovery preparation action mismatch.'}
$params=$prepare.scheduled_parameters
$branch=[string]$prepare.action_branch

function Git([string[]]$Arguments,[switch]$AllowFailure){
    $saved=$ErrorActionPreference
    try{$ErrorActionPreference='Continue';$lines=@(& $git -C $workspace @Arguments 2>&1|ForEach-Object{[string]$_});$exit=$LASTEXITCODE}
    finally{$ErrorActionPreference=$saved}
    if($exit-ne0 -and -not $AllowFailure){throw "git $($Arguments -join ' ') exited $exit`: $($lines -join [Environment]::NewLine)"}
    [pscustomobject]@{arguments=$Arguments;exit_code=$exit;output=$lines}
}
function First([object]$Result){if(@($Result.output).Count){return ([string]$Result.output[0]).Trim()};return $null}
function RemoteHead([string]$Name){$r=Git @('ls-remote','--heads','origin',"refs/heads/$Name");if(@($r.output).Count-eq0){return $null};return (([string]$r.output[0])-split '\s+')[0]}
function Invoke-ExternalJson([string]$Executable,[string[]]$Arguments){
    $saved=$ErrorActionPreference
    try{$ErrorActionPreference='Continue';$lines=@(& $Executable @Arguments 2>&1|ForEach-Object{[string]$_});$exit=$LASTEXITCODE}
    finally{$ErrorActionPreference=$saved}
    if($exit-ne0){throw "$Executable exited $exit`: $($lines -join [Environment]::NewLine)"}
    $json=$lines|Where-Object{$_.Trim().StartsWith('{')}|Select-Object -Last 1
    if(-not $json){throw "$Executable returned no JSON object."}
    $json|ConvertFrom-Json
}
function Get-GitHubToken {
    $credentialInput="protocol=https`nhost=github.com`n`n"
    $credentialOutput=@($credentialInput | & $git credential fill)
    if($LASTEXITCODE-ne0){throw 'Git credential provider failed during hosted-commit recovery.'}
    $passwordLine=$credentialOutput|Where-Object{$_ -like 'password=*'}|Select-Object -First 1
    if(-not $passwordLine){throw 'GitHub credential unavailable during hosted-commit recovery.'}
    try{return $passwordLine.Substring('password='.Length)}
    finally{Remove-Variable credentialOutput,passwordLine -ErrorAction SilentlyContinue}
}

function Run-NativeAction {
    $args=@($CliDll,'git-action','--semantic-action',$SemanticAction,'--git-executable',$git,'--fixture-root',$FixtureRoot,'--working-copy',$workspace)
    switch($SemanticAction){
        'git:create_local_commit'{$args+=@('--relative-path',[string]$params.relative_path,'--message',[string]$params.message,'--timestamp',[string]$params.timestamp)}
        'git:create_branch'{$args+=@('--branch',[string]$params.branch)}
        'git:push_ref'{$args+=@('--branch',[string]$params.branch)}
        'git:integrate_fast_forward'{$args+=@('--branch',[string]$params.branch)}
    }
    Invoke-ExternalJson $Dotnet $args
}
function Run-RemoteCommit {
    $n=Invoke-ExternalJson $Node @((Join-Path $PSScriptRoot 'eyebrowse-github-remote-commit.mjs'),$Sdk,[string]$params.branch,[string]$params.file,[string]$params.text,[string]$params.message)
    [pscustomobject]@{ok=$true;semantic_action='github:create_remote_commit';receipt_accepted=$true;exit_code=0;started_at=$n.started_at;completed_at=$n.completed_at;receipt=$n}
}
function Write-Receipt([bool]$Accepted,[string]$Reason,[object]$Evidence){
    $now=[DateTimeOffset]::UtcNow.ToString('O')
    $value=[ordered]@{
        ok=$true;semantic_action=$SemanticAction;receipt_accepted=$Accepted;exit_code=0
        started_at=$now;completed_at=$now;receipt=[ordered]@{
            receipt_kind='provider_state_recovery';recovered_without_original_receipt=$true;reason=$Reason;evidence=$Evidence
        }
    }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $output))|Out-Null
    [IO.File]::WriteAllText($output,($value|ConvertTo-Json -Depth 20),(New-Object Text.UTF8Encoding($false)))
    $value
}

$currentHead=First (Git @('rev-parse','HEAD'))
$currentBranch=First (Git @('branch','--show-current'))
$currentRemote=RemoteHead $branch
$preHead=[string]$pre.local_head
$preBranch=[string]$pre.current_branch
$preRemote=if($null-ne$pre.remote_head){[string]$pre.remote_head}else{$null}
$evidence=[ordered]@{pre_local_head=$preHead;current_local_head=$currentHead;pre_current_branch=$preBranch;current_branch=$currentBranch;pre_remote_head=$preRemote;current_remote_head=$currentRemote;action_branch=$branch}
$execute=$false

switch($SemanticAction){
    'git:create_local_commit' {
        if($currentHead-eq$preHead -and $currentBranch-eq$preBranch -and $currentRemote-eq$preRemote){$execute=$true}
        else{
            $subject=First (Git @('show','-s','--format=%s','HEAD'))
            $parent=First (Git @('rev-parse','HEAD^') -AllowFailure)
            $evidence.commit_subject=$subject;$evidence.parent=$parent
            if($parent-eq$preHead -and $subject-eq[string]$params.message -and $currentRemote-eq$preRemote -and $currentBranch-eq$preBranch){Write-Receipt $true 'intended local commit already present' $evidence|Out-Null;return}
            throw 'Ambiguous recovery state for local commit; refusing duplicate dispatch.'
        }
    }
    'git:create_branch' {
        $target=[string]$params.branch
        $exists=(Git @('show-ref','--verify','--quiet',"refs/heads/$target") -AllowFailure).exit_code-eq0
        $evidence.target_branch_exists=$exists
        if($currentBranch-eq$target -and $currentHead-eq$preHead -and $currentRemote-eq$preRemote){Write-Receipt $true 'intended local branch already present and checked out' $evidence|Out-Null;return}
        if($currentBranch-eq$preBranch -and $currentHead-eq$preHead -and -not $exists -and $currentRemote-eq$preRemote){$execute=$true}else{throw 'Ambiguous recovery state for local branch creation; refusing duplicate dispatch.'}
    }
    'git:push_ref' {
        if($currentHead-ne$preHead){throw 'Local HEAD changed around push recovery; refusing ambiguous dispatch.'}
        if($currentRemote-eq$currentHead){Write-Receipt $true 'intended remote ref already equals sealed local HEAD' $evidence|Out-Null;return}
        if($currentRemote-eq$preRemote){$execute=$true}else{throw 'Remote ref changed unexpectedly around push recovery.'}
    }
    'github:create_remote_commit' {
        if($currentHead-ne$preHead){throw 'Local HEAD changed around hosted-commit recovery.'}
        if($currentRemote-eq$preRemote){$execute=$true}
        else{
            if(-not(Test-Path $gh)){throw 'Pinned GitHub CLI unavailable during hosted-commit recovery.'}
            $oldGitHubToken=$env:GH_TOKEN
            try{
                $env:GH_TOKEN=Get-GitHubToken
                $encodedPath=((([string]$params.file -split '/')|ForEach-Object{[uri]::EscapeDataString($_)})-join'/')
                $content=Invoke-ExternalJson $gh @('api',("repos/StealthEyeLLC/world-kernel-build-001-fixture/contents/{0}"-f$encodedPath),'--method','GET','-f',("ref={0}"-f[string]$params.branch))
                $actualText=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(([string]$content.content-replace'\s','')))
                $commit=Invoke-ExternalJson $gh @('api',("repos/StealthEyeLLC/world-kernel-build-001-fixture/commits/{0}"-f$currentRemote),'--method','GET')
            }
            finally{
                $env:GH_TOKEN=$oldGitHubToken
                Remove-Variable oldGitHubToken -ErrorAction SilentlyContinue
            }
            $evidence.remote_file_matches=($actualText-ceq[string]$params.text)
            $evidence.remote_commit_message=[string]$commit.commit.message
            if($evidence.remote_file_matches -and [string]$commit.commit.message-eq[string]$params.message){Write-Receipt $true 'intended hosted commit already present' $evidence|Out-Null;return}
            throw 'Remote branch changed ambiguously around hosted-commit recovery.'
        }
    }
    'git:fetch_remote' {$execute=$true}
    'git:integrate_fast_forward' {
        if($currentRemote-ne$preRemote){throw 'Remote target changed around ff-only recovery.'}
        if($currentHead-eq$currentRemote -and $preHead-ne$preRemote){Write-Receipt $true 'intended fast-forward already present' $evidence|Out-Null;return}
        if($currentHead-eq$preHead){$execute=$true}else{throw 'Local HEAD changed ambiguously around ff-only recovery.'}
    }
}
if(-not $execute){throw 'Recovery reached no safe execution state.'}
$result=if($SemanticAction-eq'github:create_remote_commit'){Run-RemoteCommit}else{Run-NativeAction}
[IO.Directory]::CreateDirectory((Split-Path -Parent $output))|Out-Null
[IO.File]::WriteAllText($output,($result|ConvertTo-Json -Depth 30),(New-Object Text.UTF8Encoding($false)))
$result|ConvertTo-Json -Compress -Depth 30
