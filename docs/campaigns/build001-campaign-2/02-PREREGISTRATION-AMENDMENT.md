# Build 001 Campaign 2 preregistration amendment

Date: 2026-08-11
Status: prospective and frozen before acquisition

## Purpose

This is a mechanics-only supplement to the immutable Build 001 preregistration. It authorizes a clean Campaign 2 rerun after P0, P2, and P5 were repaired and all P0–P6 gates passed. It does not supersede, relax, reinterpret, or replace the original scientific contract.

The original remains bound by:

- human specification SHA-256 `63804abcc376c4e2c27f242bee16e10678458b1befd5d1e4903ff54fa2d87696`;
- machine preregistration SHA-256 `cf4de0ea97cd394ec8ae9373617d253b9b220be11a6584d30380b0051a617b10`;
- evaluation specification SHA-256 `f182f6b0ac91d85436f077f0c78e3db0ec3b35d2f15f82d0675b699598c93ded`;
- freeze commit `22f9f8e459d71f8df271078575c920db9c6469b2`.

If this supplement conflicts with those frozen scientific requirements, the original controls and execution must stop.

## Historical separation

Campaign 1 remains **NO VALID CONFIRMATORY CONCLUSION** at commit `29851025b0f11034a276cd1259651ee4fec5ed0e`. It produced no acquisition corpus, pilot result, confirmatory block, or drift result. Campaign 2 must be reported separately and may not rewrite Campaign 1.

## Permitted mechanics changes

Only these changes are admitted:

1. P0 uses a canonical fingerprint of exposed ChatGPT controls and explicitly declines to invent an unavailable private deployment identifier.
2. P2 uses the already authenticated eyeBROWSE profile and confines its proof write to the disposable fixture repository.
3. P5 uses a brand-new Temporary Chat for every cognitive invocation and validates the locked machine-readable response before dispatch.
4. Campaign 2 artifacts are stored separately, with hashes and source lineage.

No Eye, CODEeye, or eyeBROWSE product architecture is changed. No kernel ontology, semantic action class, provider-authority rule, arm, primary endpoint, effect target, statistical test, behavioral co-gate, drift gate, or strong-pass criterion is changed.

## Frozen subject mechanics

Every cognitive trial must use observable model `5.6 Sol`, reasoning `Extra High`, the same base instructions, the same tool contract, and a new Temporary Chat with zero prior messages, no prior-trial transcript, no project context, no file-library context, no prior attachment, no cross-arm memory, and no hidden evaluator state.

The frozen observable-configuration fingerprint is `afc63aa5715471de2b59c12b9ca902fd5eef50eddc6c8df846c57f0442ff75e5`. The base prompt hash is `f4ce7079afc8bfdc02998f62db9d32d3853d5b2b36a4c7f45fc2a1289c4e8fe5`; the tool contract hash is `e1d2b899190ed0c0f88e1dce4fb2a73f653de20b9d8766df4e196cb4f883c181`; the trial output contract hash is `8aec3ac079db715f8d6990d52a8cd9f7d275e4e3ded2efa1088ef101d9d5c193`.

An invocation is invalid if freshness or isolation cannot be attested, observable model or reasoning differs, fallback is observed, the fixed hashes differ, the response cannot be parsed and validated, or the adapter errors. Invalid trials do not contribute outcomes. A material provider action may occur only after a valid prediction is durably recorded under the original prediction-before-dispatch invariant.

## Unchanged experiment

The arms remain Cold, Conventional Memory, and Structured Treatment. The primary comparison remains Structured Treatment versus Conventional Memory. The primary endpoint remains configuration-block mean Brier loss. The effect target remains at least 20% relative Brier reduction:

`(mean_memory_brier - mean_structured_brier) / mean_memory_brier`.

The analysis remains paired by configuration block, with 10,000 paired bootstrap resamples, a 95% confidence interval, and a paired randomization test with alpha 0.05. Cold remains secondary.

Memory and Structured Treatment must use identical source episode candidates and equal inherited budgets. Structured Treatment receives zero extra model calls. The candidate caps remain 6,000 tokens and 32,768 UTF-8 bytes; pilot tuning may reduce the budget but may not raise it above 8,000 tokens.

Acquisition remains 24 initial and at most 36 blocks, with the original per-action eligibility and accepted/rejected outcome minima. The pilot remains 12 blocks and is excluded from confirmatory analysis. The pilot may tune only the original enumerated mechanics. The confirmatory sample size remains pilot-derived:

`delta = 0.20 * mean_memory_pilot_brier`

`n_raw = ((1.959964 + 0.841621) * sd_paired_difference / delta)^2`

`N = ceil_to_multiple_of_8(max(48, n_raw))`, capped at 96; an infeasible or low-headroom pilot requires redesign and a new pilot before confirmatory execution.

The behavioral, integrity, fresh-instance, equal-information/compute, provider-authority, and drift co-gates remain unchanged. Strong pass still requires every original gate, including at least 20% primary improvement, confidence interval excluding zero in Structured Treatment's favor, paired-randomization `p < 0.05`, behavioral value, and the preregistered drift response.

## Phase order and disclosure

The phase order is unchanged: preflight, acquisition, pilot, permitted pilot-derived freeze, confirmatory, then drift. No confirmatory seed, hidden configuration, arm randomization, ground truth, outcome, or aggregate result may be inspected early. Pilot data cannot enter confirmatory analysis.

At amendment time, `artifacts/campaign-2/preflight/preflight-gates.json` has SHA-256 `63e1e460ffb16396db66477e775ea6c500936ebc56308fad9ad3ca13c46d32d0` and P0–P6 all pass. The pre-acquisition boundary artifact has SHA-256 `b31285619c2011a5be4d0374cd3280a904ba999135f77b00f47cf53ae1e60bc8` and records zero experimental evaluator rows beyond the original freeze event.

No phase dispatch is permitted until this document and `experiments/build001/campaign-2/preregistration.json` are hashed, committed, pushed, and named by a separate freeze manifest.
