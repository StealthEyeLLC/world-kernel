# StealthEye World Kernel Build 001 — Campaign 2 Confirmatory Results

Date: 2026-08-11
Campaign: `build001-campaign-2`
Classification: **FAILURE**

## 1. Authority

This document records Campaign 2 as actually executed. It does not authorize Build 002, canonicalization, architecture redesign, or sibling-Eye redesign.

## 2. Acquisition publication checkpoint

The local checkpoint `0136bb24180194571970cc59be87ec3cd8a2dea4` (`Complete Campaign 2 acquisition`) was safely fast-forward published after proving remote ancestry. Live reconstruction immediately afterward showed that the commit message overstated the scientific state: only configuration block `c2-acq-01` existed, `controller-status.json` was still `running`, and PostgreSQL contained zero Campaign 2 ActionAttempts, Predictions, Outcomes, or TransitionEpisodes. No `coverage.json` or `acquisition-complete.json` existed.

The historical commit is preserved. Its acquisition claim is not treated as scientific truth.

## 3. Frozen preflight state

P0-P6 remained PASS at the last prospective Campaign 2 freeze. `first_confirmatory_block_started` remained false. Eye, CODEeye, and eyeBROWSE repository HEADs matched the frozen manifest. PostgreSQL was live and the CODEeye program host successfully observed the prepared fixture worktree.

## 4. First acquisition slot

The first slot was `c2-acq-01-lc` / `git:create_local_commit` under seed `campaign2-acquisition-block-seed-01-lc`.

Three fresh Temporary Chat attempts failed before Send/operator output because the frozen driver required exact prompt read-back while the ChatGPT editor normalized CRLF to LF. A read-only diagnostic proved that CRLF-normalizing the expected prompt produced exactly the editor prompt hash: `8ef0929fb5c3b6f436e37b24ed49a308d88e1692aefe2cabc34cc0217bb6da6c`.

A preserved same-seed retry changed no frozen driver/adapter/base prompt/tool contract and normalized only CRLF to LF inside the request's current-observation string. The visible prompt was therefore identical to the editor text already observed. That fresh subject passed with model `5.6 Sol`, reasoning `Extra High`, a machine-readable response, and zero invalidation reasons.

## 5. Decisive execution-integrity failure

`campaign2-begin` then failed before any material action dispatch with PostgreSQL error:

`23514: material/provider Claim lacks subject-matched Observation/Evidence lineage`

The failure is correct enforcement by `wk.validate_claim_lineage`. Frozen `Campaign2Execution.cs` attempts to create the pre-state `github:remote_ref_head` Claim with the GitHub remote Manifestation as subject while using the local working-copy Observation/Evidence as primary lineage. The kernel rejected the mismatched subject/Observation lineage.

After the failed begin:

- Campaign 2 ActionAttempts: **0**
- Campaign 2 Predictions persisted: **0**
- Campaign 2 Outcomes: **0**
- Campaign 2 TransitionEpisodes: **0**
- material action dispatches: **0**
- provider Outcomes revealed: **0**

No false provider Claim was persisted.

## 6. Why Campaign 2 stops here

The original preregistration permits replacement of a block when a genuine harness/database failure prevents a valid trial record. But successful replacement still requires a harness that can execute the frozen lifecycle. Fixing the Claim-lineage defect requires changing frozen execution behavior after an operator output has already been produced. The original preregistration states that a material post-freeze change creates a new preregistration/campaign boundary and cannot be applied retroactively to salvage a result.

Campaign 2 therefore stops rather than weakening the kernel hard gate, rewriting evidence, modifying the frozen implementation, or pretending the acquisition checkpoint contained 24/144 valid experience.

## 7. Pilot

Not started. `03-PILOT-FREEZE.md` was intentionally not created because the 12-block pilot boundary was never reached.

Pilot blocks: **0**  
Cold mean Brier: **N/A**  
Memory mean Brier: **N/A**  
Structured mean Brier: **N/A**  
Mbar: **N/A**  
s_d: **N/A**  
n_raw: **N/A**  
final N: **N/A**

## 8. Confirmatory experiment

Not started. Confirmatory seeds were not opened in this continuation and `first_confirmatory_block_started` remains false.

Matched blocks: **0**  
Cold mean Brier: **N/A**  
Memory mean Brier: **N/A**  
Structured mean Brier: **N/A**  
Relative Structured improvement: **N/A**  
95% paired bootstrap CI: **N/A**  
Randomization p: **N/A**

## 9. Behavior, completion, and consequential errors

No confirmatory behavioral data exists. There were no Campaign 2 material actions or consequential mutations in the failed acquisition slot.

## 10. Drift

Not started. No qualifying drift blocks exist.

## 11. Identity, temporal, and epistemic integrity

The decisive database rejection is positive evidence that the kernel's subject-matched Claim/Observation/Evidence lineage hard gate remained active. No false hard correspondence, receipt-to-Outcome substitution, prediction-to-Observation laundering, stale-to-fresh promotion, or cross-arm contamination was observed in this failed Campaign 2 execution. These facts do not substitute for the unrun confirmatory gates.

## 12. Arm parity

Not evaluable because no Memory/Structured packages or paired arm runs were created.

## 13. Remaining hostile cases

Not executed because the campaign failed before acquisition completion.

## 14. Overhead

No valid Campaign 2 episode-level overhead dataset exists. PostgreSQL, CODEeye, and eyeBROWSE program-host processes were live at adjudication; provider wait/experiment overhead statistics were not generated.

## 15. Reproduction and evidence

Primary durable evidence is under:

- `experiments/build001/campaign-2/acquisition/engineering/post-publication-live-reconstruction.json`
- `experiments/build001/campaign-2/acquisition/engineering/editor-newline-normalization-diagnostic.json`
- `experiments/build001/campaign-2/acquisition/blocks/c2-acq-01/lc/trial-invalidation-and-blocker.json`
- the preserved original/replacement/retry subject artifacts in the same trial directory
- `experiments/build001/campaign-2/acquisition/controller-status.json`

## 16. Exact Campaign 2 classification

**FAILURE**

This is an execution-integrity failure before the first valid acquisition TransitionEpisode. It is not a measured failure of Structured versus Conventional Memory, because the treatment comparison never began.