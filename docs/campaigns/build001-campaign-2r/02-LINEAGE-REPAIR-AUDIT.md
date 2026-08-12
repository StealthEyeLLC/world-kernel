# Campaign 2R — Lineage Repair Audit

Date: 2026-08-11
Campaign: `build001-campaign-2r`
Architecture change: **NO**
Scientific design change: **NO**
Hard lineage gate weakened: **NO**

## Repair

Campaign 2R separates local and hosted provider lineage while preserving distinct Manifestations and `git:working_copy_of` correspondence.

For local working-copy Claims, `campaign2-state-observer` now persists a local Observation targeted at the local Manifestation and backed by `git/native` Evidence. Its provider revision is the local HEAD.

For `github:remote_ref_head`, `Campaign2Lineage.cs` extracts the exact retained `git ls-remote --heads origin refs/heads/<branch>` provider command/result, validates it against `Campaign2StateObservation.remote_head`, persists a narrow `github/provider` Evidence object, and creates a `campaign2-github-ref-observer` Observation targeted at the hosted GitHub Manifestation. The hosted-ref Claim can no longer accept the local Observation/Evidence parameters.

Correspondence records both local and hosted Observations/Evidence. Prediction preconditions/basis records both worlds. The durable begin record preserves both pre-state lineage IDs so crash recovery cannot silently reconstruct only the local side.

The post-action path performs the same separation: local reobservation, hosted-ref reobservation, and provider check/browser outcome Observation remain distinct. Provider receipts are stored as receipts and are not promoted into verified provider Claims. Outcome/Episode links include the independently acquired post-state sources.

Public EpisodeExport now assigns `github:remote_ref_head` its dedicated hosted-ref evidence hash. Browser presentation remains a separate `github:browser_presented_head` Claim and cannot establish hosted material ref truth.

## Claim construction inventory

The machine-readable inventory is `artifacts/campaign-2r/claim-lineage-matrix.json` (10 rows: all nine material/provider Claim types plus the derived topology Claim). It records subject Manifestation type, authority/production class, Observation source/subject/provider, Evidence source/provider, valid/known time source, derivation type, and correspondence dependence.

Material/provider Claim constructors audited:

- `git:local_head`
- `git:current_branch`
- `git:worktree_clean`
- `git:remote_tracking_head`
- `git:remote_url`
- `github:remote_ref_head`
- `github:check_started`
- `github:check_terminal_success`
- `github:browser_presented_head`

`git:public_topology_class` was also audited as a derived, non-provider/non-material Claim. It does not convert local or hosted evidence into provider truth.

## Kernel enforcement hardening

The existing architecture already requires provider/material Claims to be grounded in subject-matched provider Observation/Evidence. The Run A suite proved the subject identity portion, but not provider-family compatibility or freshness semantics. Campaign 2R therefore strengthens the existing gate without weakening or adding an ontology primitive:

- provider Observations must be compatible with the target Manifestation provider family;
- Observation/Evidence provider namespaces must be compatible with the target Manifestation;
- material/provider Claims require a `succeeded` Observation;
- receipt acquisition methods cannot be the primary Evidence for a verified material/provider Claim;
- the original subject-matched Observation/Evidence requirement remains mandatory.

No correspondence-linked exception exists. No repository `same_as` was introduced.

## Regression

The suite increased from 13 to 16 named tests and passes **16/16**, with zero failures. Final result: `artifacts/campaign-2r/implementation-test-results.json`, SHA-256 `30a95c22540c11b9754032359f2e701f9b62350b527a0901e3af66437fb1b749`.

New coverage includes:

- exact Run A negative: local working-copy Evidence -> hosted GitHub Claim => REJECTED;
- exact positive counterpart: GitHub-provider Evidence/Observation -> hosted GitHub Claim => ACCEPTED;
- GitHub evidence/Observation -> local-only Manifestation => REJECTED;
- stale browser Observation -> fresh hosted Claim => REJECTED;
- model inference -> provider Observation => REJECTED;
- receipt -> verified provider Claim without fresh provider Observation => REJECTED;
- Prediction identifier supplied as provider Evidence => REJECTED.

The final Release build has zero warnings and zero errors.

## Real pre-freeze lifecycle sentinel

A disposable clone of `StealthEyeLLC/world-kernel-build-001-fixture` on `wk-b001-c2-audit-reset` was observed through the real Campaign 2 state observer. All harness rows were written only to the isolated non-scientific database `world_kernel_c2r_h_42e39350e2`.

The first successful sentinel (`git:create_local_commit`) persisted local and hosted provider-matched Claims, correspondence, an ActionAttempt, a complete Prediction, Prediction lineage, and the durable dispatch seal. It then stopped deliberately before material action. Assertions: Prediction before seal PASS; remote Claim subject match PASS; remote provider match PASS; local Claim subject match PASS; Outcomes 0; PredictionEvaluations 0; TransitionEpisodes 0; material dispatch false.

An earlier harness-development attempt was rejected by the Prediction JSON-shape constraint before a Prediction persisted; the non-scientific harness input was corrected before any freeze or scientific invocation.

## Six-action dry pre-dispatch audit

All six semantic actions independently passed the same real construction path with fresh provider observations and no material dispatch:

- `git:create_local_commit`
- `git:create_branch`
- `git:push_ref`
- `github:create_remote_commit`
- `git:fetch_remote`
- `git:integrate_fast_forward`

Machine-readable result: `experiments/build001/campaign-2r/harness/six-action-pre-dispatch-audit.json`, SHA-256 `8aec31fca7b9e71c759ad1b0cefdd63db4cffb8823d5d82fe3f57147c7e721f2`.

Lifecycle sentinel: `experiments/build001/campaign-2r/harness/non-scientific-lifecycle-sentinel.json`, SHA-256 `9812572ea5a58b36e5885e1073c15961d820fe9053d40a329162eadb6e116180`.

## EpisodeExport leakage audit

`artifacts/campaign-2r/episode-export-leakage-audit.json` found zero occurrences of the forbidden hidden-regime/answer-key labels in the public Claim export builder and verified that hosted-ref provenance uses the dedicated remote evidence hash. Result: PASS.

## Changed execution surface

`artifacts/campaign-2r/changed-execution-files.json` records old Run A SHA-256, new Run B SHA-256, and reason for all 15 changed execution/runtime files. Every row records:

- scientific meaning changed: NO
- subject information changed: NO
- fixture difficulty changed: NO
- metric changed: NO
- arm changed: NO

The Campaign 2R path/ID changes isolate Run B from immutable Run A. The new harness command is pre-freeze audit-only and has no scientific subject or material-dispatch authority of its own.