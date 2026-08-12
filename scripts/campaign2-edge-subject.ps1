param(
    [Parameter(Mandatory = $true)]
    [string] $RequestPath,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Campaign2User32
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr handle, int command);
}
'@

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$campaignRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'experiments\build001\campaign-2r')).TrimEnd('\') + '\'
$requestFile = [IO.Path]::GetFullPath($RequestPath)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
foreach ($candidate in @($requestFile, $outputFile)) {
    if (-not $candidate.StartsWith($campaignRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Campaign 2 subject path is outside the experiment namespace: $candidate"
    }
}
if (-not (Test-Path $requestFile -PathType Leaf)) { throw "Subject request is absent: $requestFile" }
if (Test-Path $outputFile) { throw "Subject output already exists and will not be overwritten: $outputFile" }

function Get-Utf8Sha256([string] $Text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)) }
    finally { $algorithm.Dispose() }
    return -join ($hash | ForEach-Object { $_.ToString('x2') })
}

function Get-NormalizedSha256([string] $Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    return Get-Utf8Sha256 $text
}

function Write-AtomicJson([object] $Value) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputFile)) | Out-Null
    $temporary = "$outputFile.$PID.tmp"
    $json = $Value | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText($temporary, "$json`n", (New-Object Text.UTF8Encoding($false)))
    [IO.File]::Move($temporary, $outputFile)
}

function Wait-Until([scriptblock] $Condition, [int] $TimeoutMs, [string] $Description) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        try {
            $value = & $Condition
            if ($value) { return $value }
        }
        catch [Windows.Automation.ElementNotAvailableException] { }
        Start-Sleep -Milliseconds 200
    } while ($watch.ElapsedMilliseconds -lt $TimeoutMs)
    throw "Timed out waiting for $Description after $TimeoutMs ms."
}

function Get-EdgeWindow {
    $root = [Windows.Automation.AutomationElement]::RootElement
    $windows = $root.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.Condition]::TrueCondition)
    foreach ($window in $windows) {
        if ($window.Current.Name -like '*Microsoft* Edge' -and $window.Current.Name -like '*ChatGPT*') { return $window }
    }
    foreach ($window in $windows) {
        if ($window.Current.Name -like '*Microsoft* Edge') { return $window }
    }
    return $null
}

function Get-Elements([Windows.Automation.AutomationElement] $Parent = $script:EdgeWindow) {
    return $Parent.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
}

function Find-Element(
    [string] $Name,
    [Windows.Automation.ControlType] $ControlType,
    [string] $AutomationId,
    [int] $TimeoutMs = 0
) {
    $finder = {
        $elements = Get-Elements
        foreach ($element in $elements) {
            if ($Name -and $element.Current.Name -ne $Name) { continue }
            if ($ControlType -and $element.Current.ControlType -ne $ControlType) { continue }
            if ($AutomationId -and $element.Current.AutomationId -ne $AutomationId) { continue }
            return $element
        }
        return $null
    }
    if ($TimeoutMs -gt 0) { return Wait-Until $finder $TimeoutMs "automation element '$Name$AutomationId'" }
    return & $finder
}

function Find-ElementsByName([string] $Pattern) {
    $foundElements = @()
    foreach ($element in (Get-Elements)) {
        if ($element.Current.Name -match $Pattern) { $foundElements += $element }
    }
    return $foundElements
}

function Invoke-Element([Windows.Automation.AutomationElement] $Element) {
    $pattern = $Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    $script:OperationCount += 1
}

function Set-Expanded([Windows.Automation.AutomationElement] $Element, [bool] $Expanded) {
    $pattern = $Element.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($Expanded -and $pattern.Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Expanded) {
        $pattern.Expand()
        $script:OperationCount += 1
    }
    elseif (-not $Expanded -and $pattern.Current.ExpandCollapseState -eq [Windows.Automation.ExpandCollapseState]::Expanded) {
        $pattern.Collapse()
        $script:OperationCount += 1
    }
}

function Get-Value([Windows.Automation.AutomationElement] $Element) {
    try { return ($Element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value }
    catch { return $null }
}

function Get-PageDocument {
    return Find-Element '' ([Windows.Automation.ControlType]::Document) 'RootWebArea' 30000
}

function Get-PageText {
    $document = Get-PageDocument
    $text = $document.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern)
    return $text.DocumentRange.GetText(-1)
}

