# StealthEye Persistent World Kernel - Build 001 Specification and Preregistration

**Build:** 001 - Grounded Cross-Eye Transition Inheritance Slice  
**Specification date:** 2026-08-11  
**Phase:** implementation-specification and experimental-preregistration only  
**Architecture basis:** two independent first-principles research passes, two independent cross-pass syntheses, and final adjudication  
**Primary cognitive operator:** one frontier ChatGPT invocation at a time  
**Initial machine:** `STEALTHEYELLC`  
**Initial Eyes:** `CODEeye` and `eyeBROWSE` only  
**Implementation authorization:** NONE in this document  
**Canonicalization authorization:** NONE in this document  
**Status:** candidate Build 001 specification ready for a separately authorized implementation phase

> `FROZEN` in this document means frozen for the candidate Build 001 implementation and confirmatory experiment. It does not mean canonical StealthEye architecture.

---

## 0. Mission

Build 001 exists to answer one narrow question:

> **Can a completely fresh ChatGPT invocation inherit provider-grounded, temporally structured cross-Eye transition experience and use it to predict and operate the same real digital world measurably better than an equivalent fresh invocation supplied with materially equivalent conventional episodic memory?**

The build must not try to prove a mature world model, planning, causality, autonomous experimentation, or self-improvement in general.

The slice is successful only if the structured treatment produces measurable value beyond ordinary memory while preserving the original Eye architectural constraints and sibling-substrate boundaries.

The build is explicitly allowed to conclude that the architecture should shrink.

---

## 1. Governing authority and original Eye constraints

Build 001 inherits the original Eye-line doctrine rather than creating a special world-model doctrine.

The five project-level constraints already frozen in `CODEeye` and `eyeBROWSE` remain exhaustive in spirit for this experiment:

1. **No extra safety.** Do not add a world-kernel permission system, approval layer, confirmation ceremony, privilege tier, or artificial authority restriction merely because the proposed capability is powerful.
2. **No extra guardrails.** Do not add policy engines, general allow/deny systems, risk classes, capability gates, restricted modes, or similar permanent machinery.
3. **No theater.** Every component must buy actual predictive value, continuity, correctness, performance, or experimental falsifiability.
4. **No verification architecture.** Do not create verifier agents, separate verification pipelines, mandatory double-check stages, proof-generation agents, or reviewer swarms. Provider re-observation and prediction scoring are intrinsic to the world-learning capability and are not a separate verification architecture.
5. **No receipts/action ledger.** Do not turn the kernel into a general audit, compliance, receipt, or execution-history system. Persist only the evidence, predictions, outcomes, and transition episodes intrinsically required to test and later use empirical world dynamics.

### 1.1 Narrow justification for persistent transition history

Persistent history is permitted in this candidate only because the project hypothesis is impossible to test or use without preserving action-conditioned experience across fresh ChatGPT invocations.

The following boundary is hard:

- the kernel records only explicit world-learning episodes and epistemic records needed to reconstruct them;
- it does **not** hook or mirror every CODEeye, eyeBROWSE, Eye, shell, desktop, or provider action;
- unmeasured fixture setup and unrelated Eye operations do not become permanent world-kernel records;
- no compliance UI, receipt export, proof-of-action mechanism, signing system, approval workflow, or general audit API is part of Build 001;
- primitive action traces are retained only when required to establish the semantic action actually executed or to resolve its outcome.

If persistent history stops buying predictive/operational capability, it must be reduced rather than defended as provenance for its own sake.

### 1.2 Sibling boundaries

- `CODEeye` remains authoritative for agent-facing engineering continuity and engineering semantics.
- native Git remains materially authoritative for Git objects, refs, index, remotes, and working-copy state.
- `eyeBROWSE` remains authoritative for agent-facing browser continuity and browser semantics.
- Chrome/GitHub remain materially authoritative for browser and hosted-provider state in their respective domains.
- the world kernel owns only its epistemic records, correspondence assertions, prediction records, evaluations, and transition linkages.

The world kernel does not become a new universal Eye and does not absorb either sibling.

---

## 2. Live implementation baselines inspected for this specification

This specification is grounded in the current repositories and machine rather than the earlier research-era assumptions.

### 2.1 Repository baselines

- `StealthEyeLLC/eye` main baseline: `53948b74701f51c29c9322dfa9f017ba6b45f4a4`
- `StealthEyeLLC/CODEeye` main baseline: `1ca0f93d64bc20bccb3b96dbcda43a2232783609`
- `StealthEyeLLC/eyebrowse` main baseline: `2e27f44ebd3522d0d26b036dc57f790535df3533`

An implementation phase must record the actual SHAs it uses. If any baseline has advanced, it must inspect the changed contracts before assuming compatibility.

### 2.2 Relevant current CODEeye facts

Current shipped kernel/API supports, among other methods:

```text
workspace.attach
workspace.inspect
repo.status
world.sync
world.delta
git.diff
file.inspect
symbol.*
edit.*
refactor.rename
format
build.*
test.*
```

`repo.status` currently exposes:

```text
repository logical ID
worktree logical ID
local HEAD commit
HEAD ref / branch
detached state
working/index changes
index fingerprint
worktree fingerprint
```

Current public Build 001 RPC does **not** expose commit, push, fetch, remote-inspection, or fast-forward integration operations.

This is an implementation fact, not a reason to silently expand CODEeye's canonical API during World Kernel Build 001.

### 2.3 Relevant current eyeBROWSE facts

Current shipped SDK exposes:

```text
browser.status
target.list
target.open
observe.surface
observe.delta
query.find
inspect.element
action.click
action.fill
action.type
action.key
action.scroll
js.evaluate
wait.until
network.search
network.body
cdp.send
```

Build 001 of eyeBROWSE has already demonstrated persistent Chrome, logical target/document/element continuity, semantic observation/deltas, browser-side execution, network state, and Program Host operation.

### 2.4 Machine facts observed during specification

At specification time on `STEALTHEYELLC`:

```text
.NET SDK: 10.0.302
Git:      2.55.0.windows.3
Chrome:   151.0.7922.109 in the current eyeBROWSE runtime descriptor
Node:     portable Node 24.18.1 exists under C:\AgentBrowser\tools\...
Node:     not on the LocalSystem ambient PATH
Postgres: psql not currently discoverable/installed
```

The existing CODEeye/eyeBROWSE scheduled tasks and runtime descriptor files exist, but a descriptor can outlive its process. Therefore:

> **Live probe beats descriptor.**

A runtime descriptor is a locator/hint. It is never proof that a kernel/provider/browser endpoint is alive or fresh.

Every Build 001 trial preflight must probe the actual pipe/provider/browser state.

---

## 3. Build 001 thesis

The candidate slice is:

```text
provider-owned reality
      |
      +-- CODEeye adapter --------+
      |                           |
      +-- eyeBROWSE adapter ------+----> small temporal epistemic kernel
                                           |
                                           +-- structured current beliefs
                                           +-- reversible correspondence
                                           +-- pre-action predictions
                                           +-- observed outcomes
                                           +-- TransitionEpisodes
                                           |
                                      fresh ChatGPT invocation
```

The build tests whether this structure beats an equally informed ordinary-memory representation.

It does **not** test whether a fresh model beats a memoryless model. That comparison is secondary.

---

## 4. Hard non-goals

Build 001 must not implement:

- a universal Entity object;
- a universal State object;
- a universal Event ontology;
- persistent Rule or Hypothesis objects;
- automatic rule induction or promotion;
- a planner;
- active experiment selection;
- causal discovery;
- a capability/reliability self-model;
- skill generation or skill publication;
- a graph database;
- vector/embedding retrieval;
- a learned neural world model;
- a simulator router;
- DESKTOPeye integration;
- multi-agent cognition;
- a permanent local planner/model;
- multi-machine federation;
- a generic workflow engine;
- a second permanent Windows service;
- a world-kernel policy/approval subsystem;
- a global Eye action ledger;
- a universal Git/GitHub ontology.

Any implementation that needs one of these to make Build 001 pass has changed the experiment and must return to specification.

---

## 5. Four Build 001 milestones

### Milestone A - Grounded Epistemic Kernel

> The system can persist and replay provider-grounded Evidence, Observations, Claims, temporal dispositions, action/prediction/outcome records, and bitemporal belief projections without making the kernel authoritative for provider reality.

Acceptance emphasis:

- PostgreSQL-backed append-oriented records;
- immutable evidence blobs;
- valid-time versus record/knowledge-time queries;
- no inference/evidence laundering;
- prediction-before-dispatch integrity;
- rebuildable projections;
- no extra permanent service or autonomous brain.

### Milestone B - Conservative Cross-Eye Correspondence

> The system can represent one local Git working copy and its hosted GitHub counterpart as distinct material Manifestations connected by an evidence-backed, reversible `git:working_copy_of` relationship, while surviving decoys, renames, changed remotes, divergence, stale browser state, and path reuse without false unification.

Acceptance emphasis:

- zero false active correspondence in the deterministic hostile set;
- provider locators are not identities;
- shared commit identity does not merge repositories;
- ambiguity is valid.

### Milestone C - Scorable Transition Episodes

> Every measured material semantic action receives a structured prediction before dispatch, is executed through the owning Eye/provider facet, is freshly reobserved, receives a deterministic outcome and proper score, and closes one semantic TransitionEpisode.

Acceptance emphasis:

- six bounded semantic action classes;
- complete preregistered outcome proposition vectors;
- Brier scoring plus structured delta/invariant metrics;
- no provider receipt treated as success;
- no automatic learned rules.

### Milestone D - Fresh-Instance Inheritance

> Fresh ChatGPT invocations using structured world state outperform fresh invocations using materially equivalent conventional memory under a paired, preregistered real Git/GitHub experiment.

Acceptance emphasis:

- Cold, Memory, and Structured arms;
- same model and live Eyes;
- same acquisition corpus;
- equal inherited-context budget;
- primary comparison is Structured versus Memory;
- 20% relative Brier-loss reduction target;
- behavioral benefit and contradiction recovery;
- architecture reduction if structure does not justify itself.

Build 001 is not complete until all four milestone outcomes are classified.

---

## 6. Runtime and process topology

Build 001 must not create another permanent Windows SCM service.

The implementation topology is:

```text
fresh ChatGPT / trial operator
          |
          | replaceable trial gateway
          v
WorldKernel.Build001 process (.NET 10, restartable/on-demand)
          |
          +---- PostgreSQL 18 process (on-demand, loopback, no SCM service)
          |
          +---- immutable Evidence blob root
          |
          +---- CODEeye adapter
          |       +-- canonical CODEeye named-pipe RPC
          |       +-- experiment-only native Git facet
          |
          +---- eyeBROWSE adapter
                  +-- canonical eyeBROWSE named-pipe RPC
                  +-- browser-side GitHub lens/program

separate evaluator harness
          |
          +---- hidden fixture seed/configuration
          +---- provider-native ground-truth probes
          +---- arm randomization
          +---- locked scorer
```

