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
$git = 'C:\Program Files\Git\cmd\git.exe'
$gh = 'C:\WorldKernel\Build001\runtime\gh-2.97.0\gh.exe'
if (-not (Test-Path $git)) { throw 'Pinned git.exe is unavailable.' }
if (-not (Test-Path $gh)) { throw 'Pinned portable GitHub CLI is unavailable.' }
if ($Branch -and $Branch -notmatch '^wk-b001-[a-z0-9][a-z0-9-]{0,62}$') {
    throw 'Branch is outside the disposable Build 001 namespace.'
}

function Get-ConfiguredGitHubToken {
    $credentialInput = "protocol=https`nhost=github.com`n`n"
    $credentialOutput = $credentialInput | & $git credential fill
    if ($LASTEXITCODE -ne 0) { throw 'Configured Git credential provider failed.' }
    $passwordLine = $credentialOutput | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
    if (-not $passwordLine) { throw 'Configured GitHub credential is unavailable.' }
    return $passwordLine.Substring('password='.Length)
}

$token = Get-ConfiguredGitHubToken
$env:GH_TOKEN = $token
$headers = @{
    Authorization = 'Bearer ' + $token
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'StealthEye-WorldKernel-Build001'
}

function Get-BranchProtection([string] $Name) {
    try {
        return Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$repository/branches/$Name/protection" -Headers $headers
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Remove-BranchProtection([string] $Name) {
    if (Get-BranchProtection $Name) {
        Invoke-RestMethod -Method Delete -Uri "https://api.github.com/repos/$repository/branches/$Name/protection" -Headers $headers | Out-Null
    }
}

function Set-RejectingBranchProtection([string] $Name) {
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
    } | ConvertTo-Json -Depth 8
    Invoke-RestMethod -Method Put -Uri "https://api.github.com/repos/$repository/branches/$Name/protection" -Headers $headers -Body $body -ContentType 'application/json' | Out-Null
}

function Get-ProviderConfiguration([string] $Name) {
    $protection = if ($Name) { Get-BranchProtection $Name } else { $null }
    $workflow = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$repository/actions/workflows/fixture-check.yml" -Headers $headers
    $secretRaw = & $gh secret list --repo $repository --json name
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect fixture secret metadata.' }
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

switch ($Operation) {
    'set-check-regime' {
        if ($CheckRegime -eq 'no_check') {
            & $gh workflow disable fixture-check.yml --repo $repository
            if ($LASTEXITCODE -ne 0) { throw 'Unable to disable fixture workflow.' }
        }
        else {
            & $gh workflow enable fixture-check.yml --repo $repository
            if ($LASTEXITCODE -ne 0) { throw 'Unable to enable fixture workflow.' }
            & $gh secret set WK_BUILD001_CHECK_MODE --repo $repository --body $CheckRegime
            if ($LASTEXITCODE -ne 0) { throw 'Unable to set encrypted fixture check regime.' }
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

[pscustomobject]@{
    operation = $Operation
    configuration = $configuration
    configuration_fingerprint = $fingerprint
    observed_at = [DateTimeOffset]::UtcNow.ToString('O')
} | ConvertTo-Json -Compress -Depth 6
