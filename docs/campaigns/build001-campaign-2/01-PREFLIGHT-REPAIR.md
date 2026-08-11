# Build 001 Campaign 2 preflight repair and execution audit

Date: 2026-08-11
Status: P0-P6 PASS on the audited Campaign 2 tree; no acquisition, pilot, confirmatory, or drift outcome exists at this record.

## Boundary and authority

Campaign 1 remains permanently classified **NO VALID CONFIRMATORY CONCLUSION**. Its historical result remains in `docs/05-BUILD-001-RESULTS.md`; Campaign 2 does not rewrite it.

Campaign 2 remains the prospective Build 001 rerun authorized by `docs/campaigns/build001-campaign-2/00-AUTHORITY.md`. The work described here is limited to preflight repair, deterministic experiment mechanics, recovery, and evidence capture required to execute the already-frozen Build 001 experiment. It does not authorize Build 002, canonicalization, architecture redesign, or sibling-Eye architecture changes.

Campaign 1 evidence in `artifacts/preflight/` remains historical. Campaign 2 uses the separate `artifacts/campaign-2/preflight/` and `experiments/build001/campaign-2/` namespaces.

Immediately before the audited gate was regenerated, evaluator and kernel queries returned zero Campaign 2 seed commitments, hidden configurations, reset-verification rows, invocation attestations, ground-truth rows, aggregate results, measured Actions, and TransitionEpisodes. No scientific outcome was available to influence these repairs.

## Final audited preflight

| Gate | Result | Current Campaign 2 evidence |
|---|---:|---|
| P0 observable subject configuration | PASS | `p0-baseline.json`, SHA-256 `b55fa393290cb349fdab1fe4a45c86ce3a2efc34152efa60810c9e15ee2e7672` |
| P1 live CODEeye | PASS | `p1-codeeye-live.json`, SHA-256 `f83554d2b03390e3fc0cfe199e309f942dc4469f0f156a4c5920ff21e40814b8` |
| P2 authenticated eyeBROWSE + fixture write | PASS | live `95e7ededa361e65d26a97403e4e2c5694a8297ba3dc3d23b67486d7b14f39a84`; browser remote-commit `e469cd6fddb95299f2d6bca329119993a06fbc593baaf1a669a9325c2b55ea03` |
| P3 experiment-owned native Git facet | PASS | existing Campaign 2 P3 evidence remains valid; the audited execution also exercised real accepted and rejected fixture pushes without changing P3 semantics |
| P4 PostgreSQL / integrity / recovery | PASS | `implementation-test-results.json`, 13 passed / 0 failed, SHA-256 `e36a31cbb566fa16e35e5b244c42bdf1ff0b99cb4c77546b0ee4152989863c53` |
| P5 fresh isolated invocation | PASS | `p5-fresh-invocation.json`, SHA-256 `104968ac005fb9b77ba305f9bde4d54cbbcaf34d746d68a7d7e79345a7af9934` |
| P6 deterministic reset | PASS | v4 bundle SHA-256 `9f5e0d3961f9e563bb26ef35a2067e212fe5576109b510f5c86433a7c364ca3e`; source attestation SHA-256 `3d0446d4b4135f951bbc2286ebc98ed40237f381ddc36787c3c751ac6b3e8d02` |

The repository's unmodified `PreflightGateEvaluator` produced `artifacts/campaign-2/preflight/preflight-gates.json` with SHA-256 `e9b37f1719c666001f167bf6a18079c073b9cfa889bc83c194d2ce09e98b9391`. P0 through P6 are all `passed: true`.

The phase gate was tested in both directions. The historical Campaign 1 failed manifest still refuses acquisition and identifies its original P0/P2/P5 blockers. The audited Campaign 2 manifest authorizes acquisition, pilot, confirmatory, and drift. No phase was executed by this authorization test.

## P0 repair and scientific boundary

Campaign 2 uses the observable controlled ChatGPT product configuration rather than fabricating an unavailable private deployment identifier. The subject surface is ChatGPT web, model `5.6 Sol`, reasoning `Extra High`, brand-new Temporary Chat, Chat mode, Business workspace, no project context, no File Library context, no prior trial attachment, and zero prior trial transcript.

The observable-configuration fingerprint is `afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5`. The formal P0 evidence explicitly records that the private deployment identifier is not exposed and no private-backend equality claim is made.

## P2 authenticated browser proof