### 6.1 World kernel lifetime

The kernel may remain alive throughout an acquisition/pilot/confirmatory campaign for performance, but persistence must come from its store, not process survival.

At least one acceptance test must kill/restart `WorldKernel.Build001` and reconstruct projections from persisted records.

### 6.2 PostgreSQL topology

Use PostgreSQL 18.x for the candidate Build 001 store.

Because PostgreSQL is not currently installed on the machine and the Eye canon favors minimal permanent processes:

- install/use it as an experiment-owned version-pinned runtime;
- do not register an additional permanent Windows service;
- listen only on loopback;
- launch and stop it under the experiment harness or equivalent explicit process ownership;
- keep the data directory under the Build 001 state root;
- persistence must survive a world-kernel process restart;
- PostgreSQL process survival is not considered world-kernel identity.

Recommended roots:

```text
C:\WorldKernel\Build001\State\        small durable relational state / pgdata
C:\WorldKernel\Build001\runtime\      runtime descriptors only
X:\WorldKernel\Build001\Evidence\     immutable bulk evidence
X:\WorldKernel\Build001\Temp\         disposable fixtures / exports
X:\WorldKernel\Build001\Evaluator\    hidden experiment state, not operator-visible
```

The exact paths may be changed in implementation if an existing StealthEye storage convention requires it, but the separation of durable state, bulk evidence, and hidden evaluator data is required.

### 6.3 Implementation language

For Build 001, **C# / .NET 10 is the preferred implementation choice and should be used unless the implementation phase discovers a concrete blocker.**

This resolves the synthesis-era language deferral using live facts:

- both CODEeye and eyeBROWSE kernels are .NET 10;
- the machine has .NET SDK 10.0.302;
- named-pipe and Windows process integration are native to the existing stack;
- using .NET avoids adding a second always-required application runtime;
- portable Node remains available for existing Eye Program Hosts where it already buys value.

A blocker may reopen this implementation choice without reopening the world architecture.

---

## 7. Eye/provider adapter contracts

### 7.1 CODEeye adapter

The adapter must use canonical CODEeye RPC whenever the current API exposes the required capability.

Required existing calls include at least:

```text
workspace.attach
repo.status
world.sync
git.diff
```

Other CODEeye calls may be used for fixture-file inspection or current engineering state if they contribute directly to a scored proposition.

### 7.2 Experiment-only native Git facet

The current CODEeye Build 001 API does not expose all Git operations required by this experiment. Build 001 must **not** modify CODEeye merely to make the world-kernel experiment possible.

Instead, the world experiment owns a narrow adapter named conceptually:

```text
ExperimentalCODEeyeGitFacet
```

It is not a third Eye and it is not canonical CODEeye API.

It may call stock `git.exe` directly for only the provider facts/actions required by the preregistered semantic action set.

It must:

- own no persistent identity or operating state;
- allocate no independent agent-facing logical IDs;
- treat native Git output as provider evidence;
- preserve CODEeye `repo_*` / `wt_*` identifiers when correlating local state;
- expose exact remotes/remote-tracking refs/object reachability missing from current public CODEeye RPC;
- execute only the six preregistered Git-side semantic actions needed by the experiment;
- return raw provider receipt/output separately from post-action state;
- invoke canonical CODEeye `world.sync` / `repo.status` after relevant material changes so the engineering Eye re-observes its world;
- remain experiment-owned and removable.

If Build 001 later demonstrates value, promotion of any Git facet into CODEeye requires a separate explicit CODEeye architecture/implementation decision.

### 7.3 Minimum native Git facet observations

Read-only provider facts may include:

```text
repository root / common dir
HEAD SHA
HEAD symbolic ref / branch
porcelain-v2 status
configured remotes (fetch and push URLs)
selected local refs
selected remote-tracking refs
exact object reachability
merge-base / ancestry predicates
Git version
```

Do not expose arbitrary shell output as a substitute for typed facts.

### 7.4 eyeBROWSE adapter

The adapter uses current eyeBROWSE capabilities only.

It must be able to:

- probe browser/kernel liveness;
- open or reuse the GitHub fixture target;
- observe current repo/branch/commit/check presentation;
- distinguish a stale rendered page from a fresh provider observation when possible;
- obtain exact commit/ref facts from browser-visible page/app/network state where the browser world exposes them;
- perform the one preregistered browser-owned mutation class, `github:create_remote_commit`;
- reobserve GitHub state after local pushes and remote changes;
- preserve target/document logical IDs only as eyeBROWSE identities, not world-kernel identities.

The adapter may use semantic UI actions, browser-side JavaScript, network state, or raw CDP according to eyeBROWSE's own representation/action doctrine. It must not bypass the browser world by calling the GitHub connector directly in an operator trial.

### 7.5 Evaluator fixture controller

The evaluator may use owner-authorized GitHub administration to prepare hidden provider regimes, reset fixture branches, and obtain ground truth.

This is test infrastructure, not an operator Eye and not a production world-kernel capability.

Evaluator-only provider access must never be serialized into Cold, Memory, or Structured operator context.

---

## 8. Identity and correspondence

### 8.1 No universal Entity

Build 001 has no global `Entity` object and no entity merge operation.

The primary stored referent is a provider-qualified `Manifestation`.

### 8.2 Primary manifestations

For each trial world:

- `M_local` is the local working-copy manifestation used by the trial. It may reference both CODEeye repository and worktree IDs in its provider metadata without claiming those CODEeye concepts are identical.
- `M_remote` is the hosted GitHub repository manifestation as observed through eyeBROWSE/provider evidence.

They are materially distinct.

### 8.3 Primary relationship

The only required repository-level correspondence relation is:

```text
M_local --git:working_copy_of--> M_remote
```

It is temporal, defeasible, evidence-backed, and non-transitive.

Build 001 does not expose repository-level `same_as`.

### 8.4 Activation evidence for `git:working_copy_of`

An active correspondence requires all of the following where available:

1. an exact configured local Git remote endpoint;
2. resolution of that endpoint to the GitHub fixture locator/provider identity observed through the remote side;
3. at least one exact shared Git commit/object anchor or equivalent provider-native continuity witness;
4. no current contradictory evidence indicating that the local remote points elsewhere.

Name/path similarity alone is insufficient.

LLM inference alone is insufficient.

### 8.5 Correspondence lifecycle

A correspondence assertion is immutable.

Later evidence may append a disposition:

```text
supports
disputes
supersedes
withdraws
```

A changed remote closes/supersedes the old active relationship for current projections but does not delete its history.

### 8.6 Version correspondence

Exact full Git commit/object identifiers may establish exact version correspondence across local and remote manifestations.

That does not merge the repositories.

---

## 9. Epistemic model

The core separation is:

```text
Evidence -> Observation -> Claim
Prediction -> Action -> Outcome -> PredictionEvaluation
```

These types must never be silently interchangeable.

### 9.1 Evidence

Evidence is immutable captured material such as:

- typed provider payload;
- native Git machine output;
- browser semantic snapshot/delta excerpt;
- exact command/provider response;
- selected network/provider response;
- small file/provider artifact needed to establish a scored fact.

Evidence answers: **what bytes/provider material did we retain?**

### 9.2 Observation

Observation records:

- which Eye/provider facet acquired the evidence;
- target Manifestation;
- acquisition method;
- acquisition time;
- provider revision/event metadata where available;
- coverage/freshness metadata.

Observation answers: **what did an observer acquire and when?**

### 9.3 Claim

Claim is one proposition derived from Evidence/Observation or explicitly from another source class.

Dimensions remain orthogonal:

- production method;
- authority/source class;
- valid time;
- record/knowledge time;
- freshness policy;
- optional confidence;
- derivation;
- scope;
- later support/dispute/supersession.

### 9.4 Production methods

Minimum values:

```text
provider_reported
user_or_fixture_asserted
model_inferred
deterministically_derived
```

Prediction is not a production method. It is a separate record type.

### 9.5 Authority classes

Minimum values:

```text
material_authority
provider_presentation
user_or_fixture
model
system_derivation
```

The exact provider/predicate decides whether a source is materially authoritative.

### 9.6 Dispositions rather than mutable status

Claims are not rewritten from `active` to `contradicted`.

Later records append dispositions such as:

```text
supports
disputes
supersedes
withdraws
```

Current standing is a projection over the Claim and dispositions known at the query's knowledge time.

### 9.7 Freshness

Freshness is derived, not a truth class.

A stale Observation remains a true record of what was observed at its original time.

Re-reading that record does not make it fresh.

If no sufficiently fresh provider evidence exists for a volatile predicate, the current answer is `unknown` with the last supported observation time.

### 9.8 No evidence laundering

The following are hard-invalid:

```text
Prediction -> Observation
model inference -> provider Observation
summary -> raw Evidence
provider acknowledgement -> verified material Claim without post-observation
```

Later corroboration adds support. It does not rewrite the original epistemic source class.

---

## 10. Temporal model

Build 001 uses append-oriented typed history with bitemporal Claim/Correspondence semantics and rebuildable projections.

### 10.1 Required time concepts

- `valid_range`: best-supported external validity interval asserted by the record's source/method;
- `recorded_at`: kernel-assigned knowledge/system time when the record became durable;
- `observed_at`: acquisition time for Observations;
- `provider_event_at`: optional source metadata where the provider supplies meaningful event time;
- `prediction_created_at`: when the immutable prediction became durable;
- `dispatch_at`: when material execution began;
- `outcome_resolved_at`: when the locked evaluator resolved a horizon.

### 10.2 Open validity does not mean omniscience

An open-ended Claim validity interval means no later closure is yet known under the Claim's continuity assumptions. It is not proof that the external fact remained unchanged.

Freshness policy and later dispositions determine whether it is eligible for a current belief.

### 10.3 Historical query forms

The kernel must support:

```text
query_beliefs(valid_as_of=V, known_as_of=K)
```

This must distinguish:

- what current evidence suggests was true at V;
- what the system had evidence to believe at V/K before later information arrived.

### 10.4 Out-of-order evidence

A later-arriving Observation may alter today's reconstruction of an earlier valid time without changing what was knowable at the earlier knowledge time.

No historical record is overwritten to make the timeline look cleaner.

### 10.5 Current state

There is no universal `State` row/table.

Current state is a derived projection over:

