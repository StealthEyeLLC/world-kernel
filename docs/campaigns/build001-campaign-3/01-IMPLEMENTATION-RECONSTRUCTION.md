# Build 001 Campaign 3 Implementation Reconstruction

Date: 2026-08-12
Branch: `build001-campaign-3`
Base implementation: `4b1d5e415797db7011c6e580120fe394758b15f0`
Base tree: `4a27ce78a7161ee556f0bb6ae57cdaa64df8d113`
Status: pre-freeze implementation checkpoint; no Campaign 3 scientific subject has run.

## Base selection

Campaign 3 is reconstructed from the provider-published repaired Campaign 2R implementation, not the local-only 2R freeze candidate. `3d130abd...` changes only 2R freeze/publication/boundary records and therefore is not an execution implementation base.

## Carried repairs and Campaign 3-only mechanics

1. **Provider-matched Claim lineage.** Retained because it restores the original Build 001 material/provider lineage invariant. It changes no hypothesis, arm information, scoring, ontology, provider authority, or sibling architecture.
2. **Prediction / Evidence / Observation / Outcome separation.** Retained as original Build 001 invariants: Prediction is never Observation, receipt is never material Outcome, stale/replayed Evidence is not fresh, provider outage remains unknown, and projections are rebuildable/non-authoritative.
3. **GitHub UI compatibility.** CodeMirror/contenteditable/query-shape handling remains a presentation compatibility bridge only; the semantic action remains `github:create_remote_commit`, eyeBROWSE remains the required subject/browser path, and GitHub remains material authority.
4. **Fresh subject driver.** Retained only to deliver the frozen arm payload and collect machine-readable output from one fresh Temporary Chat. It is not a second reasoning agent.
5. **Authenticated material-action context.** The user worker retains Limited interactive execution context only to restore the intended signed-in Git/GitHub path. It does not change the scientific environment or semantic action class.
6. **Campaign 3 runtime isolation.** Controller/test defaults point to the fresh Campaign 3 PostgreSQL runtime/evidence roots so Campaign 2R runtime state cannot be inherited accidentally.
7. **Logged-out PostgreSQL runtime repair.** The experiment-owned on-demand PostgreSQL process runs as `NT AUTHORITY\LOCAL SERVICE` so P4 can operate while Windows is logged out. No Windows SCM service is added and no scientific material-action path is changed.
8. **P0 evidence composer.** `Campaign3P0Composer` only converts genuine live interactive inspection/personalization observations into the exact frozen P0 artifact and rejects any model/reasoning/workspace/personalization deviation. It cannot manufacture the required browser observation and cannot authorize science by itself.
9. **Science-authorization hard gate.** `Campaign3Boundary` fails closed unless the provider-published freeze commit/tree/manifest, local identity, subject fingerprint, P0–P6 status, external Eye heads, and zero-state hashes are exact. It is intrinsic experimental boundary enforcement, not a verifier agent/pipeline or policy subsystem.
10. **Single-freeze rule.** Campaign 3 freeze validation accepts only one prospective pre-science freeze and explicitly rejects replacement/superseding-freeze markers.

## Verification

The isolated Campaign 3 candidate builds with zero warnings/errors. The authoritative isolated regression suite passes **21/21** with zero failures, including historical Campaign 2 regressions, Campaign 3 attestation/outcome regressions, P0 composition, science-authorization mismatch rejection, provider-matched Claim lineage positives and cross-provider negatives, prediction-before-dispatch, receipt/outcome separation, stale-evidence/projection laundering hostiles, arm/evaluator isolation, and paired statistics.

The six-action non-scientific dry audit passes all six frozen action classes with one durable ActionAttempt and Prediction per action, Prediction before dispatch, provider/subject-matched lineage, and zero material dispatches, Outcomes, PredictionEvaluations, or TransitionEpisodes.

The dedicated Campaign 3 science databases are literal zero. Regression and dry validation use disposable scratch databases and do not populate the science databases.