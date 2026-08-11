# Build 001 Campaign 2 preflight repair

Date: 2026-08-11
Status: P0–P6 PASS; no acquisition, pilot, confirmatory, or drift execution occurred before this record.

## Boundary

Campaign 1 remains permanently classified **NO VALID CONFIRMATORY CONCLUSION**. Its result is preserved at commit `29851025b0f11034a276cd1259651ee4fec5ed0e`; `docs/05-BUILD-001-RESULTS.md` has SHA-256 `b14df79d5d4c34be33501eea6f848503587dc0649ce49ad2eb06db0aaaf741e2`.

Campaign 2 is the prospective rerun authorized by `00-AUTHORITY.md` (SHA-256 `9a6f7ca2e976f0f0e6dd763152d3e2d26220ce9ff05763590105a59df00c1041`). It repairs only P0, P2, and P5 mechanics. It does not redesign the architecture, alter the frozen hypothesis, change any arm or endpoint, mutate a sibling repository, or waive any gate.

The Campaign 1 artifact directory `artifacts/preflight` was not modified. Campaign 2 uses `artifacts/campaign-2/preflight`; its lineage record has SHA-256 `77ec8ffcce0a32220735a953c35042ebb0ead156fab1395dd3b679f3cfee4923`.

## Repair results

| Gate | Result | Evidence |
|---|---:|---|
| P0 observable baseline | PASS | `p0-baseline.json`, `b55fa393290cb349fdab1fe4a45c86ce3a2efc34152efa60810c9e15ee2e7672` |
| P1 live CODEeye | PASS | Unchanged valid Campaign 1 evidence, `1d5daea17f491d69d7d421b3ce6a717823c193e2ad60c4454b7cc96c825c8b7a` |
| P2 authenticated eyeBROWSE | PASS | Live browser `f8aae80ccea0cb6784591575964628748e0c44131617294d9c8a88c59635f506`; write attestation `e469cd6fddb95299f2d6bca329119993a06fbc593baaf1a669a9325c2b55ea03` |
| P3 experiment-owned Git facet | PASS | Unchanged valid evidence beginning with `p3-native-git-observation.json`, `35a9ebe8e4f621aa5f853a847d586b57177bc55c4f7b4657ee6c52a65efcc6d6` |
| P4 PostgreSQL, integrity, and recovery | PASS | Campaign 2 regression: 12 passed, 0 failed, `45dd4c4ef6baac552a8c7193c2ec00d1c34d949a2765d47f393bd1bd6a9d8b1d` |
| P5 fresh isolated invocation | PASS | `p5-fresh-invocation.json`, `104968ac005fb9b77ba305f9bde4d54cbbcaf34d746d68a7d7e79345a7af9934` |
| P6 deterministic reset | PASS | Unchanged valid evidence, `6584decfec10d826688ed11277f5de97f905856f4740605ae8ab6caf439ba4d8` |

The repository's `PreflightGateEvaluator` produced `preflight-gates.json` with SHA-256 `63e1e460ffb16396db66477e775ea6c500936ebc56308fad9ad3ca13c46d32d0`. Every gate is `passed: true`; the manifest contains no failed gate.

## P0 repair

The subject is ChatGPT web in a brand-new Temporary Chat, observable model `5.6 Sol`, reasoning `Extra High`, Chat surface, and Business workspace. Project context, file-library context, prior attachments, and prior messages are absent. Base style and tone and all style characteristics are Default; Fast answers is off; four custom-instruction fields are empty and frozen. Account memory settings are transparently recorded as enabled, while Temporary Chat does not use or create memory.

The canonical observable-configuration fingerprint is `afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5`. No private serving deployment identifier is exposed, fabricated, hashed, or used in an equality claim. Equality is claimed only for the observable configuration.

P0 evidence was frozen at commit `4e4ce7bd3d67bcf1a3bd5780df5fde258cd63d2a`.

## P2 repair

The live Program Host probe observed the existing eyeBROWSE `dev` profile signed in to GitHub as `StealthEyeLLC`, exit code 0, with semantic controls available. The browser-created proof is confined to `StealthEyeLLC/world-kernel-build-001-fixture`:

- branch: `wk-b001-c2-p2-auth-proof`;
- commit: `96dd6c21a2f1ce9304020331a7c7930a128c4986`;
- base/main: `519d05879314cab45280a9f58efbd8859ecd8d64`;
- changed path: `fixture/campaign2-p2-authenticated-browser-proof-20260811.txt` only.

The GitHub connector independently fetched the commit, compared main with the proof branch, confirmed the branch is one commit ahead and zero behind, confirmed the file on the proof branch, and received 404 for that file on main. Main remained unchanged. The provider observation has SHA-256 `4708e8a4acbe9482d9573dc2eccf14eca5c94e3a29ad9372c4c925fac805c73a`.

P2 evidence was frozen at commit `19a3ac9b0787a28dca640244d19c97d3cd173b60`.

## P5 repair

The adapter opens one new Temporary Chat, verifies zero prior messages and attachments, verifies no project or file-library context, verifies `5.6 Sol` and `Extra High`, submits the exact locked prompt, waits for a machine-readable response, validates the response contract, and rechecks observable configuration after the response. The mechanism does not persist or inject prior arm transcripts, cross-arm memory, hidden evaluator labels, or chain of thought.

Two preflight attempts were invalidated before an acceptable subject response:

- `campaign2-p5-probe-001`: prompt submission timed out; no subject response was accepted and no material action was dispatched.
- `campaign2-p5-probe-002`: PowerShell's case-insensitive automatic `$Matches` variable collided with a `$matches` accumulator; the adapter error invalidated the attempt and no material action was dispatched.

The bounded repairs generated a harmless editor input event before semantic Send invocation and renamed the colliding accumulator. Prompt, arm package, tool contract, output contract, metric, and experiment meaning did not change.

`campaign2-p5-probe-003` passed. It started with a new Temporary Chat and zero prior messages, parsed the locked JSON response, observed no product fallback, retained `5.6 Sol` / `Extra High`, and dispatched no material action. The raw result has SHA-256 `a41bc1ffd658fcc17d5dd450da6245025200a022bf2fc1d6ad06447d60f491e7`; the formal attestation has SHA-256 `104968ac005fb9b77ba305f9bde4d54cbbcaf34d746d68a7d7e79345a7af9934`.

P5 evidence was frozen at commit `bb10549a2da7d7bacfa7f88e283d7effdabd83ff`.

## Prospective phase boundary

The read-only boundary observation has SHA-256 `b31285619c2011a5be4d0374cd3280a904ba999135f77b00f47cf53ae1e60bc8`. At capture, the evaluator database contained only the original `v1-original` freeze event and:

- seed commitments: 0;
- arm randomizations: 0;
- invocation attestations: 0;
- ground-truth rows: 0;
- aggregate results: 0.

No acquisition, pilot, confirmatory, or drift phase had started, and no confirmatory outcome had been inspected. The Campaign 2 amendment must be hash-frozen and committed before phase dispatch.