The live eyeBROWSE Program Host profile is authenticated to GitHub as `StealthEyeLLC`. The proof action remains confined to `StealthEyeLLC/world-kernel-build-001-fixture`; the browser receipt is not treated as Outcome. Independent provider observation established the material commit and branch state. Fixture `main` remains at the original baseline `519d05879314cab45280a9f58efbd8859ecd8d64`.

The audited live P2 observation was refreshed after the execution hardening and still showed authenticated GitHub state and semantic browser controls. No production repository or sibling-Eye repository was used for the browser write proof.

## P5 fresh-subject repair and freeze

The failed P5 attempts remain preserved:

- `campaign2-p5-probe-001`: the prompt reached the editor but did not produce a valid submitted subject invocation. No material action was dispatched.
- `campaign2-p5-probe-002`: PowerShell's case-insensitive `$Matches` automatic variable collided with a local accumulator. The attempt was invalidated; no material action was dispatched.
- `campaign2-p5-probe-003`: passed with a new Temporary Chat, zero prior messages, `5.6 Sol`, `Extra High`, machine-readable locked response, no fallback, and no material action.

The subject mechanism remains unchanged since the P5 attestation. The current normalized hashes match the formal P5 record exactly: base prompt `f4ce7079afc8bfdc02998f62db9d32d3853d5b2b36a4c7f45fc2a1289c4e8fe5`; tool contract `e1d2b899190ed0c0f88e1dce4fb2a73f653de20b9d8766df4e196cb4f883c181`; trial-output contract `8aec3ac079db715f8d6990d52a8cd9f7d275e4e3ded2efa1088ef101d9d5c193`; subject driver `b2c6e52e4a44eff7e46ebdbb6e97d5ec706110698362961106ffe96ded27ab98`; invocation adapter `98d4bd52e90bf86564c502898403c8549c8a21e31f722c893c1f693e2334df46`.

## Execution-mechanics audit

The takeover audit found defects that ordinary regression did not expose. Each was repaired prospectively while the science boundary was still zero.

### Prospective evaluator registration

Each acquisition action slot receives a sealed hidden schedule outside the repository tree. Its commitment is written to the evaluator database before reset or subject cognition. The deterministic reset is independently reconstructed from native Git and provider state, and the evaluator requires the sealed recipe, reset manifest, independent fingerprint, provider identity, branch, policy regime, and fixture baseline to agree before a Prediction can be sealed for dispatch.

Registration is content-idempotent. A restart may reuse only an exact existing seed/reset registration; any mismatch fails closed.

### Reset and provider-control hardening

`fixture-reset.ps1` now establishes check and push policy deliberately around evaluator setup. `fixture-provider-admin.ps1` converges workflow state idempotently instead of treating an already-disabled workflow as an error. It obtains the already-authorized GitHub credential through Git's credential provider, exposes it only as process-local `GH_TOKEN` during provider administration, restores the prior environment afterward, and never serializes the token.

A PowerShell helper-name collision with the built-in `h`/history alias was found in the independent reset verifier and eliminated by using an explicit `Get-Sha256Bytes` helper.

### P6 v4

P6 was re-attested after every reset/provider-control change rather than carrying forward stale evidence. v2 and v3 remain as superseded engineering history. v4 binds the current reset stack.

Accepted-regime repeated resets produced the same fingerprint `df89027051be30dcd8494ac762e2c5f7cf769d053cc28c02965208a59cbf97b0`. Rejected/protected/stale repeated resets produced the same fingerprint `1e38ac0b261fd42d915937ab8ce429a386287600f54e1963fa1938e3bf9262ca`. The two regimes differ. Independent reobservation reproduced both final fingerprints and exact local/remote equality.

The provider behavior proof also performed real actions: the accepted fixture push landed at the provider; the protected push returned a real rejection and left the remote ref unchanged. Both branches were reset back to their deterministic state afterward.

### Provider-check temporal isolation

A live audit showed that GitHub workflow state changes are asynchronous: a reset/setup push can create a workflow run that becomes visible after the workflow is re-enabled. Therefore a same-SHA check observation cannot simply ask whether *any* run exists.

The final check observer is bounded by the durable action dispatch timestamp. GitHub provider data determines whether a run for the expected head was created at or after `dispatched_at`; eyeBROWSE separately observes the visible GitHub checks presentation. This prevents a pre-dispatch evaluator-setup run from being counted as action consequence while preserving the historical run as real evidence.