- eligible Claims;
- dispositions;
- correspondence;
- freshness;
- the requested valid/knowledge times.

Any materialized current view is disposable and must be rebuildable.

---

## 11. Action, prediction, and outcome model

### 11.1 Semantic action boundary

One measured Build 001 action is one semantic material action with:

- one declared action type;
- explicit target Manifestation(s);
- typed parameters;
- referenced pre-state Claims/Observations;
- one dispatch boundary;
- a complete preregistered prediction proposition vector;
- independently observable postconditions.

Primitive Git commands, browser clicks, JS calls, or API requests are execution trace, not separate TransitionEpisodes unless the semantic action itself changes.

### 11.2 Minimal lifecycle

```text
ActionAttempt declared
        |
Prediction committed
        |
seal_for_dispatch
        |
material execution begins
        |
provider receipt/return (if any)
        |
fresh post-action Observation(s)
        |
Outcome resolved at locked horizon(s)
        |
PredictionEvaluation
        |
TransitionEpisode closed
```

### 11.3 Prediction ordering invariant

A prediction is eligible only if it is durably stored before dispatch:

```text
prediction.recorded_at < action.dispatch_at
```

The kernel must enforce this transactionally.

If target, action type, or material parameters change after prediction, the old action is abandoned/misaligned and a new ActionAttempt + Prediction is required.

### 11.4 Provider receipt is not success

A successful Git exit code, HTTP 2xx, browser confirmation, or UI success string is a provider receipt/presentation fact.

It does not by itself create a verified Outcome.

### 11.5 Outcome statuses

Minimum:

```text
verified
partial
failed
unknown
censored
```

`unknown` is valid when fresh provider state cannot be obtained.

`censored` is valid when an asynchronous outcome cannot be resolved inside the locked evaluation horizon.

### 11.6 Attribution

Outcome records must distinguish at least:

```text
consistent_with_action
confounded_or_ambiguous
not_attributed
```

Out-of-band state changes must not be silently learned as action effects.

### 11.7 Compensation

Build 001 has no universal compensation phase.

If a fixture cleanup or semantic undo is itself measured, it is another ActionAttempt with its own lifecycle. Evaluator-only reset operations stay outside the world-learning corpus.

---

## 12. TransitionEpisode

### 12.1 Definition

One closed `TransitionEpisode` links the empirical lifecycle of one semantic material action:

```text
pre-state evidence/claims
+ correspondence basis
+ prediction
+ dispatched action
+ provider receipt/trace
+ fresh post-state observations
+ outcome
+ prediction evaluation
```

It is a linkage/experience object, not a duplicate world snapshot.

### 12.2 Required references

A closed episode must reference:

- trial/run ID;
- target Manifestation(s);
- active/used Correspondence assertion(s);
- relevant pre-action Observations;
- relevant pre-action Claims;
- ActionAttempt;
- eligible Prediction;
- dispatch phase;
- post-action Observations;
- Outcome(s);
- PredictionEvaluation(s);
- public environment/version scope;
- producer model/invocation version;
- closure time.

### 12.3 What is deliberately absent

Build 001 TransitionEpisodes do not contain:

- chain of thought;
- full chat transcripts;
- an LLM-written causal explanation;
- promoted Rule objects;
- planner nodes;
- skill code;
- hidden evaluator regime labels;
- unrelated Eye activity.

### 12.4 Public versus hidden scope

The world-kernel episode may contain only environment facts the operator could legitimately observe or inherit.

Hidden fixture seed, ground-truth policy label, arm assignment, and evaluator-only provider facts live in the separate evaluator store and must never enter treatment context.

---

## 13. Build 001 logical data model

All durable records carry:

```text
id
schema_version
recorded_at   // kernel-assigned, immutable
```

Opaque IDs may use UUIDv7 or an equivalently sortable unique identifier. Embedded timestamp bits are not authoritative ordering; `recorded_at`/record sequence is.

### 13.1 `manifestation`

Purpose: provider-qualified mutable material referent.

Required:

```text
manifestation_id
provider_namespace
manifestation_kind
authority_scope
identity_basis
schema_version
recorded_at
```

Optional:

```text
provider_native_id
observer_native_ids JSONB
first_observation_id
display_label
```

Examples:

```text
provider_namespace = codeeye/git-local
manifestation_kind = git-working-copy

provider_namespace = eyebrowse/github
manifestation_kind = github-repository
```

The internal ID is not material identity proof.

### 13.2 `locator`

Purpose: temporal provider address/name/path for a Manifestation.

Required:

```text
locator_id
manifestation_id
locator_namespace
locator_type
locator_value
valid_range tstzrange
source_observation_id
schema_version
recorded_at
```

Optional:

```text
supersedes_locator_id
normalization_metadata JSONB
```

Rename/move appends new locator records; it does not rewrite identity.

### 13.3 `evidence`

Purpose: immutable retained provider material.

Required:

```text
evidence_id
provider_namespace
observer_name
captured_at
hash_algorithm
content_hash
blob_ref
media_type
acquisition_method
byte_length
schema_version
recorded_at
```

Optional:

```text
provider_revision
provider_event_at
encoding
metadata JSONB
```

Evidence bytes are immutable.

### 13.4 `observation`

Purpose: one provider/Eye acquisition event.

Required:

```text
observation_id
target_manifestation_id
observer_name
observer_version
provider_namespace
observed_at
acquisition_status
coverage JSONB
schema_version
recorded_at
```

Optional:

```text
provider_revision
provider_event_at
locator_id
source_dependency JSONB
raw_normalized_payload JSONB
```

Mechanical join table:

```text
observation_evidence(observation_id, evidence_id)
```

### 13.5 `claim`

Purpose: one proposition about provider/epistemic state.

Required:

```text
claim_id
subject_manifestation_id
predicate_namespace
predicate
value_json
production_method
authority_class
valid_range tstzrange
scope JSONB
producer JSONB
schema_version
recorded_at
```

Optional:

```text
subject_selector JSONB
confidence double precision
freshness_policy_id
```

Mechanical links:

```text
claim_observation(claim_id, observation_id)
claim_evidence(claim_id, evidence_id)
claim_derivation(claim_id, source_claim_id)
```

Material/provider-reported Claims require Observation/Evidence lineage.

### 13.6 `claim_disposition`

Purpose: append support/dispute/supersession/withdrawal without mutating the original Claim.

Required:

```text
claim_disposition_id
target_claim_id
relation  // supports | disputes | supersedes | withdraws
effective_valid_at
basis JSONB
producer JSONB
schema_version
recorded_at
```

Optional:

```text
replacement_claim_id
rationale_code
```

### 13.7 `correspondence`

Purpose: defeasible relation between separate Manifestations.

Required:

```text
correspondence_id
left_manifestation_id
relation_namespace
relation_type
right_manifestation_id
method
confidence
valid_range tstzrange
producer JSONB
schema_version
recorded_at
```

Mechanical evidence links:

```text
correspondence_observation(correspondence_id, observation_id)
correspondence_evidence(correspondence_id, evidence_id)
correspondence_claim_basis(correspondence_id, claim_id)
```

Required Build 001 relation:

```text
relation_namespace = git
relation_type      = working_copy_of
```

### 13.8 `correspondence_disposition`

Same append-only pattern as Claim disposition:

```text
correspondence_disposition_id
target_correspondence_id
relation  // supports | disputes | supersedes | withdraws
effective_valid_at
basis JSONB
schema_version
recorded_at
```

### 13.9 `action_attempt`

Purpose: immutable declared semantic action.

Required:

```text
action_id
trial_id
configuration_block_id
target_manifestations JSONB
owning_eye
capability_name
capability_version
semantic_action_namespace
semantic_action_type
parameters JSONB
producer_model JSONB
fixture_scope_id
schema_version
declared_at
recorded_at
```

Mechanical pre-state links:

```text
action_precondition_claim(action_id, claim_id)
action_precondition_observation(action_id, observation_id)
```

### 13.10 `action_phase`

Purpose: append irreversible lifecycle boundaries.

Minimum phases:

```text
dispatched
provider_acknowledged
```

Required:

```text
action_phase_id
action_id
phase
occurred_at
payload JSONB
schema_version
recorded_at
```

Exactly one `dispatched` phase is permitted per ActionAttempt.

The dispatch transaction verifies an eligible Prediction exists.

### 13.11 `prediction`

Purpose: immutable pre-action forecast.

Required:

```text
prediction_id
action_id
evaluation_spec_version
evaluation_spec_hash
outcome_probabilities JSONB
expected_deltas JSONB
expected_invariants JSONB
horizons JSONB
mechanism
mechanism_version
producer_model JSONB
schema_version
created_at
recorded_at
```

Mechanical basis links:

```text
prediction_assumed_claim(prediction_id, claim_id)
prediction_basis_episode(prediction_id, episode_id)
prediction_basis_evidence(prediction_id, evidence_id)
```

No free-form rationale is required or scored.

### 13.12 `outcome`

Purpose: evaluator resolution of actual postconditions at a locked horizon.

Required:

```text
outcome_id
action_id
horizon_id
resolution_status
actual_propositions JSONB
actual_deltas JSONB
actual_invariants JSONB
attribution_status
resolver_version
resolved_at
schema_version
recorded_at
```

Mechanical links:

```text
outcome_observation(outcome_id, observation_id)
outcome_evidence(outcome_id, evidence_id)
```

### 13.13 `prediction_evaluation`

Purpose: immutable proper/component-wise scoring.

Required:

```text
evaluation_id
prediction_id
outcome_id
eligibility_status
scorer_version
mean_brier_loss
brier_components JSONB
delta_tp
delta_fp
delta_fn
delta_precision
delta_recall
delta_f1
invariant_violations JSONB
evaluated_at
schema_version
recorded_at
```

Optional:

```text
latency_metrics JSONB
censor_notes
```

A changed scorer creates a new evaluation record. It never overwrites the original.

### 13.14 `transition_episode`

Purpose: queryable unit of lived experience.

Required:

```text
episode_id
trial_id
configuration_block_id
action_id
prediction_id
public_environment_scope JSONB
producer_versions JSONB
closed_at
schema_version
recorded_at
```

Mechanical links:

```text
episode_correspondence(episode_id, correspondence_id)
episode_pre_observation(episode_id, observation_id)
episode_pre_claim(episode_id, claim_id)
episode_post_observation(episode_id, observation_id)
episode_outcome(episode_id, outcome_id)
episode_evaluation(episode_id, evaluation_id)
```

### 13.15 Explicitly absent tables

There is no Build 001 table for:

```text
entity
state
event
rule
hypothesis
capability
plan
experiment
skill
embedding
universal_graph_node
universal_graph_edge
action_receipt_export
audit_entry
approval
risk_class
```

