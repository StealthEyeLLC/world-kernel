# Build 001 Campaign 2 prospective preregistration amendment

Date: 2026-08-11
Status: prospective amendment; audited execution-freeze manifest required before acquisition
Authority: `docs/campaigns/build001-campaign-2/00-AUTHORITY.md`
Original immutable preregistration: `docs/preregistration/original/`

## Prospectivity statement

Campaign 1 completed implementation but did not execute the cognitive experiment:

- Campaign 1 acquisition episodes: 0
- Campaign 1 pilot blocks: 0
- Campaign 1 confirmatory blocks: 0
- Campaign 1 drift blocks: 0
- Campaign 1 Cold outcomes: 0
- Campaign 1 Conventional Memory outcomes: 0
- Campaign 1 Structured outcomes: 0

Campaign 2 preflight repair and the execution-mechanics audit were therefore performed before any pilot, confirmatory, drift, or treatment-vs-control outcome existed. Immediately before the audited P0-P6 gate was regenerated, the Campaign 2 evaluator and measured kernel tables also contained zero scientific rows.

This amendment is not a post-hoc attempt to rescue an observed result.

## Immutable scientific design

Campaign 2 does not change the Build 001 scientific hypothesis or the treatment comparison.

Arms remain:

1. Cold
2. Conventional Memory
3. Structured

The primary comparison remains **Structured vs Conventional Memory**. The primary endpoint remains configuration-block mean Brier loss.

Strong predictive support still requires all of the following:

- `>= 20%` relative reduction in Structured mean Brier loss versus Memory;
- paired configuration-block bootstrap with `10,000` resamples and a 95% CI for `B_M - B_S` excluding zero in Structured's favor;
- the frozen paired randomization/permutation test with `p < 0.05`;
- the frozen behavioral co-gate;
- fresh-instance validity;
- drift validity;
- equal-information/equal-compute validity.

Outperforming Cold is not sufficient for PASS.

## Frozen action classes

The six action classes remain unchanged:

- `git:create_local_commit`
- `git:create_branch`
- `git:push_ref`
- `github:create_remote_commit`
- `git:fetch_remote`
- `git:integrate_fast_forward`

No action class may be added or removed after acquisition begins.

## P0 observable-model operationalization

Campaign 2 uses an observable controlled ChatGPT product configuration instead of claiming access to an unavailable private OpenAI deployment identifier.

The controlled subject configuration is:

- product surface: ChatGPT web;
- documented/selected model: `5.6 Sol`;
- reasoning: `Extra High`;
- conversation: brand-new Temporary Chat;
- subject mode: Chat;
- workspace: Business;
- no prior trial transcript;
- no project context;
- no File Library context;
- no prior trial attachment leakage.

Observable configuration fingerprint:

`afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5`

The private deployment identifier remains explicitly unobserved; no private-backend equality claim is made.

## Frozen subject contracts

The subject contracts remain those formally attested by P5:

- base prompt SHA-256: `f4ce7079afc8bfdc02998f62db9d32d3853d5b2b36a4c7f45fc2a1289c4e8fe5`;
- tool contract SHA-256: `e1d2b899190ed0c0f88e1dce4fb2a73f653de20b9d8766df4e196cb4f883c181`;
- trial-output contract SHA-256: `8aec3ac079db715f8d6990d52a8cd9f7d275e4e3ded2efa1088ef101d9d5c193`;
- P5 subject driver SHA-256: `b2c6e52e4a44eff7e46ebdbb6e97d5ec706110698362961106ffe96ded27ab98`;
- P5 invocation adapter SHA-256: `98d4bd52e90bf86564c502898403c8549c8a21e31f722c893c1f693e2334df46`;
- fresh invocation method: `campaign2-temporary-chat-isolation-v1` / the formally attested Campaign 2 P5 method.

If the subject driver, adapter, base prompt, tool contract, output contract, model selection, reasoning level, or observable configuration changes before acquisition, P5 must be re-probed and re-attested prospectively. There is no silent fallback.

## P2 authenticated browser operationalization

The hosted GitHub action remains browser-mediated through eyeBROWSE and confined to `StealthEyeLLC/world-kernel-build-001-fixture` disposable `wk-b001-*` branches and `fixture/state.txt`.

