param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('set-check-regime','set-push-regime','inspect')]
    [string] $Operation,

    [string] $Branch,

    [ValidateSet('no_check','success','failure')]
    [string] $CheckRegime = 'success',

    [ValidateSet('accepted','rejected_by_provider_policy')]
    [string] $PushRegime = 'accepted'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = 'StealthEyeLLC/world-kernel-build-001-fixture'
$gh = 'C:\WorldKernel\Build001\runtime\gh-2.97.0\gh.exe'
if (-not (Test-Path $gh)) { throw 'Pinned portable GitHub CLI is unavailable.' }
if ($Branch -and $Branch -notmatch '^wk-b001-[a-z0-9][a-z0-9-]{0,62}$') {
    throw 'Branch is outside the disposable Build 001 namespace.'
}
if ($Branch -eq 'main') { throw 'Fixture provider administration may not target main.' }

function Invoke-GhLines([string[]] $Arguments, [string] $InputText = $null, [switch] $AllowFailure) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        if ($null -eq $InputText) {
            $lines = @(& $gh @Arguments 2>&1 | ForEach-Object { [string]$_ })
        }
        else {
            $lines = @($InputText | & $gh @Arguments 2>&1 | ForEach-Object { [string]$_ })
        }
        $exit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $saved }
    if ($exit -ne 0 -and -not $AllowFailure) {
        throw "GitHub CLI failed ($($Arguments -join ' ')): $($lines -join [Environment]::NewLine)"
    }
    return [pscustomobject]@{ exit_code = $exit; lines = $lines }
}

function Invoke-GhJson([string[]] $Arguments) {
    $result = Invoke-GhLines $Arguments
    $text = $result.lines -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

function Get-BranchProtection([string] $Name) {
    if (-not $Name) { return $null }
    $endpoint = "repos/$repository/branches/$([uri]::EscapeDataString($Name))/protection"
    $result = Invoke-GhLines @('api',$endpoint,'--method','GET') -AllowFailure
    if ($result.exit_code -eq 0) {
        return ($result.lines -join [Environment]::NewLine) | ConvertFrom-Json
    }
    if (($result.lines -join [Environment]::NewLine) -match 'HTTP 404|Not Found') { return $null }
    throw "Unable to inspect fixture branch protection: $($result.lines -join [Environment]::NewLine)"
}

function Remove-BranchProtection([string] $Name) {
    if ($Name -eq 'main') { throw 'Main branch protection is outside Campaign 2 fixture authority.' }
    if (Get-BranchProtection $Name) {
        $endpoint = "repos/$repository/branches/$([uri]::EscapeDataString($Name))/protection"
        Invoke-GhLines @('api',$endpoint,'--method','DELETE') | Out-Null
    }
}

function Set-RejectingBranchProtection([string] $Name) {
    if ($Name -eq 'main') { throw 'Main branch protection is outside Campaign 2 fixture authority.' }
    $body = [ordered]@{
        required_status_checks = $null
        enforce_admins = $true
        required_pull_request_reviews = [ordered]@{
            dismiss_stale_reviews = $false
            require_code_owner_reviews = $false
            required_approving_review_count = 1
            require_last_push_approval = $false
        }
        restrictions = $null
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $false
        lock_branch = $false
        allow_fork_syncing = $true
    } | ConvertTo-Json -Depth 8 -Compress
    $endpoint = "repos/$repository/branches/$([uri]::EscapeDataString($Name))/protection"
    Invoke-GhLines @('api',$endpoint,'--method','PUT','--input','-') $body | Out-Null
}

function Get-Workflow {
    return Invoke-GhJson @('api',"repos/$repository/actions/workflows/fixture-check.yml",'--method','GET')
}

function Get-ProviderConfiguration([string] $Name) {
    $protection = if ($Name) { Get-BranchProtection $Name } else { $null }
    $workflow = Get-Workflow
    $secretResult = Invoke-GhLines @('secret','list','--repo',$repository,'--json','name')
    $secretRaw = $secretResult.lines -join [Environment]::NewLine
    return [ordered]@{
        repository = $repository
        branch = $Name
        branch_protected = [bool]$protection
        required_approving_reviews = if ($protection -and $protection.required_pull_request_reviews) {
            [int]$protection.required_pull_request_reviews.required_approving_review_count
        } else { 0 }
        admins_enforced = [bool]($protection -and $protection.enforce_admins.enabled)
        force_pushes_allowed = [bool]($protection -and $protection.allow_force_pushes.enabled)
        deletions_allowed = [bool]($protection -and $protection.allow_deletions.enabled)
        workflow_state = [string]$workflow.state
        encrypted_check_secret_present = [bool]([string]$secretRaw -match 'WK_BUILD001_CHECK_MODE')
    }
}

function Get-GitHubToken {
    $git = 'C:\Program Files\Git\cmd\git.exe'
    $credentialInput = "protocol=https`nhost=github.com`n`n"
    $credentialOutput = @($credentialInput | & $git credential fill)
    if ($LASTEXITCODE -ne 0) { throw 'Git credential provider failed for fixture provider administration.' }
    $passwordLine = $credentialOutput | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
    if (-not $passwordLine) { throw 'GitHub credential is unavailable for fixture provider administration.' }
    return $passwordLine.Substring('password='.Length)
}

$oldGitHubToken = $env:GH_TOKEN
try {
    $env:GH_TOKEN = Get-GitHubToken
    switch ($Operation) {
        'set-check-regime' {
            $currentWorkflow = Get-Workflow
            $currentState = [string]$currentWorkflow.state
            if ($CheckRegime -eq 'no_check') {
                if ($currentState -eq 'active') {
                    Invoke-GhLines @('workflow','disable','fixture-check.yml','--repo',$repository) | Out-Null
                }
            }
            else {
                if ($currentState -ne 'active') {
                    Invoke-GhLines @('workflow','enable','fixture-check.yml','--repo',$repository) | Out-Null
                }
                Invoke-GhLines @('secret','set','WK_BUILD001_CHECK_MODE','--repo',$repository,'--body',$CheckRegime) | Out-Null
            }
        }
        'set-push-regime' {
            if (-not $Branch) { throw '-Branch is required for set-push-regime.' }
            if ($PushRegime -eq 'accepted') { Remove-BranchProtection $Branch }
            else { Set-RejectingBranchProtection $Branch }
        }
        'inspect' { }
    }

    $configuration = Get-ProviderConfiguration $Branch
    $bytes = [Text.Encoding]::UTF8.GetBytes(($configuration | ConvertTo-Json -Compress))
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $fingerprint = ([BitConverter]::ToString($hasher.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    $result = [pscustomobject]@{
        operation = $Operation
        configuration = $configuration
        configuration_fingerprint = $fingerprint
        observed_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
}
finally {
    $env:GH_TOKEN = $oldGitHubToken
    Remove-Variable oldGitHubToken -ErrorAction SilentlyContinue
}
$result | ConvertTo-Json -Compress -Depth 6