---

## 14. PostgreSQL implementation constraints

### 14.1 Append orientation

Core epistemic records are inserted, not overwritten.

Permitted mutable operational metadata is limited to process/runtime bookkeeping that is not epistemic history.

Claims/correspondence are revised through disposition rows.

### 14.2 Bitemporal queries

Use PostgreSQL range/time support for `valid_range` and ordinary `recorded_at` knowledge time.

The store must support efficient indexes for:

```text
subject + predicate + valid_range
recorded_at
manifestation
correspondence endpoints
semantic action type
episode public scope
trial/configuration block
```

PostgreSQL 18 temporal constraint features may be used where they genuinely simplify integrity, but contradictory Claims are intentionally allowed to coexist; do not apply no-overlap constraints that destroy epistemic conflict.

### 14.3 No graph layer

Correspondence/provenance use relational tables and joins.

A graph database is forbidden in Build 001.

### 14.4 No vector layer

Historical episode selection is exact/structured and deterministic.

No embeddings or pgvector index in Build 001.

---

## 15. Evidence blob plane

Evidence blobs are content-addressed by an algorithm-qualified cryptographic hash.

Recommended naming:

```text
X:\WorldKernel\Build001\Evidence\sha256\ab\cd\<full-hash>
```

Minimum evidence retention for the experiment:

- typed native Git response needed for a scored Claim;
- eyeBROWSE semantic/network extract needed for a scored remote Claim;
- action provider receipt when it matters to action alignment;
- post-action provider evidence used by the Outcome resolver;
- hostile identity evidence.

Do not retain full-page/browser recordings or every command output by default.

If a compact exact excerpt is sufficient, retain the compact exact excerpt plus provider metadata rather than bulk unrelated material.

---

## 16. Kernel conceptual API

The exact wire format is an implementation detail, but the semantic operations are frozen.

### 16.1 Ingestion and epistemics

```text
observation.ingest(envelope)
claim.append(assertion)
claim.disposition(target, relation, basis)
correspondence.assert(left, relation, right, basis)
correspondence.disposition(target, relation, basis)
beliefs.query(scope, valid_as_of, known_as_of)
```

### 16.2 Action/prediction lifecycle

```text
action.declare(spec)
prediction.record(action_id, prediction)
action.seal_for_dispatch(action_id, prediction_id)
action.record_provider_receipt(action_id, receipt)
outcome.record(action_id, horizon, observations, actuals)
prediction.evaluate(prediction_id, outcome_id, scorer_version)
episode.close(action_id)
```

`action.seal_for_dispatch` must atomically:

1. confirm ActionAttempt has not already been dispatched;
2. confirm the supplied Prediction exists and belongs to that action;
3. confirm Prediction is immutable/currently eligible;
4. append the dispatch phase/cutoff;
5. return a one-use dispatch token to orchestration.

The kernel itself does not execute the provider action.

### 16.3 Experience/context

```text
episode.query(exact_scope, action_type, time_range, limit)
package.build(trial_context, arm, budget)
projection.rebuild()
runtime.info()
```

`package.build` must be deterministic and versioned.

### 16.4 Explicitly absent APIs

No Build 001 API for:

```text
entity.merge
rule.promote
hypothesis.learn
plan.create
experiment.choose
capability.score
skill.publish
embedding.search
approve_action
verify_action
export_receipt
```

---

## 17. Preregistered semantic action vocabulary

Build 001 uses exactly six operator-visible material semantic action classes in the primary corpus.

The implementation may use multiple provider primitives internally but may not add a seventh scored action class after confirmatory execution begins.

### 17.1 `git:create_local_commit`

Precondition:

- fixture worktree has exactly one evaluator-prepared tracked-file change;
- target branch is known;
- no unresolved merge/rebase state.

Execution:

- stage the fixture change;
- create one commit with deterministic experiment metadata/message.

Required binary prediction propositions:

```text
P01 provider_accepts_action
P02 local_head_changes
P03 local_head_equals_new_commit
P04 remote_target_ref_changes_before_push
P05 local_worktree_clean_after
P06 current_branch_name_changes
P07 new_commit_reachable_locally
P08 new_commit_reachable_remotely_before_push
```

Expected invariants are not hard-coded for the operator; the evaluator knows actual outcomes.

### 17.2 `git:create_branch`

Build 001 semantics are **create and check out one new disposable local branch at current HEAD**.

Required propositions:

```text
P01 provider_accepts_action
P02 new_local_branch_exists
P03 current_branch_is_new_branch
P04 local_head_sha_changes
P05 remote_branch_exists_before_push
P06 worktree_content_changes
```

### 17.3 `git:push_ref`

Execution:

- push the current local test branch/ref to its preregistered remote target;
- no force push.

Required propositions:

```text
P01 provider_accepts_push
P02 remote_ref_exists_at_H1
P03 remote_ref_equals_local_head_at_H1
P04 local_head_changes_because_of_push
P05 local_worktree_changes_because_of_push
P06 remote_check_starts_by_H2
P07 remote_check_terminal_success_by_H3
P08 browser_presentation_reflects_new_remote_head_by_H1
```

`P01`, `P06`, `P07`, and `P08` are deliberately influenced by hidden provider configuration/regime.

### 17.4 `github:create_remote_commit`

Execution:

- use eyeBROWSE against the authenticated GitHub web application to make one controlled fixture-file change and create one hosted commit on the target test branch.

Required propositions:

```text
P01 provider_accepts_action
P02 remote_head_changes
P03 remote_head_equals_new_hosted_commit
P04 local_head_changes_before_fetch
P05 local_worktree_changes_before_fetch
P06 local_remote_tracking_ref_changes_before_fetch
P07 new_remote_commit_reachable_locally_before_fetch
P08 browser_presentation_reflects_new_remote_commit_by_H1
```

No direct GitHub connector/API call is permitted inside the operator action path for this semantic action.

### 17.5 `git:fetch_remote`

Execution:

- perform a normal fetch for the configured fixture remote;
- do not merge, rebase, or update the checked-out branch.

Required propositions:

```text
P01 provider_accepts_action
P02 local_head_changes
P03 local_worktree_changes
P04 remote_tracking_ref_equals_remote_head_at_H1
P05 remote_head_changes_because_of_fetch
P06 remote_commit_reachable_locally_after_fetch
P07 checked_out_branch_content_changes
```

### 17.6 `git:integrate_fast_forward`

Execution:

- attempt an explicit fast-forward-only integration of the selected remote-tracking ref into the checked-out local branch;
- no automatic merge commit;
- no rebase.

Required propositions:

```text
P01 fast_forward_is_accepted
P02 local_head_equals_remote_target_after_H1
P03 local_head_changes
P04 local_worktree_content_changes
P05 local_worktree_clean_after
P06 remote_head_changes_because_of_integration
P07 merge_commit_created
```

Hidden divergence topology makes `P01` nontrivial.

---

## 18. Prediction format and scoring

### 18.1 Complete proposition vector

For every measured action, the operator must provide a probability in `[0,1]` for every required proposition in the locked evaluation spec.

Omitting a proposition is not allowed to reduce the denominator.

The pilot freezes the missing-value handling rule before confirmatory trials. Default candidate rule: missing/invalid probability is scored as `0.5` and recorded as a format defect.

### 18.2 Primary proper score

For binary proposition `j`:

```text
Brier_j = (p_j - y_j)^2
```

Action-level Brier loss is the arithmetic mean across the complete required proposition vector at the locked horizons.

Configuration-block primary loss is the arithmetic mean across its measured actions.

The independent confirmatory unit is the configuration block, not each proposition/action.

### 18.3 Structured delta score

Each action schema defines a fixed evaluator vocabulary of possible material deltas, such as:

```text
local_head_changed
remote_ref_changed
working_tree_changed
branch_created
remote_tracking_ref_changed
check_started
check_terminal
```

The operator predicts expected deltas.

Report:

```text
precision
recall
F1
TP / FP / FN
```

Delta F1 is secondary, not the confirmatory primary metric.

### 18.4 Invariant scoring

Expected nonchanges are encoded separately.

Report every violated invariant and severity category from the locked evaluator schema.

No weighted synthetic overall score is the primary acceptance metric.

### 18.5 Timing

Asynchronous effects use pilot-frozen horizons/latency bins.

Candidate horizons before pilot:

```text
H1 = short provider-state propagation / first fresh re-observation
H2 = check-start horizon
H3 = check-terminal horizon
```

The exact wall-clock values are fixed from the non-confirmatory pilot and then hashed into the evaluation spec before confirmatory seeds are opened.

Do not select a convenient horizon after observing an outcome.

---

## 19. Real fixture world

### 19.1 Primary fixture

Use one evaluator-controlled disposable GitHub repository as the primary hosted world.

The implementation phase may choose its final repository name. This specification does not create or canonicalize one.

Requirements:

- owned/controlled by StealthEyeLLC or an explicitly owner-authorized test identity;
- no production code or user data;
- all branches/files/checks disposable;
- deterministic reset from seed;
- authenticated Git operations available to the local fixture;
- authenticated eyeBROWSE profile can perform the remote-commit action;
- provider policy/check configuration can be changed by evaluator setup;
- evaluator has a direct provider-native way to determine ground truth that is not exposed to trial operators.

### 19.2 Local fixture

Each matched arm run gets an equivalent isolated local working copy/worktree.

The reset mechanism must make starting material state equivalent across arms while avoiding cross-arm contamination.

Preferred pattern:

- one clean version-pinned base/mirror or canonical seed source;
- arm/run-specific working-copy root;
- opaque randomized test branch names;
- no reuse of dirty working trees between arms;
- exact starting SHA and file hashes recorded by evaluator.

### 19.3 Hidden environment dimensions

The fixture generator may vary only preregistered safe provider/environment factors.

Core factors:

1. **Local/remote topology**
   - synchronized;
   - local ahead;
   - remote ahead;
   - diverged/non-fast-forward.

2. **Direct push regime**
   - accepted;
   - rejected by real hosted-provider branch policy/ruleset/protection.

3. **Remote check/workflow regime**
   - no check for this action;
   - check starts and succeeds;
   - check starts and fails where useful.

4. **Browser presentation freshness regime**
   - fresh observation after action;
   - deliberately stale previously rendered page until a fresh browser observation/refresh is requested.

5. **Policy epoch**
   - stable;
   - reversed in drift cohort after historical experience supports the prior pattern.

The exact generator and factor combinations are locked before confirmatory execution.

### 19.4 Real provider rules, not mocked world dynamics

Hidden outcome variation must arise from real Git/GitHub/browser state wherever practical.

Examples:

- Git ancestry determines fast-forwardability;
- GitHub branch protection/ruleset determines push rejection;
- GitHub Actions/check configuration determines check behavior;
- browser target/page freshness determines stale presentation.

Do not implement a fake probabilistic simulator to manufacture the result Build 001 is supposed to learn.

### 19.5 Evaluator-only auxiliary fixtures

The hostile identity suite may use additional disposable local repos or hosted fixture repos/forks/redirects when required to test decoys, changed remotes, and provider identity.

These are evaluator fixtures, not additional primary operator worlds and not additional Eyes.

---

## 20. Experiment/operator isolation without product guardrails

The owner retains normal StealthEye/GitHub authority outside the experiment.

Inside a confirmatory trial, the operator is instantiated with exactly the interfaces under test:

- CODEeye adapter;
- eyeBROWSE adapter;
- world-package input appropriate to the assigned arm;
- the six semantic action operations.

It does not receive the evaluator's hidden provider controller or configuration labels.

This is experimental blinding, not a permanent capability restriction or Eye guardrail.

Nothing in this specification changes the normal Eye product authority surface.

---

## 21. Preflight gates before acquisition/pilot

No confirmatory work may begin until all preflight gates pass.

### P0 - Baseline capture

Record:

- Eye/CODEeye/eyeBROWSE exact commits;
- .NET/Git/Chrome/PostgreSQL/Node versions used;
- provider account/repository ID;
- model/version/configuration;
- evaluator schema/scorer versions.

### P1 - Live CODEeye

A live probe must show:

- current named-pipe connectivity;
- workspace attachment to an isolated fixture working copy;
- `repo.status` returns correct local HEAD/branch/change state;
- `world.sync` reflects a controlled local change.

Descriptor files alone cannot pass P1.

### P2 - Live eyeBROWSE

A live probe must show:

- browser/kernel connectivity;
- fixture GitHub target can be opened/observed;
- target/document semantic state is available;
- authenticated test identity can perform one disposable browser-side remote commit;
- fresh observation can distinguish new hosted commit state from a deliberately stale page.

### P3 - Experimental Git facet

The facet must demonstrate against the fixture only:

- exact remotes;
- local and remote-tracking refs;
- object reachability;
- create branch;
- commit evaluator-prepared change;
- push non-force ref;
- fetch;
- ff-only integration;
- provider receipt capture;
- no persistent state of its own.

### P4 - PostgreSQL runtime

Demonstrate:

- on-demand PostgreSQL 18 startup without SCM-service registration;
- schema migration/init;
- write/read;
- world-kernel restart with data retained;
- database stop/restart with data retained;
- loopback-only endpoint;
- projection rebuild from durable records.

### P5 - Fresh invocation mechanism

The implementation must establish a reproducible way to run each evaluation arm in a genuinely clean model context.

The mechanism may be a product-native fresh conversation or another isolated invocation path available at execution time, but it must satisfy:

- exact model/version/config recorded;
- no prior trial transcript;
- no cross-arm memory;
- no hidden evaluator state;
- same model/config across all confirmatory arms;
- same base instructions/tool contract across arms except arm-specific inherited state.

A permanent local model/controller is not permitted merely to solve experiment orchestration.

### P6 - Deterministic reset

Given a configuration seed and arm slot, reset must reproduce:

- starting local SHA/ref/content;
- remote SHA/ref/content;
- branch policy/check regime;
- browser stale/fresh setup;
- allowed target branch relationship;
- evaluator labels.

Reset mismatch blocks the trial before the operator sees outcome information.

---

## 22. Acquisition corpus

The acquisition corpus exists to create real prior experience from which Memory and Structured inheritance are generated.

It is not confirmatory evaluation data.

### 22.1 Acquisition operator

Use the same frontier model family/configuration intended for confirmatory evaluation unless implementation timing makes that impossible.

Each acquisition configuration block starts with a fresh invocation and **no prior world-memory inheritance**. This prevents the acquisition corpus from recursively depending on the treatment being evaluated.

### 22.2 Acquisition action selection

The evaluator schedules actions to guarantee coverage. The model predicts each action but does not need to choose which training action occurs.

For each scheduled action:

```text
observe -> predict -> dispatch -> execute -> reobserve -> score -> close episode
```

This creates a balanced empirical corpus rather than allowing the model to avoid difficult action classes.

### 22.3 Acquisition stop rule

Start with 24 independent configuration blocks.

Continue, up to 36 blocks, until all are true:

- each of the six semantic action classes has at least 20 eligible closed TransitionEpisodes;
- `git:push_ref` has at least 8 accepted and 8 rejected provider outcomes;
- `git:integrate_fast_forward` has at least 8 accepted and 8 rejected/non-FF outcomes;
- the check-start proposition has at least 8 true and 8 false outcomes if checks remain in the locked evaluator;
- at least 6 distinct configuration seeds contribute to each action class.

If coverage is still not achieved at 36 blocks, the fixture generator is inadequate and must be redesigned **before** pilot data is used.

No confirmatory seed may appear in acquisition.

### 22.4 Same underlying corpus for both history-bearing arms

Memory and Structured inherit from exactly the same acquisition TransitionEpisodes.

No arm-specific training actions are allowed.

---

## 23. Inherited-history packaging and fairness

Build 001 must isolate the value of structured temporal/correspondence representation rather than accidentally testing who received more information.

### 23.1 Shared candidate-episode selection

For every evaluation decision, a deterministic selector produces the same candidate historical episode IDs for Memory and Structured using only operator-visible/public keys such as:

```text
semantic action class
remote fixture manifestation/correspondence
observable branch/topology class where known
provider/version compatibility
time/recency
success/failure/counterexample balance
```

No embedding retrieval.

No treatment-only hidden configuration lookup.

The two arms serialize the same selected experiential source records differently.

### 23.2 Conventional Memory serialization

Memory receives a deterministic narrative/chronological representation of the selected source records.

It may include the same underlying factual information that Structured sees, including:

- prior observed pre-state facts;
- action taken;
- prior prediction probabilities where they are part of the source episode;
- actual outcome;
- prior prediction mistakes;
- timestamps in ordinary readable form;
- discovered repository-specific behavior expressed as ordinary episode history.

It does not receive:

- a typed bitemporal query API;
- direct Claim/Disposition graph structure;
- typed active/disputed correspondence status;
- hidden evaluator labels.

Memory must not be intentionally weak or vague.

### 23.3 Structured serialization

Structured receives a deterministic compact package containing the same selected experiential source plus:

- typed Manifestation/Locator references;
- active/disputed `git:working_copy_of` correspondence;
- Claim source/authority/freshness/valid/knowledge times;
- contradictions/dispositions;
- selected TransitionEpisodes with original PredictionEvaluation components;
- explicit unknown/stale state.

It does not receive hidden fixture regime labels or extra source episodes.

### 23.4 Inherited-context budget

Candidate preregistration budget:

```text
maximum 6,000 model tokens AND maximum 32 KiB UTF-8 bytes
```

The lower effective limit wins.

The same ceiling applies to Memory and Structured inherited-state packages.

The 12-block pilot may reduce this budget if both arms fit comfortably and the smaller budget improves repeatability. The pilot may not increase it above 8,000 tokens/48 KiB without restarting the full pilot under the new budget.

The final budget is frozen before confirmatory seeds are opened.

### 23.5 Same model-call schedule

Package construction must not invoke an extra LLM for Structured.

Both history-bearing arms receive one deterministic package and the same operator-call opportunities.

No treatment-only summarizer, verifier, critic, or hidden reasoning call.

---

## 24. Fresh current-state observation

Inherited experience never substitutes for current provider observation.

Each evaluation arm receives the same protocol for fresh current-state acquisition before a material decision.

The protocol must make available, at minimum where relevant:

Local side:

- current local HEAD SHA/ref/branch;
- working-copy cleanliness/change summary;
- configured target remote;
- selected remote-tracking state when observed/fetched;
- exact provider version.

Remote/browser side:

- current target/repository locator;
- browser target/document identity;
- branch/ref/commit presentation;
- check/policy-related presentation where exposed;
- freshness/observation time.

A stale browser presentation remains a stale Observation. The operator may choose to request a fresh browser observation if that is part of the task behavior.

---

## 25. Non-confirmatory pilot

The pilot validates the experiment, not the hypothesis.

### 25.1 Pilot size

Run exactly 12 independent held-out configuration blocks, each under all three arms.

Pilot blocks and their outcomes are permanently excluded from confirmatory analysis.

### 25.2 Pilot may tune only these experimental parameters

- exact H1/H2/H3 timing horizons;
- inherited package budget within the range in Section 23.4;
- serialization formatting that does not change underlying information;
- fixture reset mechanics;
- outcome proposition wording/schema where ambiguity is discovered;
- confirmatory sample size through the mechanical power rule below;
- provider wait/retry mechanics required to distinguish censored from failed outcomes.

### 25.3 Pilot may not change

- primary Structured-versus-Memory comparison;
- 20% target relative Brier reduction;
- primary Brier metric family;
- three primary arms;
- six semantic action classes;
- no-Entity/no-State/no-Event kernel boundary;
- provider-authority model;
- original Eye constraints;
- use of fresh invocations;
- equal-information/call-budget requirement.

If pilot reveals a needed change to one of those, Build 001 specification must be reopened and the pilot restarted from zero.

### 25.4 Pilot headroom requirement

The pilot must show that conventional Memory has nontrivial prediction headroom.

Candidate criterion:

```text
mean Memory block-level Brier loss >= 0.05
```

If Memory is essentially perfect, the fixture cannot falsify a 20% relative improvement and must be made harder before confirmatory testing.

### 25.5 Mechanical confirmatory sample-size rule

From the 12 pilot blocks compute:

```text
L_M,i = block-level Memory Brier loss
L_S,i = block-level Structured Brier loss
d_i   = L_M,i - L_S,i
Mbar  = mean(L_M,i)
s_d   = sample standard deviation(d_i)
delta = 0.20 * Mbar
```

Target:

```text
alpha = 0.05 two-sided
power = 0.80
z_alpha = 1.959964
z_power = 0.841621
n_raw = ((z_alpha + z_power) * s_d / delta)^2
```

Then:

```text
N = ceiling_to_multiple_of_8(max(48, n_raw))
```

Rules:

- if `s_d == 0`, set `N = 48`;
- if `Mbar < 0.05`, pilot headroom fails and confirmatory run does not start;
- if calculated `N > 96`, the current design is considered impractical/underpowered; revise the fixture or measurement design and rerun the pilot from zero rather than silently clipping an underpowered confirmatory run;
- confirmatory `N` is frozen before confirmatory seed generation/reveal.