A browser receipt is not Outcome. The provider state must be independently reobserved. Old browser observations remain historically stale and are not rewritten as fresh.

For check-bearing push outcomes, a GitHub Actions run qualifies only if its provider creation time is at or after the durable action dispatch timestamp and its head SHA matches the sealed target. eyeBROWSE independently records the visible checks presentation as supplementary evidence. This prospective temporal discriminator prevents evaluator-setup runs on the same SHA from being misclassified as action consequence.

## Deterministic reset / evaluator separation

The hidden fixture schedule remains evaluator-only. It is sealed outside the repository tree and committed by hash before the corresponding reset/subject invocation.

For each action slot:

1. the evaluator records the prospective seed/schedule commitment;
2. the fixture is reset through real Git/GitHub provider mechanics;
3. a separate verifier reconstructs the material Git/provider state and fingerprint;
4. the evaluator requires the sealed schedule, reset manifest, independent observation, provider identity, fixture baseline, branch, push regime, check regime, and browser-freshness setup to agree;
5. only then may the Prediction/dispatch lifecycle begin.

Hidden evaluator labels and answer keys are not exposed to the subject, Memory package, Structured package, or public acquisition `EpisodeExport`.

P6 is bound to the final audited reset implementation by its execution-freeze manifest. Earlier P6 v2/v3 records remain superseded engineering history.

## Fresh-context and trial invalidation rules

Every scored arm invocation remains a new Temporary Chat with zero prior trial transcript. A subject is never reused for another arm.

A trial is invalidated only for the frozen mechanical reasons: wrong product/model/reasoning configuration, context contamination, malformed or unparsable response, failure to record the full pre-action prediction vector before dispatch, mismatch between sealed action/parameters and dispatched action, corrupted reset/isolation, or other preregistered mechanical invalidity. Unfavorable predictions or outcomes are not invalidation grounds.

Crash recovery is state-based and fail-closed. A retry may reuse an exact prospective registration or durable Action/Prediction/Outcome state; it may redispatch only when provider/native state proves the pre-state is unchanged and the action has not already occurred. Ambiguous state is not silently retried.

## Equal information and representation

Every eligible acquisition TransitionEpisode becomes one canonical public source episode. Memory and Structured select from the exact same candidate episode IDs under the frozen public deterministic selector and the same inherited token/byte ceiling.

Conventional Memory remains a serious control. It receives the same public episode facts—including prior observations, claims, valid/known times, correspondence, action, probabilities, outcome, errors, timestamps, and evidence lineage—in chronological experiential narrative form.

Structured receives those same source episodes through the existing typed World Kernel representation. It receives no hidden evaluator label, treatment-only retrieval oracle, embedding search, or extra model call.

The difference is representation, not history quantity or privileged truth.

## Acquisition

Acquisition remains prospective and provider-backed.

- begin with 24 independent configuration blocks;
- continue to at most 36 only until the original coverage criteria are satisfied;
- require at least 20 eligible episodes per semantic action;
- require at least 8 accepted pushes and 8 rejected pushes;
- require at least 8 accepted fast-forwards and 8 rejected fast-forwards;
- require at least 6 distinct configuration seeds per action;
- retain the original check-regime and remaining coverage requirements.

If coverage is not achieved by 36 blocks, do not manufacture it.

## Pilot

After acquisition coverage passes, run exactly 12 held-out configuration blocks × 3 arms. Pilot data remains permanently excluded from confirmatory analysis.

Only the originally permitted fields may be tuned from pilot evidence: H1/H2/H3 horizons, inherited package budget within allowed bounds, serialization formatting without information change, reset mechanics, unambiguous proposition wording/schema, mechanically derived confirmatory N, and provider wait/censor mechanics.

Memory headroom requirement remains:

`mean Memory block Brier >= 0.05`

If Memory mean Brier is below `0.05`, redesign/re-pilot according to the frozen rule.

## Confirmatory sample size

The frozen calculation remains:

`d_i = L_M,i - L_S,i`

`Mbar = mean Memory pilot Brier`

`s_d = sample SD of d_i`

