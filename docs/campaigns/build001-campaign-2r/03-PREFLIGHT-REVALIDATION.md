# Build 001 Campaign 2R — Preflight Revalidation

Date: 2026-08-11
Branch: `build001-campaign-2r`
Authority: `docs/campaigns/build001-campaign-2r/00-AUTHORITY.md`

## Status

Campaign 2R remains prospective. Run A is immutable and is not salvaged. No Campaign 2R scientific Action, Prediction, Outcome, Evaluation, TransitionEpisode, seed commitment, ground-truth row, campaign-scoped Claim/Correspondence, or acquisition artifact existed at the final preflight boundary.

## Lineage repair validation

The committed Claim-lineage repair at `6617d161682454cd0647fa4f0531299cfaa22cd4` remains the governing repair. The final dry six-action harness was rerun with one fresh isolated PostgreSQL database per semantic action. Each action produced exactly one non-scientific Action and Prediction and zero Outcomes, Evaluations, or TransitionEpisodes. The complete lifecycle sentinel was separately exercised by the integration test suite and is explicitly non-scientific with no Campaign 2R campaign ID.

The Claim-construction, six-action pre-dispatch, post-action Claim, and EpisodeExport leakage audits all pass against the final relevant source hashes.

## Current GitHub UI compatibility repair

Fresh P2 revalidation found a mechanical GitHub UI compatibility change: the GitHub file editor is currently exposed as a visible CodeMirror `contenteditable` surface, and the eyeBROWSE query result shape may be a direct array with PascalCase element properties. `scripts/eyebrowse-github-remote-commit.mjs` was adapted only to bridge those current presentation details. The semantic action, authenticated eyeBROWSE authority, target repository, independent provider verification, and preregistered success criteria are unchanged.

The first disposable P2 branch write that could not be correlated to the active local browser receipt was excluded. The credited P2 proof is the separately correlated write on `wk-b001-c2r-p2-live2`, commit `9b6d4b530a4d67209b517f33a8f15db55d5e440b`, independently reobserved through GitHub provider authority.

## Subject-configuration revalidation

Fresh signed-in Edge observations re-established the original frozen observable configuration exactly:

- ChatGPT web, GPT-5.6 Sol, Extra High;
- Temporary Chat, Chat subject mode, Business workspace;
- no project context, File Library context, prior trial transcript, or prior trial attachment;
- account memory enabled but not used or created by Temporary Chat;
- record-history reference enabled;
- four frozen Personalization text fields (Custom instructions, Nickname, Occupation, More about you), all empty;
- Default base style/tone and Default Warm, Enthusiastic, Headers & Lists, and Emoji characteristics;
- Fast Answers off.

The resulting canonical observable-configuration fingerprint is the preregistered value:

`afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5`

P5 was rerun in a fresh Temporary Chat using that exact fingerprint and passed.

## Fresh P0–P6 result

The final formal preflight evaluator reports all P0–P6 gates passed. The current gate artifact is `artifacts/campaign-2r/preflight/preflight-gates.json`.

The final implementation regression suite passes 16/16 with zero build warnings. Projection rebuild, immediate-stop/WAL recovery, PostgreSQL topology, authenticated eyeBROWSE, native Git accepted/rejected/fetch/ff-only behavior, and deterministic provider reset were all freshly revalidated.

## Prospective boundary

`artifacts/campaign-2r/preflight/scientific-boundary-zero.json` records zero Campaign 2R scientific counts. `artifacts/campaign-2r/preflight/prefreeze-final-audit-manifest.json` binds the final prefreeze source/evidence hashes.

No acquisition may begin until this preflight/mechanical-compatibility state is committed and pushed, independently verified from GitHub, and then bound by a separate prospective execution-freeze commit while the Campaign 2R scientific counters remain zero.