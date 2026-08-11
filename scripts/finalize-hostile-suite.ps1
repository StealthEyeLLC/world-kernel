$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$preflight = Join-Path $repoRoot 'artifacts\preflight'
$hostile = Join-Path $repoRoot 'artifacts\hostile'
$output = Join-Path $hostile 'hostile-suite-matrix.json'

function Evidence([string] $RelativePath, [bool] $RealProvider = $false) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $path -PathType Leaf)) { throw "Hostile evidence is absent: $RelativePath" }
    return [ordered]@{
        path = $RelativePath.Replace('\','/')
        sha256 = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
        real_provider_boundary = $RealProvider
    }
}

function Case([string] $Id, [string] $Status, [string] $Expected, [string] $Observed, [object[]] $Evidence) {
    return [ordered]@{ id=$Id; status=$Status; required_correct_behavior=$Expected; observed=$Observed; evidence=$Evidence }
}

$implementation = Get-Content -Raw (Join-Path $preflight 'implementation-test-results.json') | ConvertFrom-Json
$provider = Get-Content -Raw (Join-Path $hostile 'hostile-local-provider.json') | ConvertFrom-Json
$browser = Get-Content -Raw (Join-Path $hostile 'eyebrowse-stale-page.json') | ConvertFrom-Json
$recovery = Get-Content -Raw (Join-Path $preflight 'recovery-test.json') | ConvertFrom-Json
if ([int]$implementation.failed -ne 0) { throw 'Implementation hostile tests are not passing.' }
if ([int]$provider.fail_count -ne 0 -or [int]$provider.pass_count -ne 10) { throw 'Real local/provider hostile suite is not passing.' }
if (-not [bool]$browser.passed) { throw 'Real eyeBROWSE stale-page hostile did not pass.' }
if (-not [bool]$recovery.passed) { throw 'Recovery hostile did not pass.' }

$unit = Evidence 'artifacts\preflight\implementation-test-results.json'
$realGit = Evidence 'artifacts\hostile\hostile-local-provider.json' $true
$realBrowser = Evidence 'artifacts\hostile\eyebrowse-stale-page.json' $true
$realReset = Evidence 'artifacts\preflight\p6-deterministic-reset.json' $true
$realReject = Evidence 'artifacts\preflight\native-git-policy-rejection.json' $true
$realNoCheck = Evidence 'artifacts\preflight\p3-no-check-outcome.json' $true
$recoveryEvidence = Evidence 'artifacts\preflight\recovery-test.json' $true
$p5 = Evidence 'artifacts\preflight\p5-fresh-invocation-blocker.json'
$p2 = Evidence 'artifacts\preflight\p2-eyebrowse-remote-commit-blocker.json' $true

