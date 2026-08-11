# Locked preregistration summary

Machine-readable authority: `docs/preregistration/original/StealthEye_World_Kernel_Build_001_Preregistration.json` (`cf4de0ea97cd394ec8ae9373617d253b9b220be11a6584d30380b0051a617b10`).

## Primary design

- Arms: Cold, Conventional Memory, Structured Treatment.
- Primary contrast: Structured Treatment versus Conventional Memory.
- Primary unit: matched configuration block, not individual actions.
- Primary loss: block-mean Brier loss over every locked proposition.
- Strong-support target: at least 20% relative reduction in mean Brier loss.
- Interval: paired block bootstrap, 10,000 resamples, 95% interval.
- Test: locked paired randomization test; required threshold `p < 0.05` where specified.
- Maximum inherited package: 6,000 estimated tokens and 32,768 bytes; pilot may reduce it but may not raise it above 8,000 tokens.
- Pilot: exactly 12 nonconfirmatory trials, excluded from confirmatory analysis.

## Semantic actions

- `git:create_local_commit`
- `git:create_branch`
- `git:push_ref`
- `github:create_remote_commit`
- `git:fetch_remote`
- `git:integrate_fast_forward`

Each action uses its exact frozen proposition vector. The evaluation-spec hash is bound to the ActionAttempt before any Prediction is inserted, and dispatch is rejected unless the vector is complete, eligible, and durably earlier than dispatch.

## Lock boundary

The original preregistration is already immutable. If a permitted pilot-only mechanical amendment is necessary, it must be versioned, justified, hashed, and committed before any confirmatory seed or outcome is exposed. The first accepted confirmatory dispatch seals the confirmatory lock in the evaluator store.