This makes the exact confirmatory sample count a preregistered pilot-derived parameter rather than an architecture claim.

---

## 26. Confirmatory design

### 26.1 Independent unit

The independent unit is a **configuration block**.

Multiple semantic actions/propositions inside one block are averaged/clustered. They are not counted as independent samples.

### 26.2 Arms

#### Arm A - Cold

```text
fresh ChatGPT
same base instructions
same live CODEeye/eyeBROWSE access
same semantic actions
no acquisition history
```

#### Arm B - Conventional Memory

```text
fresh ChatGPT
same base instructions
same live Eyes/actions
same selected acquisition source episodes as Structured
conventional deterministic narrative memory
same inherited-state budget
```

#### Arm C - Structured

```text
fresh ChatGPT
same base instructions
same live Eyes/actions
same selected acquisition source episodes as Memory
structured world-kernel package
same inherited-state budget
```

### 26.3 Matched starting state

For a configuration block, each arm receives an equivalent reset world under the same hidden configuration seed.

Use separate opaque branch/worktree instances when needed to avoid one arm's mutations contaminating another arm.

Arm execution order is randomized within block.

### 26.4 Fresh invocation rule

Every arm run starts with a fresh model context.

In drift/reversal sequences, each contradiction opportunity also starts a fresh invocation; continuity is only the arm's updated persistent inherited state.

### 26.5 No hidden compute subsidy

Across arms:

- same model/version/config;
- same base task instructions;
- same operator tool/action schema;
- same live observation opportunity;
- same inherited-state ceiling for Memory/Structured;
- same maximum operator-call schedule;
- same fixture goal.

### 26.6 Confirmatory corpus composition

At least one third of confirmatory blocks must exercise a regime change/drift sequence.

The remainder are stationary held-out configurations.

Exact stratification is frozen after pilot and before confirmatory seed reveal.

### 26.7 Blinding

Evaluator hidden labels are derived from seeded fixture state and direct provider-native audit unavailable to the operator.

The scorer does not use the model's self-declared success.

Primary score files and integrity exclusions are frozen before arm labels are decoded for aggregate analysis where practical.

---

## 27. Evaluation tasks and behavior metric

### 27.1 Task objective

A confirmatory task requires the fresh operator to use current observations plus its arm-specific inherited state to reach a seeded safe target relation between local and remote branch state using the fewest justified material actions while preserving locked invariants.

The exact task templates are fixed before confirmatory execution.

### 27.2 Behavioral action metric

The evaluator computes the minimum number of material semantic actions required to reach the target from the true seeded state under the allowed action vocabulary.

For each run:

```text
excess_material_actions = max(0, actual_material_actions - oracle_minimum_material_actions)
```

Report also:

```text
failed_material_actions
real observation calls
provider refreshes
task completion
consequential wrong-target/invariant errors
```

This replaces subjective labeling of a model action as "futile" with an evaluator-computable path-length measure.

### 27.3 Behavioral co-gate

Where Memory has nonzero mean excess material actions, a strong PASS requires at least 20% relative reduction in Structured mean excess material actions.

If Memory's mean excess is zero, Structured must also remain effectively zero; the behavioral gate becomes non-degradation rather than an impossible relative improvement.

Structured may not improve prediction by taking more consequential wrong actions.

---

## 28. Primary statistical analysis

### 28.1 Primary endpoint

Block-level mean Brier loss.

### 28.2 Primary comparison

Structured versus Conventional Memory.

Cold is secondary context only.

### 28.3 Effect criterion

Let:

```text
B_M = mean block-level Brier loss for Memory
B_S = mean block-level Brier loss for Structured
relative_improvement = (B_M - B_S) / B_M
```

Strong predictive support requires:

```text
relative_improvement >= 0.20
```

### 28.4 Paired uncertainty procedure

Use a paired cluster bootstrap over configuration blocks:

```text
10,000 resamples
resample whole matched configuration blocks
preserve Memory/Structured pairing
95% two-sided confidence interval
```

The confidence interval for `B_M - B_S` must exclude zero in Structured's favor.

### 28.5 Paired randomization check

Also run a paired randomization/permutation test on block-level Memory/Structured labels.

Require:

```text
p < 0.05
```

This is confirmatory support for the single preregistered primary hypothesis, not a search across metrics.

### 28.6 Secondary outcomes

Report without replacing the primary endpoint:

- Cold versus Memory/Structured Brier;
- delta precision/recall/F1;
- invariant violations;
- calibration curve/ECE where sample size supports it;
- timing-bin score;
- task completion;
- excess material actions;
- failed actions;
- observation/tool counts;
- inherited tokens/bytes;
- kernel/package latency;
- evidence/storage growth.

No secondary metric can rescue failure of the primary comparison into a strong PASS.

---

## 29. Drift / contradiction cohort

### 29.1 Purpose

Build 001 does not implement persistent Rules, but it must test whether inherited experience can stop dominating once real provider behavior changes.

### 29.2 Regime reversal

The evaluator selects one previously experienced binary provider regularity, such as direct push acceptance on a class of test branches, and reverses it through real provider configuration.

Historical acquisition evidence remains unchanged.

New contradictory TransitionEpisodes append normally.

### 29.3 Fresh-instance sequence

For each drift block:

1. fresh invocation receives inherited state before first contradictory opportunity;
2. it predicts/acts and observes contradiction 1;
3. updated arm state persists;
4. a completely fresh invocation handles opportunity 2;
5. repeat through opportunity 3 and subsequent held-out decisions.

### 29.4 Recovery criterion

By no later than the third strong relevant contradictory TransitionEpisode:

- probability assigned to the obsolete binary outcome must fall to `<= 0.50`;
- it must be below the probability assigned to the newly supported alternative;
- the next decision must not rely on the obsolete expectation as a high-confidence invariant;
- old supporting Evidence remains historically retrievable;
- new contradictory Evidence remains linked;
- current Structured package must present the conflict/current standing rather than silently deleting history.

The confirmatory drift gate requires at least 90% of drift blocks to meet this bound for a strong PASS.

The 90% is an experimental acceptance parameter, not a universal architecture law.

---

## 30. Hostile suite

The hostile suite is deterministic and separate from the primary prediction statistic unless a case is explicitly included in a configuration block.

| Hostile case | Failure targeted | Required correct behavior |
|---|---|---|
| same-basename local decoy repo | name/path identity | separate Manifestation; no correspondence from name |
| hosted fork/clone fixture where available | shared-history false merge | separate Manifestation; exact shared commits only |
| GitHub repository rename | locator-as-identity | preserve remote Manifestation only when strong provider continuity exists; append locator change |
| changed local remote | stale working-copy relation | dispute/close old `git:working_copy_of`; establish new only with evidence |
| stale browser target/page | presentation as truth | retain original observed time; fresh remote belief remains unknown or comes from new observation |
| local unpushed commit | local/remote collapse | local head advances, remote does not; divergence is expected |
| unseen remote commit | remote/local collapse | remote advances, local remains unchanged until fetch/integration |
| same branch name, different SHA/history | alias as version | compare exact refs/ancestry; preserve divergence |
| deleted test branch | history rewrite | current existence ends; prior Claims remain queryable |
| out-of-order observation ingestion | record order as world order | valid/knowledge-time queries remain correct |
| delayed provider/check event | convenient horizon | outcome stays pending/censored until locked horizon |
| provider unavailable | stale state promoted current | return unknown plus last-supported time |
| old Evidence replayed | freshness laundering | original observation time/revision retained; no freshness reset |
| Prediction injected as Observation | self-confirming loop | typed ingestion rejects/reclassifies; cannot become provider Evidence |
| model inference submitted as provider fact | evidence laundering | remains model-derived Claim only |
| provider says success, postcondition absent | receipt as outcome | Outcome remains failed/partial/unknown from re-observation |
| partial material application | assumed atomicity | Outcome records actual partial deltas |
| local path deleted/recreated with another repo | path continuity | new Manifestation unless provider continuity proves otherwise |
| identical-content independent clone | content hash as identity | separate Manifestation; content equality only |
| out-of-band remote mutation | false causal attribution | record unexpected delta and attribution ambiguity |
| correlated browser/local observations | fake independence | retain dependency/source metadata; no automatic confidence multiplication |
| delayed/stale runtime descriptor | descriptor as liveness | live probe wins; descriptor cannot create fresh runtime Claim |
| action parameters changed after Prediction | retroactive prediction | old Prediction ineligible; new ActionAttempt+Prediction required |
| cross-arm state leak | invalid fresh-instance result | trial block invalidated and harness fixed before replacement |
| policy reversal | immortal experience | obsolete expectation loses operational influence within bound |

### 30.1 Correspondence acceptance

For hostile identity cases:

- active `git:working_copy_of` precision must be 100%;
- recall must be at least 95% among cases whose preregistered evidence is sufficient to establish the relationship;
- explicitly ambiguous cases are not forced into the recall denominator;
- there is no Build 001 hard `same_as` operation.

A false active working-copy relation is a hard architecture failure for the suite.

---

## 31. Build 001 invariants

These are implementation-level hard rules.

1. A Prediction eligible for scoring is durably recorded before dispatch.
2. A dispatched ActionAttempt cannot be retrofitted with a different target/action/parameter set.
3. Predictions are immutable.
4. Provider Evidence is immutable and content-addressed.
5. Model inference cannot enter the Observation/provider-report path.
6. Prediction cannot enter the Observation/provider-report path.
7. A provider receipt cannot independently create a verified material Outcome.
8. Every material provider-reported Claim retains Observation/Evidence lineage.
9. Manifestations are never destructively merged.
10. Repository-level `same_as` is unavailable.
11. Correspondence change appends dispositions; it never rewrites prior history.
12. Claim contradiction/supersession appends dispositions; it never rewrites prior history.
13. `valid_as_of` and `known_as_of` remain independently queryable.
14. Kernel `recorded_at` is assigned by the persistence boundary, not trusted from model input.
15. Old Evidence cannot acquire a new `observed_at` merely because it is retrieved/reingested.
16. Provider outage yields unknown/currently-unresolved rather than assumed unchanged.
17. Cached/current projections are rebuildable and cannot be cited as raw provider Evidence.
18. A closed TransitionEpisode has a complete prediction/action/outcome/evaluation lifecycle.
19. Hidden evaluator regime labels never enter world-kernel operator packages.
20. Memory and Structured historical episode selection comes from the same source-episode candidate set.
21. Memory and Structured obey the same inherited-context ceiling.
22. Structured package creation invokes no extra LLM.
23. All confirmatory arms use the same model/version/configuration and base instructions.
24. Every arm run uses a fresh model context.
25. Evaluator ground truth is independent of the operator's self-declared success.
26. World-kernel persistent history is limited to explicit learning episodes/epistemic state; it is not a global Eye action hook.
27. The world kernel implements no permission/approval/policy/risk-tier engine.
28. The world kernel implements no separate verifier agent/pipeline.
29. The world kernel runs as an experiment-owned process, not an additional permanent Windows service.
30. PostgreSQL, if left running during a campaign, is process/runtime mechanics and is not registered as a permanent StealthEye service.
31. CODEeye/eyeBROWSE canonical repositories are not modified merely to satisfy this experiment without a separately justified change.
32. Evaluator setup/reset operations are not counted as learned world actions.
33. Confirmatory primary metric, effect target, and analysis rule cannot change after confirmatory seed reveal.
34. Missing operator probabilities cannot disappear from scoring.
35. No model chain of thought is required or persisted.