`delta = 0.20 * Mbar`

`n_raw = ((1.959964 + 0.841621) * s_d / delta)^2`

`N = ceil_to_multiple_of_8(max(48, n_raw))`

Special rules remain:

- if `s_d == 0`, `N = 48`;
- if `Mbar < 0.05`, redesign/re-pilot;
- if calculated `N > 96`, redesign/re-pilot and **do not clip N to 96**.

The final pilot result, horizons, inherited budget, serializer/scorer hashes, randomization algorithm, and confirmatory seed commitment must be frozen and pushed before confirmatory seeds are opened.

## Confirmatory and drift

Each confirmatory block remains matched across Cold, Memory, and Structured under equivalent hidden configuration, with randomized arm order and isolated fixture branches/worktrees.

A scored Prediction must be durable before material dispatch. Ground truth comes from fresh provider-native reobservation, not from the subject, orchestrator intuition, exit code alone, HTTP success alone, or a browser success presentation.

The drift cohort and contradiction rule remain unchanged. By the third strong contradiction, the obsolete-outcome probability must be at most 0.50 and below the newly supported alternative; at least 90% of qualifying drift blocks must meet the frozen bound.

## Integrity requirements

Campaign 2 continues to require:

- no repository-level `same_as`;
- local and hosted repositories remain distinct Manifestations;
- hard `git:working_copy_of` only with sufficient provider/history evidence;
- correspondence precision 100% and recall at least 95% among sufficiently evidenced true cases;
- correct `valid_as_of`, `known_as_of`, and current freshness/unknown semantics;
- immutable/content-addressed Evidence;
- no Prediction -> Observation laundering;
- no receipt -> verified Outcome substitution;
- no old Evidence -> fresh Evidence substitution;
- no projection -> raw provider Evidence substitution.

## Execution freeze requirement

The audited P0-P6 manifest currently has SHA-256:

`e9b37f1719c666001f167bf6a18079c073b9cfa889bc83c194d2ce09e98b9391`

Before any acquisition seed is opened, a machine-readable Campaign 2 execution-freeze manifest must bind the exact audited implementation commit, governing preregistration/authority hashes, Eye-line HEADs, P0/P5/P2 evidence, subject contracts, serializers, scorer, reset/check mechanics, the six action classes, the three arms, primary metric/statistics, behavioral/drift gates, fresh-context/equal-information rules, and invalidation rules.

The execution freeze is prospective. If any frozen execution file changes afterward, acquisition must not continue under the stale freeze; the change must be evaluated under the preregistered invalidation/re-attestation boundary.

## Pre-subject execution-freeze repair

The initial execution freeze was committed and provider-verified before acquisition at `c7762b38b969c586d330387eed1181ebbec913a8` with freeze SHA-256 `e094714b0dbe3681f4d30c32aec379643eb3369a9e289452ebf011aecad90667`.

The first acquisition action-slot seed (`campaign2-acquisition-block-seed-01-lc`, configuration block `c2-acq-01`) was durably inserted before the controller hit a mechanical CLR compatibility error: PostgreSQL `timestamptz` was returned by Npgsql as `System.DateTime`, while the new execution code directly cast the value to `System.DateTimeOffset`.

No ChatGPT subject had been invoked. No Prediction existed. No Action was declared or dispatched. No provider Outcome or ground truth existed. No scientific result had been observed. The sealed hidden schedule and its commitment were not changed.

The bounded repair therefore changes only timestamp materialization in the Campaign 2 execution plumbing. All PostgreSQL timestamp reads now pass through one deterministic UTC coercion helper, including retry and recovery paths. The freeze validator also distinguishes the original pre-acquisition freeze from a narrowly scoped replacement freeze that is permitted only before the first subject invocation, only with zero scientific outcomes, and only when it explicitly names the superseded prospective freeze.

This repair changes no subject mechanism, prompt, arm representation, candidate episodes, metric, scorer, action class, hidden schedule, fixture difficulty, statistical procedure, behavioral gate, drift gate, or hypothesis. The exact already-committed seed is retained rather than replaced.

Durable engineering evidence: `experiments/build001/campaign-2/acquisition/engineering/pre-subject-timestamp-repair.json`.