The negative audit (`audit-provider-check-v3/check-after-boundary.json`, SHA-256 `88fd4a44b3ca49265afcc6534dabca4423167c3563410fa098a9d56a3dc883ae`) found zero qualifying post-boundary runs. The positive audit created a real post-boundary accepted push; `audit-provider-check-v3-true/check-after-boundary.json` (SHA-256 `4cb734b9325491a8a51180f3c02c4d71d2ea709e226059a70c8fe06571f0a198`) observed one qualifying run, terminal success, and matching successful eyeBROWSE presentation.

### Crash recovery and append-only integrity

The acquisition controller is resumable by artifact and provider state. Seed/reset registration, evidence, manifestations, observations, claims, correspondence, Prediction lineage, and evaluator ground truth are exact-content idempotent. Begin recovery reconstructs an existing Action -> unique Prediction -> sealed dispatch boundary instead of creating a second Action. Material-action recovery either proves the intended action already occurred, proves the pre-state is unchanged and safely retries, or refuses an ambiguous state. It never blindly redispatches.

Close recovery uses the existing unique Action/Outcome/TransitionEpisode constraints. If database closure succeeded but the public artifact write was interrupted, the runner verifies the stored Outcome/Brier values against fresh provider evidence and reconstructs the same public `EpisodeExport` rather than opening another episode.

Receipt and Outcome remain distinct. Recovery receipts are explicitly labeled as recovered provider-state receipts; verified Outcome still requires fresh independent provider reobservation.

### Typed inheritance and arm parity

Acquisition closes a public `EpisodeExport` containing the provider-grounded experience required by the frozen serializers: observations, Claims with temporal provenance, correspondence, Prediction, Outcome, Brier components, deltas, evidence hashes, and TransitionEpisode identity. Hidden evaluator regimes are not included.

The frozen Memory and Structured serializers were not modified. Inspection confirmed that Conventional Memory already receives the same public episode facts, claims, valid/known time, correspondence, evidence lineage, Prediction, Outcome, and errors in chronological narrative form, while Structured receives the same source episode IDs in typed representation. The treatment therefore retains a representational difference rather than an information-quantity advantage.

## P1 refresh

The original P1 probe state had been consumed by later preflight work, so a fresh disposable worktree was reset at `wk-b001-c2-p1-live`. Exactly one uncommitted `p3_local_change=1` delta was created there. Live CODEeye observed that change through the `codeeye-dev` Program Host path. This produced the current P1 artifact rather than reusing a stale descriptor.

## Final prospective boundary

At the final audited gate:

- P0-P6: PASS;
- full regression: 13 PASS, 0 FAIL;
- subject P0/P5 hashes: unchanged from formal attestations;
- Campaign 2 evaluator science rows: 0;
- Campaign 2 measured Actions: 0;
- Campaign 2 TransitionEpisodes: 0;
- acquisition corpus: not started;
- pilot: not started;
- confirmatory: not started;
- drift: not started.

The audited implementation must be committed and pushed, then a separate Campaign 2 execution-freeze manifest must bind that exact commit and the preregistered scientific constants before any acquisition seed is opened.

## Pre-subject execution-freeze repair

The initial execution freeze was committed and provider-verified before acquisition at `c7762b38b969c586d330387eed1181ebbec913a8` with freeze SHA-256 `e094714b0dbe3681f4d30c32aec379643eb3369a9e289452ebf011aecad90667`.

The first acquisition action-slot seed (`campaign2-acquisition-block-seed-01-lc`, configuration block `c2-acq-01`) was durably inserted before the controller hit a mechanical CLR compatibility error: PostgreSQL `timestamptz` was returned by Npgsql as `System.DateTime`, while the new execution code directly cast the value to `System.DateTimeOffset`.

No ChatGPT subject had been invoked. No Prediction existed. No Action was declared or dispatched. No provider Outcome or ground truth existed. No scientific result had been observed. The sealed hidden schedule and its commitment were not changed.

The bounded repair therefore changes only timestamp materialization in the Campaign 2 execution plumbing. All PostgreSQL timestamp reads now pass through one deterministic UTC coercion helper, including retry and recovery paths. The freeze validator also distinguishes the original pre-acquisition freeze from a narrowly scoped replacement freeze that is permitted only before the first subject invocation, only with zero scientific outcomes, and only when it explicitly names the superseded prospective freeze.

This repair changes no subject mechanism, prompt, arm representation, candidate episodes, metric, scorer, action class, hidden schedule, fixture difficulty, statistical procedure, behavioral gate, drift gate, or hypothesis. The exact already-committed seed is retained rather than replaced.

Durable engineering evidence: `experiments/build001/campaign-2/acquisition/engineering/pre-subject-timestamp-repair.json`.