Violations of 1-18, 19-25, 26-31, or 33-35 invalidate the affected confirmatory result and may constitute Build 001 FAILURE depending on scope.

---

## 32. Acceptance gates

### Gate A1 - Store/replay integrity

- all required records can be written and queried;
- world-kernel process death/restart preserves durable epistemic state;
- projection rebuild produces identical controlled results;
- no manually maintained authoritative current-world table is required.

### Gate A2 - Bitemporal correctness

100% of deterministic temporal hostile cases must return the preregistered expected answer for:

```text
valid_as_of
known_as_of
current freshness/unknown
```

### Gate A3 - Epistemic type integrity

100% of deliberate prediction/inference/replayed-evidence laundering cases must be rejected or retained under their truthful source class.

### Gate B1 - Correspondence precision

- zero false active `git:working_copy_of` assertions in the hostile suite;
- at least 95% recall on sufficiently evidenced true cases;
- ambiguous cases remain distinct.

### Gate B2 - Locator/identity continuity

Rename, changed remote, path reuse, fork/decoy, and version-equality tests behave according to Section 30 without destructive merge.

### Gate C1 - Complete measured episodes

For 100% of analyzed measured semantic actions:

```text
pre Observation/Evidence
pre Claim references
ActionAttempt
eligible Prediction
dispatch phase
provider receipt if produced
post Observation(s)
Outcome
PredictionEvaluation
closed TransitionEpisode
```

Systematic record incompleteness is a failure, not missing data to ignore.

### Gate C2 - Prediction ordering

100% of analyzed Predictions satisfy the locked pre-dispatch ordering invariant.

Any backfilled/scored-after-the-fact prediction is an experiment-integrity failure.

### Gate C3 - Receipt/outcome separation

All injected receipt-without-postcondition cases remain non-verified until material re-observation supports the postcondition.

### Gate C4 - Fixture isolation

Zero operator material mutations outside evaluator-designated disposable fixture resources.

This is an experiment-integrity condition, not a new Eye permission framework.

### Gate D1 - Primary predictive advantage

Structured versus Memory:

- relative mean block-level Brier-loss reduction >= 20%;
- paired block bootstrap 95% CI for `B_M - B_S` excludes zero in Structured's favor;
- paired randomization test `p < 0.05`;
- at least the pilot-derived required number of complete matched blocks.

### Gate D2 - Behavioral value

Structured versus Memory:

- where Memory has excess-action headroom, at least 20% relative reduction in mean excess material actions;
- no increase in consequential wrong-target/invariant errors;
- task completion no more than 5 percentage points below Memory;
- if Memory has zero excess-action headroom, Structured must be non-degraded.

### Gate D3 - Fresh-instance inheritance

The D1/D2 effect must occur under complete conversational reset.

If the advantage requires chat continuity, the central Build 001 mission fails.

### Gate D4 - Contradiction recovery

At least 90% of drift blocks meet the three-contradiction recovery bound in Section 29.

### Gate D5 - More-than-memory

Outperforming Cold is insufficient.

If Structured does not materially outperform Memory on the locked primary endpoint, no strong world-kernel support exists.

### Gate D6 - Compute/information parity

- same model/config;
- same source historical episodes;
- same inherited package ceiling;
- same operator-call schedule;
- no extra Structured LLM call;
- no hidden evaluator information in treatment.

The experiment cannot claim a structural win bought by more model compute or more history.

---

## 33. Allowed Build 001 conclusions

### PASS - architecture strongly supported

Requires all hard A/B/C integrity gates plus D1-D6.

Meaning:

> Structured provider-grounded transition inheritance has earned another measured build.

It does **not** mean:

- mature world model achieved;
- canonical architecture approved;
- planner/experimentation/skills authorized.

### PARTIAL PASS - kernel works, incremental value not yet decisive

Examples:

- A/B/C integrity passes;
- Structured point estimate beats Memory but misses 20% or CI/p-value criterion;
- prediction improves but behavior is inconclusive;
- contradiction recovery needs one bounded fix;
- pilot/confirmatory variance makes the screen inconclusive.

Permitted next action:

- one targeted replication/simplification addressing the specific unresolved metric.

Not permitted merely from PARTIAL PASS:

- planner;
- active experimentation;
- skills;
- neural world models;
- multi-machine expansion.

### ARCHITECTURE REDUCTION - ordinary memory wins the simplicity argument

Trigger examples:

- Memory matches Structured on prediction and behavior;
- Structured advantage is small relative to complexity;
- typed correspondence/bitemporality adds no measurable decision value;
- treatment win disappears under equal-information packaging.

Preferred direction:

```text
Eyes + ordinary episodic memory + existing tested capabilities
```

Useful narrow temporal/evidence mechanics may survive if they have independent operational value.

### FAILURE - candidate kernel is unsound

Examples:

- false active correspondence;
- broken historical belief reconstruction;
- predictions can be backfilled;
- stale evidence can masquerade as fresh;
- model inference can enter provider Observation path;
- fresh-instance isolation is not real;
- contradiction remains dangerously sticky;
- world-kernel recording expands into uncontrolled/global action capture;
- experiment mutates outside the fixture.

Later builds are forbidden until a narrower redesign is separately justified.

### No confirmatory conclusion

External/harness failure that prevents the pilot-derived minimum valid matched blocks is not reinterpreted as PASS/PARTIAL/FAILURE. Complete the locked protocol or rerun under a newly preregistered campaign.

---

## 34. Build 001 implementation workstreams

These workstreams are implementation order, not authorization in this document.

### Workstream 1 - Repository/project initialization

When separately authorized, initialize one candidate world-kernel repository/project or an explicitly owner-selected location.

Do not modify CODEeye/eyeBROWSE repos for the kernel itself.

Minimum shape:

```text
src/
  WorldKernel.Protocol/
  WorldKernel.Store/
  WorldKernel.Kernel/
  WorldKernel.Adapters.CODEeye/
  WorldKernel.Adapters.EyeBrowse/
  WorldKernel.Evaluator/
tests/
  WorldKernel.IntegrationTests/
  fixtures/
experiments/
  build001/
docs/
```

Split assemblies only when a real boundary exists; this shape is a target, not a ceremony requirement.

### Workstream 2 - PostgreSQL/evidence substrate

Implement:

- version-pinned on-demand PostgreSQL runtime;
- migrations for Section 13;
- content-addressed Evidence storage;
- kernel-assigned record time/sequence;
- projection queries;
- restart/replay tests.

### Workstream 3 - CODEeye adapter and Git facet

Implement canonical pipe integration plus the minimum native Git facet.

Pressure-test current CODEeye IDs rather than copying them as global world identity.

### Workstream 4 - eyeBROWSE adapter/GitHub lens

Implement compact repo/branch/commit/check observations and remote-commit program using existing eyeBROWSE APIs.

No direct GitHub connector in operator path.

### Workstream 5 - epistemic/correspondence kernel

Implement:

- Manifestation/Locator;
- Evidence/Observation/Claim;
- dispositions;
- `git:working_copy_of` resolver;
- valid/knowledge-time query;
- hostile identity tests.

### Workstream 6 - action/prediction/outcome lifecycle

Implement:

- ActionAttempt;
- Prediction;
- transactional seal/dispatch cutoff;
- receipts;
- post-observation Outcome;
- deterministic scorer;
- TransitionEpisode closure.

### Workstream 7 - deterministic package serializers

Implement from the same candidate episode selection:

- Memory serializer;
- Structured serializer;
- Cold empty state;
- token/byte budget enforcement;
- package hash/version.

No LLM summarizer.

### Workstream 8 - fixture/evaluator

Implement:

- seeded reset;
- hidden provider regimes;
- ground truth;
- action proposition evaluator;
- oracle minimum action count;
- arm randomization;
- hidden-data separation.

### Workstream 9 - preflight + acquisition + pilot

Pass P0-P6, generate acquisition corpus, run exactly 12 pilot blocks, mechanically freeze confirmatory N/horizons/package budget.

### Workstream 10 - confirmatory run

Run the locked matched blocks with no architecture/metric changes.

### Workstream 11 - measured result artifact

Produce one Build 001 results document containing:

- exact commits/runtime versions;
- prereg hash/config;
- hard gate results;
- primary/secondary statistics;
- hostile-suite results;
- drift results;
- overhead;
- defects fixed during implementation;
- explicit remaining boundaries;
- PASS/PARTIAL/REDUCTION/FAILURE conclusion.

This results artifact is intrinsic experimental evidence, not a generic Eye receipt system.

---

## 35. Required integration tests before pilot

Minimum deterministic tests:

### Storage/temporal

- evidence hash immutability;
- kernel record-time monotonicity;
- claim supersession at later knowledge time;
- out-of-order observation;
- stale evidence replay;
- projection rebuild after process restart;
- current unknown when freshness expires/provider unavailable.

### Epistemic type

- Prediction cannot be ingested as Observation;
- model-inferred Claim remains model-inferred after later corroboration;
- provider receipt cannot create verified Outcome;
- summary/current projection cannot be accepted as raw Evidence.

### Identity/correspondence

- exact remote + shared commit activates `git:working_copy_of`;
- same basename without remote evidence does not;
- changed remote disputes old relation;
- rename with strong remote identity preserves remote Manifestation/updates Locator;
- local path recreation creates new Manifestation absent strong continuity;
- fork/shared commit does not merge manifestations;
- stale browser locator does not override fresh provider evidence.

### Prediction/action

- Prediction before dispatch eligible;
- Prediction after dispatch rejected/ineligible;
- changed action parameters require new ActionAttempt;
- one dispatch only;
- partial outcome scores only resolved components according to locked rules;
- async horizon cannot be moved after outcome.

### Packaging

- same candidate episode IDs feed Memory/Structured;
- equal budget enforcement;
- hidden evaluator fields impossible to serialize through the operator package schema;
- package generation is deterministic for same DB snapshot/query.

