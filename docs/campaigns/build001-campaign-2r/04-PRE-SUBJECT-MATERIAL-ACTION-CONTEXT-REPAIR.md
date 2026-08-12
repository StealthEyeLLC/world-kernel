# Build 001 Campaign 2R — Pre-Subject Material-Action Context Repair

Date: 2026-08-12
Branch: `build001-campaign-2r`
Authority: `docs/campaigns/build001-campaign-2r/00-AUTHORITY.md`
Superseded execution freeze: `a732b06dde446beddaec6e120c88275eb8c6e870`

## Status at discovery

The first Campaign 2R execution freeze was provider-verified, but no Campaign 2R scientific subject invocation, evaluator seed commitment, ActionAttempt, Prediction, Outcome, PredictionEvaluation, TransitionEpisode, campaign-scoped Claim/Correspondence, or acquisition file had occurred.

A final controller launch-path audit found one equivalent mechanical execution defect before the first subject: the frozen controller scheduled `material-action` at `Highest` integrity, while this workstation exposes the signed-in GitHub credential to the interactive user only at `Limited` integrity. The subject job remains `Highest` because it requires the signed-in desktop/Edge session. Reset, verify-reset, prepare, and material Git/provider work use the Limited interactive token.

## Repair

Exactly one controller execution-context line changed:

- before: `material-action` user job at `Highest`;
- after: `material-action` user job at `Limited`.

No semantic action, target, parameter, outcome proposition, provider authority, subject configuration, seed-generation regime, stop rule, scoring rule, lineage rule, or database gate changed.

## Non-scientific proof

`artifacts/campaign-2r/preflight/material-action-context-repair.json` records a Limited interactive scheduled task running as `STEALTHEYELLC\StealthEye`. It contacted GitHub using the existing disposable P3 accepted workspace and issued an idempotent no-op push. The local and provider branch heads were equal before the push and remained equal afterward; no provider state changed.

The six-action pre-dispatch audit was regenerated against the repaired controller and asserts that the six semantic actions and evaluation spec remain exact, Prediction still precedes dispatch, the subject remains Highest, and material action now uses Limited.

The full implementation/hostile regression suite passes 16/16 with zero build warnings after this repair. The formal P0–P6 evaluator remains all-pass. Campaign 2R scientific counters were reobserved at zero.

## Freeze consequence

The first execution freeze is not used for acquisition after this source change. A new prospective execution-freeze commit must supersede `a732b06dde446beddaec6e120c88275eb8c6e870`, bind the provider-verified repair commit, and again prove all Campaign 2R scientific counters zero before the first subject invocation.