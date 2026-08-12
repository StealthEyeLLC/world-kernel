# Campaign 2R — Run A Failure Analysis

Date: 2026-08-11
Run A terminal commit: `4bc35bac9ee20268f3ce2966fc62f2c2ed4cff0b`
Run A classification: **FAILURE**

## Immutable historical facts

Campaign 2 / Run A remains closed and immutable. The historical `Complete Campaign 2 acquisition` commit remains evidence of an orchestration overclaim; the later provider/database reconstruction proved acquisition had not completed.

Run A produced zero valid acquisition TransitionEpisodes, zero Campaign 2 ActionAttempts, zero persisted Predictions, zero Outcomes, zero PredictionEvaluations, zero TransitionEpisodes, zero material action dispatches, zero provider Outcomes, zero pilot blocks, zero confirmatory blocks, and zero drift blocks. Therefore Run A did not measure Structured versus Conventional Memory in either direction.

The fresh-subject mechanism itself succeeded: ChatGPT web, 5.6 Sol, Extra High, brand-new Temporary Chat, locked base prompt/tool contract, no prior trial transcript, no project/File Library context, and machine-readable operator output.

## Decisive failure

The first successful fresh subject output was followed by `campaign2-begin`. PostgreSQL rejected Claim construction with SQLSTATE `23514`:

`material/provider Claim lacks subject-matched Observation/Evidence lineage`

`wk.validate_claim_lineage` behaved correctly. The frozen Run A `Campaign2Execution.cs` created the pre-state `github:remote_ref_head` Claim with the hosted GitHub Manifestation as subject while supplying the local working-copy Observation/Evidence as its primary lineage.

This was cross-manifestation evidence laundering:

local working-copy evidence -> hosted GitHub Claim

No false hosted Claim was persisted.

## Root cause reconstructed from source

The Run A state observer already executed an exact provider query:

`git ls-remote --heads origin refs/heads/<branch>`

and retained its command/result inside the state observation. The defect was not absence of provider truth. The defect was persistence: the combined state artifact was represented only as a local working-copy Observation/Evidence and that local lineage was reused by the hosted-ref Claim.

The same semantic mistake also existed in public EpisodeExport provenance: `github:remote_ref_head` fell back to the local state-evidence hash.

## Scientific consequence

Run A's classification remains **FAILURE**, not `NO VALID CONFIRMATORY CONCLUSION` and not `PARTIAL PASS`. Its precise meaning is that the frozen Run A execution implementation could not construct the first valid acquisition lifecycle while obeying the kernel's own Claim-lineage constraint.

Campaign 2R is therefore a new prospective Build 001 rerun. It does not salvage Run A cognition, seeds, state, or result rows.