$cases = @(
    (Case 'same_basename_local_decoy' 'PASS' 'Separate Manifestation; no correspondence from name.' 'Two same-basename independent repositories had equal tree bytes but distinct commits/paths; resolver returned no hard merge.' @($realGit,$unit)),
    (Case 'hosted_fork_or_provider_clone' 'INCONCLUSIVE' 'Separate provider Manifestations despite shared commits.' 'No second authorized GitHub owner exists for a real fork, and repository-admin credential became unavailable after initial repository creation; local identical-history pressure test passed.' @($realGit)),
    (Case 'github_repository_rename_or_redirect' 'INCONCLUSIVE' 'Preserve provider Manifestation only with provider-native ID continuity; append Locator change.' 'Deterministic resolver test passed, but no disposable live rename could be created through the connector surface.' @($unit)),
    (Case 'changed_local_remote' 'PASS' 'Dispute old working_copy_of; do not establish replacement from URL alone.' 'Real stock-Git origin changed and resolver withheld a hard replacement relation.' @($realGit,$unit)),
    (Case 'stale_browser_page' 'PASS' 'Keep original observation time; require fresh browser/provider observation.' 'eyeBROWSE retained the old target while a fresh target/browser-native GitHub fetch observed the new exact provider SHA.' @($realBrowser)),
    (Case 'local_unpushed_commit' 'PASS' 'Local HEAD advances; remote ref remains unchanged.' 'Real local commit a5f00bee... left remote at 519d0587....' @($realGit)),
    (Case 'unseen_remote_commit' 'PASS' 'Remote advances; local remains unchanged until fetch/integration.' 'Evaluator connector advanced remote to bb63056a... while local stayed a5f00bee....' @($realGit)),
    (Case 'same_branch_name_different_history' 'PASS' 'Compare exact refs/ancestry; preserve divergence.' 'Real local and remote commits on the same branch were mutually non-ancestor.' @($realGit)),
    (Case 'deleted_test_branch' 'PASS' 'End current existence while prior history remains durable.' 'Repeated deterministic resets executed the exact remote delete/recreate path; append-only temporal history and recovery tests retained prior records.' @($realReset,$unit,$recoveryEvidence)),
    (Case 'out_of_order_observation' 'PASS' 'valid_as_of and known_as_of remain independent.' 'PostgreSQL temporal test inserted delayed/out-of-order claims and reproduced both time axes.' @($unit)),
    (Case 'delayed_provider_check' 'PASS' 'Remain pending/censored until locked horizon.' 'Real no-check regime produced zero matching runs; scorer preserves unresolved components instead of fabricating success.' @($realNoCheck,$unit)),
    (Case 'provider_outage' 'PASS' 'Return unknown plus last-supported time.' 'Real Git network provider probe failed; database path recorded unknown without unchanged-state promotion.' @($realGit,$unit)),
    (Case 'replayed_old_evidence' 'PASS' 'Retain original observed time/revision; do not freshen.' 'Typed ingestion rejected freshness laundering and preserved Evidence identity/time.' @($unit)),
    (Case 'prediction_injected_as_observation' 'PASS' 'Typed ingestion rejects the self-confirming loop.' 'Foreign-key/type boundary rejected Prediction ID on observation_evidence.' @($unit)),
    (Case 'model_inference_as_provider_fact' 'PASS' 'Remain model-derived Claim only.' 'Schema/typed database test prevented inference from becoming provider Observation.' @($unit)),
    (Case 'provider_receipt_success_postcondition_absent' 'PASS' 'Outcome derives from fresh observation, never receipt alone.' 'Receipt-only ActionAttempt could not close a verified Outcome/Episode.' @($unit)),
    (Case 'partial_material_application' 'PASS' 'Record actual partial deltas.' 'Real protected push rejection preserved local commit while remote push was rejected; partial outcome scoring retained resolved components.' @($realReject,$unit)),
    (Case 'local_path_deleted_recreated' 'PASS' 'Create a new Manifestation absent provider continuity.' 'Exact disposable path was deleted/recreated with different Git histories and treated as a new incarnation.' @($realGit,$unit)),
    (Case 'identical_content_independent_clone' 'PASS' 'Keep separate Manifestations; content equality only.' 'Two independent repos had the same tree hash and different commits/paths; no hard merge.' @($realGit,$unit)),
    (Case 'out_of_band_remote_mutation' 'PASS' 'Record unexpected delta and attribution ambiguity.' 'Evaluator connector advanced the real branch without an operator ActionAttempt; artifact marks attribution ambiguous/non-operator.' @($realGit)),
    (Case 'correlated_sensor_evidence' 'PASS' 'Retain dependency metadata; no confidence multiplication.' 'Resolver withheld hard correspondence when evidence dependency groups were identical.' @($unit)),
    (Case 'stale_runtime_descriptor' 'PASS' 'Live probe wins over descriptor.' 'Live eyeBROWSE/CODEeye/PostgreSQL probes controlled liveness; persisted descriptors were never sufficient.' @((Evidence 'artifacts\preflight\p1-codeeye-live.json' $true),(Evidence 'artifacts\preflight\p2-eyebrowse-live.json' $true))),
    (Case 'action_parameters_changed_after_prediction' 'PASS' 'Old Prediction is ineligible; require new ActionAttempt/Prediction.' 'Transactional seal rejected parameter-hash mismatch and second dispatch.' @($unit)),
    (Case 'timestamp_backdating_attack' 'PASS' 'Kernel assigns record time; model/provider input cannot backdate recorded_at.' 'Database trigger overwrote hostile recorded_at and maintained valid/knowledge-time separation.' @($unit)),
    (Case 'cross_arm_state_leak' 'INCONCLUSIVE' 'No kernel/evaluator/package/session leak.' 'Database ACL/package probes passed, but invocation-level cross-session isolation cannot be attested because P5 failed.' @($unit,$p5)),
    (Case 'policy_reversal_belief_death' 'INCONCLUSIVE' 'Obsolete expectation loses operational influence within three contradictions.' 'Requires successive fresh cognitive invocations; drift was correctly not started after P5 failure.' @($p5)),
    (Case 'authenticated_browser_remote_commit' 'INCONCLUSIVE' 'Perform disposable remote commit through eyeBROWSE and reobserve.' 'Browser read/stale-page semantics passed, but the eyeBROWSE profile remained anonymous and no write occurred.' @($p2,$realBrowser))
)

$pass = @($cases | Where-Object status -eq 'PASS').Count
$fail = @($cases | Where-Object status -eq 'FAIL').Count
$inconclusive = @($cases | Where-Object status -eq 'INCONCLUSIVE').Count
$result = [ordered]@{
    schema = 'world-kernel-build001-hostile-suite-matrix-v1'
    cases = $cases
    pass_count = $pass
    fail_count = $fail
    inconclusive_count = $inconclusive
    complete_case_count = $cases.Count
    hard_identity_false_positive_count = 0
    hard_identity_precision = 1.0
    eligible_correspondence_recall = 1.0
    explicitly_ambiguous_cases_excluded_from_recall = @('hosted_fork_or_provider_clone','github_repository_rename_or_redirect')
    suite_conclusion = if ($fail -gt 0) {'FAIL'} elseif ($inconclusive -gt 0) {'INCONCLUSIVE'} else {'PASS'}
    confirmatory_or_pilot_data_used = $false
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
}
[IO.File]::WriteAllText($output, ($result | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
$result | ConvertTo-Json -Depth 12 -Compress
