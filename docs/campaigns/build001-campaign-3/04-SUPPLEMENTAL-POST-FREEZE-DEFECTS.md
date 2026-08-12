# Campaign 3 Supplemental Post-Freeze Frozen Execution Defects

Date: 2026-08-12
Campaign: `build001-campaign-3`
Authority: post-freeze evidence only; no source repair; no replacement freeze; no scientific invocation
Terminal disposition: **ABORTED AFTER PROSPECTIVE FREEZE, BEFORE FIRST SCIENTIFIC SUBJECT**

## Purpose

Campaign 3 was already terminally aborted at commit `95c2f88b764415d5c7c576ff285363737344cee7` after the prospectively frozen science-authorization boundary failed. This supplement preserves two additional execution-significant defects discovered by read-only inspection of the same frozen bytes. It does not alter that disposition and does not authorize a Campaign 3 repair.

## Frozen boundary

The single prospective freeze is commit `648f1343c3355bd4c1f60529c7366055004b2d27`, tree `c4c956e6259719dd7074776da65ce70b676e196c`, with manifest SHA-256 `4c39cf3ae6b57e2fd4143650513dc6f24bd1c81b6f528af1bb57a870c6cf23ac`. Its implementation is commit `336894b8245c4b21ece26b90e15ed643f7b0f32b`, and its frozen observable subject-configuration fingerprint is `b11c931b9fbcda0a1e21587f74233b54a84c50e603bc0d17c7d8e933a0eeba75`. The manifest forbids post-freeze source repair.

## Supplemental defect 1 — stale configuration fingerprint

The frozen acquisition controller hardcodes `afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5` at `scripts/campaign3-acquisition-controller.ps1:33`. It passes that value to the subject-request builder at line 265. `scripts/campaign3-new-subject-request.ps1:65` writes the supplied value into `observable_configuration_fingerprint_sha256`.

That value differs from the prospectively frozen Campaign 3 fingerprint `b11c931b9fbcda0a1e21587f74233b54a84c50e603bc0d17c7d8e933a0eeba75`. Therefore a scientific subject request produced by the frozen controller would attest the wrong observable configuration fingerprint.

## Supplemental defect 2 — strict science authorization is not wired into acquisition

`src/WorldKernel.Build001/Campaign3Boundary.cs:7-43` defines the strict Campaign 3 science-authorization schema and `PassesScienceAuthorization`, binding authorization to the freeze, provider identity, zero state, frozen subject fingerprint, preflight, and external Eye heads.

The operational acquisition controller instead calls only `phase-authorize` against `preflight-gates.json` at `scripts/campaign3-acquisition-controller.ps1:159`. `src/WorldKernel.Build001/Program.cs:611-615` implements that command solely as `PreflightGateEvaluator.EnsurePhaseAuthorized(preflight-manifest, phase)`. A source-wide search across `src` and `scripts` finds no operational caller of `PassesScienceAuthorization`; only its definition exists.

Thus the frozen acquisition entry path can pass preflight phase authorization without consuming the stricter post-freeze science-authorization record. This compounds the already-recorded post-freeze authorization defect; it does not permit a Campaign 3 repair.

## Frozen-source identity

A Git diff from frozen implementation commit `336894b8245c4b21ece26b90e15ed643f7b0f32b` through terminal abort commit `95c2f88b764415d5c7c576ff285363737344cee7` shows zero changed paths under `src` or `scripts`. Git blob identity was also verified for the acquisition controller, subject-request builder, Campaign3Boundary, ExperimentControl, and Program sources. The defects therefore belong to the frozen implementation rather than post-freeze source drift.

## Scientific boundary remains zero

At the supplemental check there were zero subject requests, zero subject results, and no acquisition directory. A fresh zero-state verification again reported zero kernel scientific rows, zero evaluator rows, zero scientific files, zero subject results, zero episode exports, and zero hidden evaluator result files. The temporary zero-state evidence hash was `7056ac05f1a64414b8667c45e5b2f6ded3883a6131ad7b8a2c4a15d1a6cf3ab0`.

## Next boundary

A provider ref named `build001-campaign-4` was observed pointing exactly at the Campaign 3 terminal abort commit `95c2f88b764415d5c7c576ff285363737344cee7`; no distinct Campaign 4 commit was observed during this record. This supplement does not authorize or begin Campaign 4 execution.

Campaign 3 remains terminal. Any repair must occur prospectively in a fresh campaign boundary.