### Original Eye constraints

- no approval/risk/policy tables/endpoints;
- no global action hook;
- no verifier-agent invocation;
- no permanent SCM-service installation by world-kernel setup;
- no CODEeye/eyeBROWSE canonical API changes required for the test.

---

## 36. Pilot/confirmatory preregistration freeze procedure

Before confirmatory execution, generate a machine-readable preregistration containing:

```text
spec version/hash
Eye/CODEeye/eyeBROWSE commits
world-kernel commit
model/version/config
PostgreSQL/Git/Chrome versions
fixture repository/provider identity
six action schemas
evaluation spec hash
H1/H2/H3
inherited token/byte ceiling
acquisition corpus hash/episode IDs
pilot block IDs (excluded)
confirmatory N
confirmatory seed commitment/hash
randomization algorithm/version
primary metric
20% target
bootstrap/randomization procedures
behavioral metric/gate
contradiction gate
hard invariants
missing-data/replacement rules
serializer versions
scorer version/hash
```

The preregistration is hashed before confirmatory seeds are used.

Hashing a preregistration is ordinary experimental integrity. It does not create an Eye receipt/proof subsystem.

Any material change after freeze creates a new preregistration version and a new confirmatory campaign. It cannot be applied retroactively to salvage a result.

---

## 37. Missing-data and retry policy

### Allowed block replacement before outcome reveal

A block may be replaced under the locked seed-generation rule when a genuine external harness failure occurs before any arm outcome is revealed, such as:

- fixture reset failed integrity check;
- provider-wide outage prevents trial start;
- fresh-invocation mechanism failed before operator output;
- database/kernel failure prevented any valid trial record.

### Not replaceable as "infrastructure failure"

These are experiment data:

- provider action rejection predicted incorrectly;
- operator timeout inside the locked task deadline;
- unknown/censored outcome at the locked horizon;
- model format error;
- model chooses an unnecessary action;
- eyeBROWSE stale observation causes an incorrect decision;
- action fails under hidden provider policy.

Do not retry model mistakes until they become successes.

---

## 38. Fresh-instance operator output contract

Each scored decision must emit a machine-readable action/prediction object before dispatch.

Conceptual form:

```json
{
  "action": {
    "type": "git:push_ref",
    "target": "<manifestation/ref>",
    "parameters": {"...": "..."}
  },
  "prediction": {
    "evaluation_spec": "wk001-push-v1",
    "probabilities": {
      "provider_accepts_push": 0.72,
      "remote_ref_exists_at_H1": 0.70,
      "remote_ref_equals_local_head_at_H1": 0.68,
      "local_head_changes_because_of_push": 0.03,
      "local_worktree_changes_because_of_push": 0.02,
      "remote_check_starts_by_H2": 0.55,
      "remote_check_terminal_success_by_H3": 0.45,
      "browser_presentation_reflects_new_remote_head_by_H1": 0.60
    },
    "expected_deltas": ["..."],
    "expected_invariants": ["..."]
  }
}
```

The experiment does not need or persist private chain-of-thought. A short user-visible explanation may be produced for debugging, but it is not part of world learning or primary scoring.

---

## 39. Current implementation blockers/gaps identified before build

These are known specification findings, not implementation failures yet.

### Gap 1 - CODEeye Git mutation breadth

Current public CODEeye RPC lacks commit/push/fetch/ff-only integration and remote/ref inspection required by the experiment.

Resolution: experiment-owned native Git facet, not immediate CODEeye expansion.

### Gap 2 - PostgreSQL absent

`psql` is not currently discoverable on the machine.

Resolution: Build 001 implementation must provision a version-pinned PostgreSQL 18 runtime without creating a permanent Windows service.

### Gap 3 - Node not on LocalSystem PATH

Portable Node exists under AgentBrowser, but ambient SYSTEM `node` is unavailable.

Resolution: do not rely on PATH. Use explicit runtime paths if an existing Program Host is invoked.

### Gap 4 - persisted descriptors can be stale

Runtime descriptors may exist while their described kernel process is not currently running.

Resolution: every trial/preflight uses live pipe/provider/browser probes.

### Gap 5 - authenticated GitHub browser action must be proven

Current eyeBROWSE Build 001 demonstrated real GitHub observation, but the world experiment requires authenticated browser-side write capability to its disposable fixture.

Resolution: P2 preflight proves this before acquisition.

### Gap 6 - repeatable fresh ChatGPT trial mechanism

The architecture requires genuine fresh invocations; the current specification does not assume a particular UI/API harness.

Resolution: P5 must establish and freeze a reproducible invocation adapter before pilot.

No confirmatory trial begins while any gap remains unresolved.

---

## 40. Technology decisions and intentionally deferred choices

### Frozen for Build 001

- .NET 10 world-kernel implementation unless concrete blocker;
- PostgreSQL 18.x relational store;
- PostgreSQL range/time semantics for bitemporal claims;
- content-addressed immutable Evidence blobs;
- relational correspondence/provenance;
- JSONB only for namespaced/provider-specific payloads;
- stock Git provider;
- canonical CODEeye named-pipe API plus experiment-only native Git facet;
- canonical eyeBROWSE named-pipe API;
- deterministic package serializers;
- no extra permanent Windows service;
- one frontier ChatGPT operator at a time.

### Deliberately deferred

- graph engine;
- vectors/embeddings;
- learned rules/DSL;
- capability reliability model;
- general retrieval engine;
- neural predictor;
- planner;
- experiment engine;
- skill compiler;
- multi-machine transport;
- product name;
- permanent service topology for any future world system.

---

## 41. What Build 001 is allowed to teach us

A PASS would justify only the next question:

> Can persistent beliefs/experience die correctly and remain useful under contradiction/regime change as the corpus grows?

A PASS does not authorize the mature architecture ladder automatically.

Candidate future order remains:

```text
Build 002 belief revision / regime change
Build 003 context reconstruction at scale
Build 004 scoped empirical rules/dynamics
Build 005 third-Eye transfer
Build 006 selective foresight
Build 007 simulator + capability routing/self-model
Build 008 planning
Build 009 active experimentation
Build 010 skill crystallization
conditional residual neural predictors
conditional model-generation inheritance
conditional multi-machine federation
```

Every later slice requires its own explicit authorization and acceptance gates.

---

## 42. Architecture reduction rule

If Structured does not beat Conventional Memory enough to justify its complexity, the project must not preserve the world kernel merely because it is elegant.

The default reduction is:

```text
ChatGPT
   |
current native Eyes
   |
ordinary bounded episodic/trajectory memory
   |
existing tested capabilities
```

Provider-grounded observations and simple continuity may survive where independently useful.

The project is not entitled to be a world model.

---

## 43. Build 001 completion statement template

A future results document must end with exactly one of:

```text
Build 001 result: PASS - structured provider-grounded transition inheritance materially outperformed conventional memory under the preregistered fresh-instance test.
```

```text
Build 001 result: PARTIAL PASS - kernel integrity held, but structured incremental value did not meet every preregistered strong-pass gate; only the identified bounded follow-up is justified.
```

```text
Build 001 result: ARCHITECTURE REDUCTION - conventional memory matched the structured architecture closely enough that the larger world-kernel structure is not justified.
```

```text
Build 001 result: FAILURE - one or more fundamental identity, temporal, epistemic, prediction-order, isolation, or contradiction invariants failed.
```

No infrastructure-completion wording may substitute for one of these empirical conclusions.

---

## 44. Grounding references used for this specification

### StealthEye repositories

- `StealthEyeLLC/eye` at `53948b74701f51c29c9322dfa9f017ba6b45f4a4`
  - `README.md`
  - `docs/EYE_CANON.md`
- `StealthEyeLLC/CODEeye` at `1ca0f93d64bc20bccb3b96dbcda43a2232783609`
  - `docs/00-CHARTER.md`
  - `docs/01-ARCHITECTURE.md`
  - `docs/02-BUILD-001-SLICE.md`
  - `docs/04-ROADMAP.md`
  - `docs/06-DECISIONS.md`
  - `docs/07-CAPABILITY-MATRIX.md`
  - `docs/09-BUILD-001-RESULTS.md`
  - `src/CODEeye.Kernel/Program.cs`
  - `src/CODEeye.Kernel/WorldEngine.cs`
  - `src/CODEeye.Kernel/WorldEngine.Queries.cs`
  - `src/CODEeye.World/GitWorld.cs`
  - `src/CODEeye.World/WorldStore.cs`
  - `program-host/sdk/codeeye.mjs`
- `StealthEyeLLC/eyebrowse` at `2e27f44ebd3522d0d26b036dc57f790535df3533`
  - `docs/00-CHARTER.md`
  - `docs/01-ARCHITECTURE.md`
  - `docs/06-DECISIONS.md`
  - `docs/07-CAPABILITY-MATRIX.md`
  - `docs/09-BUILD-001-RESULTS.md`
  - `program-host/sdk/eyebrowse.mjs`

### Current external technical references

- PostgreSQL 18 official documentation/release material for range/temporal constraints and `WITHOUT OVERLAPS` / `PERIOD` support.
- GitHub official branch/protected-branch documentation and current REST API version `2026-03-10` for real provider-side branch policy/check fixtures.

### Machine observations

Live `STEALTHEYELLC` inspection during specification established the runtime/toolchain facts in Section 2.4 and the descriptor-liveness caveat.

---

## 45. Final specification conclusion

Build 001 is now specification-complete at the candidate level.

The slice is deliberately smaller than the broader research architecture:

```text
real Git/GitHub world
     |
CODEeye + eyeBROWSE
     |
small bitemporal epistemic transition kernel
     |
pre-action Prediction -> real action -> fresh Outcome -> score
     |
TransitionEpisodes
     |
fresh ChatGPT inheritance
     |
Cold vs Memory vs Structured
```

It preserves the original Eye constraints:

- no extra authority friction;
- no policy/approval/risk-tier architecture;
- no separate verifier agent/pipeline;
- no generic action ledger/receipt product;
- no second autonomous brain;
- no new permanent Windows service;
- no silent takeover of sibling semantics;
- no architecture components without a measured Build 001 purpose.

The next legitimate phase, if separately authorized, is implementation of this exact slice, followed by acquisition, pilot freeze, and confirmatory execution.

Implementation may fix concrete defects and resolve integration details inside these boundaries. It may not silently change the primary hypothesis, arm fairness, kernel ontology, provider-authority rule, or acceptance metric after confirmatory evidence begins.

**Candidate Build 001 specification status: COMPLETE / READY FOR SEPARATE IMPLEMENTATION AUTHORIZATION.**
