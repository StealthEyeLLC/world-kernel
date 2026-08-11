# Build 001 implementation boundary

The complete, controlling human specification is preserved unchanged at:

`docs/preregistration/original/StealthEye_World_Kernel_Build_001_Spec_and_Preregistration.md`

This implementation instantiates only the frozen Build 001 slice:

- immutable content-addressed Evidence;
- provider acquisitions as Observations;
- bitemporal Claims and append-only ClaimDispositions;
- conservative `git:working_copy_of` CorrespondenceClaims and append-only dispositions;
- ActionAttempts and ordered ActionPhases;
- complete, frozen proposition-vector Predictions committed before dispatch;
- fresh-evidence Outcomes and proper-score PredictionEvaluations;
- one TransitionEpisode per measured semantic action;
- deterministic structured and conventional-memory packages from identical source episodes;
- an evaluator store physically and logically separate from operator-visible kernel state.

State is a rebuildable projection, not a persistent primitive. The implementation deliberately has no universal Entity, State, Event, Rule, Hypothesis, Capability, Plan, Experiment, Skill, embedding, graph-node, or graph-edge table.

Native providers remain authoritative. Git owns Git state, GitHub owns hosted state, filesystems own bytes, and browser runtime owns presentation. The kernel records what was captured, claimed, predicted, and later evaluated; it does not become material authority.