function Get-LastSubjectJson([string] $Text) {
    $needle = '"action_class"'
    $position = $Text.LastIndexOf($needle, [StringComparison]::Ordinal)
    while ($position -ge 0) {
        $start = $Text.LastIndexOf('{', $position)
        if ($start -lt 0) { return $null }
        $depth = 0
        $inString = $false
        $escaped = $false
        for ($index = $start; $index -lt $Text.Length; $index += 1) {
            $character = $Text[$index]
            if ($inString) {
                if ($escaped) { $escaped = $false; continue }
                if ($character -eq '\') { $escaped = $true; continue }
                if ($character -eq '"') { $inString = $false }
                continue
            }
            if ($character -eq '"') { $inString = $true; continue }
            if ($character -eq '{') { $depth += 1; continue }
            if ($character -eq '}') {
                $depth -= 1
                if ($depth -eq 0) {
                    $raw = $Text.Substring($start, $index - $start + 1)
                    try {
                        $value = $raw | ConvertFrom-Json
                        if ($null -ne $value.action_class -and $null -ne $value.prediction) {
                            return [pscustomobject]@{ raw = $raw; value = $value }
                        }
                    }
                    catch { }
                    break
                }
            }
        }
        if ($position -eq 0) { break }
        $position = $Text.LastIndexOf($needle, $position - 1, [StringComparison]::Ordinal)
    }
    return $null
}

function Test-SubjectOutput([object] $Value, [object] $Request) {
    $required = @('action_class', 'target', 'parameters', 'prediction', 'requested_observations', 'material_action')
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    if (($actual -join "`n") -ne (($required | Sort-Object) -join "`n")) {
        throw 'Subject output keys differ from the locked contract.'
    }
    if ($Value.action_class -ne $Request.semantic_action) { throw 'Subject changed the locked semantic action.' }
    if ($Value.target -ne $Request.target) { throw 'Subject changed the locked target.' }
    if ($Value.parameters -isnot [pscustomobject]) { throw 'Subject parameters are not an object.' }
    if ($Value.requested_observations -isnot [array]) { throw 'Subject requested_observations is not an array.' }
    if ($Value.material_action -isnot [string]) { throw 'Subject material_action is not a string.' }
    $expectedPropositions = @($Request.propositions | Sort-Object)
    $actualPropositions = @($Value.prediction.PSObject.Properties.Name | Sort-Object)
    if (($expectedPropositions -join "`n") -ne ($actualPropositions -join "`n")) {
        throw 'Prediction proposition set differs from the locked vector.'
    }
    foreach ($proposition in $expectedPropositions) {
        $probability = $Value.prediction.PSObject.Properties[$proposition].Value
        if ($probability -isnot [double] -and $probability -isnot [decimal] -and $probability -isnot [int] -and $probability -isnot [long]) {
            throw "Probability for '$proposition' is not numeric."
        }
        if ([double]$probability -lt 0 -or [double]$probability -gt 1) {
            throw "Probability for '$proposition' is outside [0,1]."
        }
    }
}

function Get-SubjectState {
    $elements = Get-Elements
    $names = @($elements | ForEach-Object { $_.Current.Name })
    $document = Get-PageDocument
    $href = Get-Value $document
    $chat = $null
    $work = $null
    foreach ($element in $elements) {
        if ($element.Current.Name -eq 'Chat' -and $element.Current.ControlType -eq [Windows.Automation.ControlType]::RadioButton) {
            $chat = $element
        }
        if ($element.Current.Name -eq 'Work' -and $element.Current.ControlType -eq [Windows.Automation.ControlType]::RadioButton) {
            $work = $element
        }
    }
    $chatSelected = $href -match '^https://chatgpt\.com/\?temporary-chat=true(?:&|$)'
    if ($chat) {
        try {
            if (($chat.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected) {
                $chatSelected = $true
            }
        }
        catch { }
    }
    if ($work) {
        try {
            if (($work.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected) {
                $chatSelected = $false
            }
        }
        catch { }
    }
    $attachmentCount = @($names | Where-Object { $_ -match 'remove attachment|remove file|attached file' }).Count
    $messageMarkerCount = @($names | Where-Object { $_ -match '^(You said:|ChatGPT said:)$' }).Count
    return [ordered]@{
        href = $href
        signed_in = @($names | Where-Object { $_ -like '*open profile menu' }).Count -gt 0
        login_control_present = @($names | Where-Object { $_ -match '^(Log in|Sign up)$' }).Count -gt 0
        temporary_chat = @($names | Where-Object { $_ -match '^(Turn off temporary chat|Temporary chat)$' }).Count -gt 0 -and $href -match '[?&]temporary-chat=true(?:&|$)'
        chat_surface_selected = $chatSelected
        message_marker_count = $messageMarkerCount
        attachment_marker_count = $attachmentCount
        project_context_present = $href -match '/project/|/g/'
        file_library_context_present = $attachmentCount -gt 0
        temporary_markers = @($names | Where-Object { $_ -match 'temporary chat' } | Select-Object -Unique)
    }
}

function Get-ModelState {
    $button = Find-Element 'Extra High' ([Windows.Automation.ControlType]::Button) '' 10000
    Set-Expanded $button $true
    Start-Sleep -Milliseconds 250
    $advanced = Find-Element 'Show advanced options' ([Windows.Automation.ControlType]::MenuItem) ''
    if ($advanced) {
        Set-Expanded $advanced $true
        Start-Sleep -Milliseconds 250
    }
    $model = Find-Element 'Model GPT-5.6 Sol' ([Windows.Automation.ControlType]::MenuItem) '' 5000
    $effort = Find-Element 'Effort Extra High' ([Windows.Automation.ControlType]::MenuItem) '' 5000
    $result = [ordered]@{
        selected_model = if ($model) { '5.6 Sol' } else { $null }
        reasoning_selection = if ($effort) { 'Extra High' } else { $null }
        model_marker = if ($model) { $model.Current.Name } else { $null }
        effort_marker = if ($effort) { $effort.Current.Name } else { $null }
    }
    $button = Find-Element 'Extra High' ([Windows.Automation.ControlType]::Button) '' 5000
    Set-Expanded $button $false
    return $result
}

$requestBytes = [IO.File]::ReadAllBytes($requestFile)
$requestText = [Text.Encoding]::UTF8.GetString($requestBytes)
$request = $requestText | ConvertFrom-Json
if ($request.schema -ne 'world-kernel-build001-campaign2-subject-request-v1') { throw 'Invalid subject request schema.' }
if ($request.mode -notin @('inspect', 'invoke')) { throw 'Invalid subject request mode.' }
foreach ($hashName in @('base_prompt_sha256', 'tool_contract_sha256', 'arm_package_sha256', 'trial_output_contract_sha256', 'observable_configuration_fingerprint_sha256')) {
    if ($request.$hashName -notmatch '^[0-9a-f]{64}$') { throw "Request $hashName is not SHA-256." }
}
if ((Get-NormalizedSha256 $request.base_prompt_path) -ne $request.base_prompt_sha256) { throw 'Base prompt hash mismatch.' }
if ((Get-NormalizedSha256 $request.tool_contract_path) -ne $request.tool_contract_sha256) { throw 'Tool contract hash mismatch.' }
if ((Get-NormalizedSha256 $request.arm_package_path) -ne $request.arm_package_sha256) { throw 'Arm package hash mismatch.' }
if ($request.expected_model -ne '5.6 Sol' -or $request.expected_reasoning -ne 'Extra High') { throw 'Unlocked product selection.' }
if ($request.arm -eq 'structured' -and [int]$request.extra_treatment_model_calls -ne 0) { throw 'Structured extra model call prohibited.' }

$script:OperationCount = 0
$script:EdgeWindow = $null
$openedTab = $false
$startedAt = [DateTimeOffset]::UtcNow.ToString('O')
$exitCode = 5

try {
    $edgeProcess = Get-Process msedge | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $edgeProcess) { throw 'Signed-in Microsoft Edge window is not open.' }
    [Campaign2User32]::ShowWindowAsync($edgeProcess.MainWindowHandle, 3) | Out-Null
    [Campaign2User32]::SetForegroundWindow($edgeProcess.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 300
    $script:EdgeWindow = Get-EdgeWindow
    if (-not $script:EdgeWindow) { throw 'Microsoft Edge automation window is unavailable.' }

    $newTab = Find-Element 'New Tab' ([Windows.Automation.ControlType]::Button) 'view_28' 5000
    Invoke-Element $newTab
    $openedTab = $true
    Start-Sleep -Milliseconds 300
    $address = Find-Element 'Address and search bar' ([Windows.Automation.ControlType]::Edit) 'view_1021' 10000
    $addressValue = $address.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
    $addressValue.SetValue('https://chatgpt.com/?temporary-chat=true')
    $address.SetFocus()
    [Windows.Forms.SendKeys]::SendWait('{ENTER}')
    $script:OperationCount += 2

    Wait-Until {
        $document = Get-PageDocument
        $href = Get-Value $document
        if ($href -match '^https://chatgpt\.com/.*temporary-chat=true') { return $document }
        return $null
    } 30000 'ChatGPT Temporary Chat navigation' | Out-Null
    Find-Element 'Chat with ChatGPT' ([Windows.Automation.ControlType]::Edit) 'prompt-textarea' 30000 | Out-Null

    Start-Sleep -Milliseconds 400
    $continue = Find-Element 'Continue' ([Windows.Automation.ControlType]::Button) ''
    if ($continue) {
        Invoke-Element $continue
        Start-Sleep -Milliseconds 600
    }
    Find-Element 'Turn off temporary chat' ([Windows.Automation.ControlType]::Button) '' 15000 | Out-Null

    $before = Get-SubjectState
    $modelBefore = Get-ModelState
    $invalidationReasons = @()
    if (-not $before.signed_in -or $before.login_control_present) { $invalidationReasons += 'chatgpt_not_signed_in' }
    if (-not $before.temporary_chat) { $invalidationReasons += 'temporary_chat_not_observable' }
    if (-not $before.chat_surface_selected) { $invalidationReasons += 'wrong_chat_surface' }
    if ($before.message_marker_count -ne 0) { $invalidationReasons += 'prior_transcript_present' }
    if ($before.attachment_marker_count -ne 0) { $invalidationReasons += 'prior_attachment_marker_present' }
    if ($before.project_context_present) { $invalidationReasons += 'project_context_marker_present' }
    if ($before.file_library_context_present) { $invalidationReasons += 'file_library_context_marker_present' }
    if ($modelBefore.selected_model -ne '5.6 Sol') { $invalidationReasons += 'wrong_or_unobservable_model_selection' }
    if ($modelBefore.reasoning_selection -ne 'Extra High') { $invalidationReasons += 'wrong_or_unobservable_reasoning_selection' }

    $uiEvidence = [ordered]@{
        application = 'Microsoft Edge'
        application_version = (Get-Item 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe').VersionInfo.FileVersion
        before = $before
        model_before = $modelBefore
    }

    if ($request.mode -eq 'inspect' -or $invalidationReasons.Count -gt 0) {
        $uiJson = $uiEvidence | ConvertTo-Json -Depth 15 -Compress
        $result = [ordered]@{
            schema = 'world-kernel-build001-campaign2-subject-adapter-result-v1'
            passed = $invalidationReasons.Count -eq 0
            mode = $request.mode
            trial_id = $request.trial_id
            started_at = $startedAt
            completed_at = [DateTimeOffset]::UtcNow.ToString('O')
            invalidation_reasons = $invalidationReasons
            request_sha256 = Get-Utf8Sha256 $requestText
            ui_evidence = $uiEvidence
            ui_evidence_sha256 = Get-Utf8Sha256 $uiJson
            operation_count = $script:OperationCount
        }
        Write-AtomicJson $result
        $result | ConvertTo-Json -Depth 30 -Compress | Write-Output
        $exitCode = if ($result.passed) { 0 } else { 4 }
    }
    else {
        $basePrompt = [IO.File]::ReadAllText($request.base_prompt_path).Replace("`r`n", "`n").Trim()
        $toolContract = [IO.File]::ReadAllText($request.tool_contract_path).Replace("`r`n", "`n").Trim()
        $armPackage = [IO.File]::ReadAllText($request.arm_package_path).Replace("`r`n", "`n").Trim()
        $propositions = $request.propositions | ConvertTo-Json -Compress
        $prompt = "$basePrompt`n`nCOMMON TOOL CONTRACT`n$toolContract`n`nARM PACKAGE ($($request.arm))`n$armPackage`n`nCURRENT PROVIDER OBSERVATIONS`n$($request.current_observations)`n`nLOCKED TASK`nSemantic action: $($request.semantic_action)`nTarget: $($request.target)`nTask: $($request.task)`nPropositions: $propositions`n`nReturn exactly the locked JSON object now."
        $textbox = Find-Element 'Chat with ChatGPT' ([Windows.Automation.ControlType]::Edit) 'prompt-textarea' 10000
        $valuePattern = $textbox.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
        if ($valuePattern.Current.IsReadOnly) { throw 'ChatGPT prompt textbox is read-only.' }
        $valuePattern.SetValue($prompt)
        $textbox.SetFocus()
        [Windows.Forms.SendKeys]::SendWait('{END}')
        [Windows.Forms.SendKeys]::SendWait(' ')
        [Windows.Forms.SendKeys]::SendWait('{BACKSPACE}')
        $script:OperationCount += 4
        Start-Sleep -Milliseconds 150
        $preparedPrompt = (Get-Value $textbox).Replace("`r`n", "`n")
        if ($preparedPrompt -ne $prompt) { throw 'ChatGPT prompt editor did not retain the exact locked prompt.' }
        $send = @(Find-ElementsByName '^(Send prompt|Send message|Send)$' |
            Where-Object { $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button } |
            Select-Object -First 1)
        if ($send.Count -gt 0) {
            Invoke-Element $send[0]
        }
        else {
            [Windows.Forms.SendKeys]::SendWait('{ENTER}')
            $script:OperationCount += 1
        }

        Wait-Until {
            $current = Find-Element 'Chat with ChatGPT' ([Windows.Automation.ControlType]::Edit) 'prompt-textarea'
            if (-not $current) { return $true }
            $value = Get-Value $current
            return [string]::IsNullOrWhiteSpace($value) -or $value -eq 'Temporary chat'
        } 30000 'subject prompt submission' | Out-Null

        $parsed = Wait-Until {
            $stop = @(Find-ElementsByName '^(Stop generating|Stop response)$').Count -gt 0
            if ($stop) { return $null }
            $candidate = Get-LastSubjectJson (Get-PageText)
            if ($candidate) {
                try {
                    Test-SubjectOutput $candidate.value $request
                    return $candidate
                }
                catch { }
            }
            return $null
        } ([int]$request.response_timeout_ms) 'machine-readable subject response'
        Test-SubjectOutput $parsed.value $request

        $after = Get-SubjectState
        $modelAfter = Get-ModelState
        $fallback = $modelAfter.selected_model -ne '5.6 Sol' -or $modelAfter.reasoning_selection -ne 'Extra High'
        $uiEvidence.after = $after
        $uiEvidence.model_after = $modelAfter
        $uiJson = $uiEvidence | ConvertTo-Json -Depth 15 -Compress
        $result = [ordered]@{
            schema = 'world-kernel-build001-campaign2-subject-adapter-result-v1'
            passed = -not $fallback
            mode = 'invoke'
            trial_id = $request.trial_id
            arm = $request.arm
            started_at = $startedAt
            completed_at = [DateTimeOffset]::UtcNow.ToString('O')
            request_sha256 = Get-Utf8Sha256 $requestText
            prompt_sha256 = Get-Utf8Sha256 $prompt
            raw_response_sha256 = Get-Utf8Sha256 $parsed.raw
            subject_output = $parsed.value
            machine_readable_response_parsed = $true
            observable_product_fallback = $fallback
            invalidation_reasons = if ($fallback) { @('observable_product_fallback') } else { @() }
            ui_evidence = $uiEvidence
            ui_evidence_sha256 = Get-Utf8Sha256 $uiJson
            operation_count = $script:OperationCount
        }
        Write-AtomicJson $result
        $result | ConvertTo-Json -Depth 30 -Compress | Write-Output
        $exitCode = if ($result.passed) { 0 } else { 4 }
    }
}
catch {
    $result = [ordered]@{
        schema = 'world-kernel-build001-campaign2-subject-adapter-result-v1'
        passed = $false
        mode = $request.mode
        trial_id = $request.trial_id
        started_at = $startedAt
        completed_at = [DateTimeOffset]::UtcNow.ToString('O')
        invalidation_reasons = @('adapter_error')
        error_type = $_.Exception.GetType().Name
        error = $_.Exception.Message
        request_sha256 = Get-Utf8Sha256 $requestText
        operation_count = $script:OperationCount
    }
    Write-AtomicJson $result
    $result | ConvertTo-Json -Depth 30 -Compress | Write-Output
    $exitCode = 5
}
finally {
    if ($openedTab) {
        try {
            $close = Find-Element 'Close tab' ([Windows.Automation.ControlType]::Button) 'view_27' 3000
            if ($close) { Invoke-Element $close }
        }
        catch { }
    }
}

exit $exitCode
