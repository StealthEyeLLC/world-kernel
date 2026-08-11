BEGIN;

CREATE SCHEMA IF NOT EXISTS wk;

CREATE OR REPLACE FUNCTION wk.force_recorded_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.recorded_at := clock_timestamp();
  RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION wk.deny_history_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'Build 001 history is append-only: %.% cannot be changed by %', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP
    USING ERRCODE = '55000';
END;
$$;

CREATE TABLE IF NOT EXISTS wk.manifestation (
  manifestation_id uuid PRIMARY KEY,
  provider_namespace text NOT NULL,
  manifestation_kind text NOT NULL,
  identity_basis jsonb NOT NULL,
  incarnation_key text NOT NULL,
  provider_native_id text,
  observer_native_ids jsonb NOT NULL DEFAULT '{}'::jsonb,
  first_observation_id uuid,
  display_label text,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (jsonb_typeof(identity_basis) = 'object'),
  CHECK (jsonb_typeof(observer_native_ids) = 'object'),
  UNIQUE (provider_namespace, provider_native_id, incarnation_key)
);

CREATE TABLE IF NOT EXISTS wk.evidence (
  evidence_id uuid PRIMARY KEY,
  provider_namespace text NOT NULL,
  observer_name text NOT NULL,
  captured_at timestamptz NOT NULL,
  hash_algorithm text NOT NULL CHECK (hash_algorithm = 'sha256'),
  content_hash character(64) NOT NULL CHECK (content_hash ~ '^[0-9a-f]{64}$'),
  blob_ref text NOT NULL,
  media_type text NOT NULL,
  acquisition_method text NOT NULL,
  byte_length bigint NOT NULL CHECK (byte_length >= 0),
  provider_revision text,
  provider_event_at timestamptz,
  encoding text,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE INDEX IF NOT EXISTS evidence_content_address_idx
  ON wk.evidence(hash_algorithm, content_hash);

CREATE TABLE IF NOT EXISTS wk.observation (
  observation_id uuid PRIMARY KEY,
  target_manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  observer_name text NOT NULL,
  observer_version text NOT NULL,
  provider_namespace text NOT NULL,
  observed_at timestamptz NOT NULL,
  acquisition_status text NOT NULL CHECK (acquisition_status IN ('succeeded','partial','stale','outage','failed')),
  coverage jsonb NOT NULL CHECK (jsonb_typeof(coverage) = 'object'),
  provider_revision text,
  provider_event_at timestamptz,
  source_dependency jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(source_dependency) = 'object'),
  raw_normalized_payload jsonb,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

ALTER TABLE wk.manifestation
  DROP CONSTRAINT IF EXISTS manifestation_first_observation_fk;
ALTER TABLE wk.manifestation
  ADD CONSTRAINT manifestation_first_observation_fk
  FOREIGN KEY (first_observation_id) REFERENCES wk.observation(observation_id) DEFERRABLE INITIALLY DEFERRED;

CREATE TABLE IF NOT EXISTS wk.locator (
  locator_id uuid PRIMARY KEY,
  manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  locator_namespace text NOT NULL,
  locator_type text NOT NULL,
  locator_value text NOT NULL,
  valid_range tstzrange NOT NULL CHECK (NOT isempty(valid_range)),
  source_observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  supersedes_locator_id uuid REFERENCES wk.locator(locator_id),
  normalization_metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(normalization_metadata) = 'object'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS wk.observation_evidence (
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  evidence_id uuid NOT NULL REFERENCES wk.evidence(evidence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (observation_id, evidence_id)
);

CREATE TABLE IF NOT EXISTS wk.claim (
  claim_id uuid PRIMARY KEY,
  subject_manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  predicate_namespace text NOT NULL,
  predicate text NOT NULL,
  value_json jsonb NOT NULL,
  production_method text NOT NULL CHECK (production_method IN ('provider_reported','observed','inferred','derived','operator_asserted')),
  authority_class text NOT NULL CHECK (authority_class IN ('material','provider','epistemic','derived')),
  valid_range tstzrange NOT NULL CHECK (NOT isempty(valid_range)),
  scope jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(scope) = 'object'),
  producer jsonb NOT NULL CHECK (jsonb_typeof(producer) = 'object'),
  subject_selector jsonb,
  confidence double precision CHECK (confidence >= 0.0 AND confidence <= 1.0),
  freshness_policy_id text,
  primary_observation_id uuid REFERENCES wk.observation(observation_id),
  primary_evidence_id uuid REFERENCES wk.evidence(evidence_id),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (
    authority_class NOT IN ('material','provider') OR
    (primary_observation_id IS NOT NULL AND primary_evidence_id IS NOT NULL)
  ),
  CHECK (
    production_method NOT IN ('provider_reported','observed') OR
    (primary_observation_id IS NOT NULL AND primary_evidence_id IS NOT NULL)
  )
);

CREATE TABLE IF NOT EXISTS wk.claim_observation (
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (claim_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.claim_evidence (
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  evidence_id uuid NOT NULL REFERENCES wk.evidence(evidence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (claim_id, evidence_id)
);

CREATE TABLE IF NOT EXISTS wk.claim_derivation (
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  source_claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (claim_id, source_claim_id),
  CHECK (claim_id <> source_claim_id)
);

CREATE TABLE IF NOT EXISTS wk.claim_disposition (
  claim_disposition_id uuid PRIMARY KEY,
  target_claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  relation text NOT NULL CHECK (relation IN ('supports','disputes','supersedes','withdraws')),
  effective_valid_at timestamptz NOT NULL,
  basis jsonb NOT NULL CHECK (jsonb_typeof(basis) = 'object'),
  producer jsonb NOT NULL CHECK (jsonb_typeof(producer) = 'object'),
  replacement_claim_id uuid REFERENCES wk.claim(claim_id),
  rationale_code text,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (relation <> 'supersedes' OR replacement_claim_id IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS wk.correspondence_claim (
  correspondence_id uuid PRIMARY KEY,
  left_manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  relation_namespace text NOT NULL CHECK (relation_namespace = 'git'),
  relation_type text NOT NULL CHECK (relation_type = 'working_copy_of'),
  right_manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  method text NOT NULL,
  confidence double precision NOT NULL CHECK (confidence >= 0.0 AND confidence <= 1.0),
  strength text NOT NULL CHECK (strength IN ('candidate','hard')),
  valid_range tstzrange NOT NULL CHECK (NOT isempty(valid_range)),
  producer jsonb NOT NULL CHECK (jsonb_typeof(producer) = 'object'),
  basis_fingerprint character(64) NOT NULL CHECK (basis_fingerprint ~ '^[0-9a-f]{64}$'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (left_manifestation_id <> right_manifestation_id)
);

CREATE TABLE IF NOT EXISTS wk.correspondence_observation (
  correspondence_id uuid NOT NULL REFERENCES wk.correspondence_claim(correspondence_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (correspondence_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.correspondence_evidence (
  correspondence_id uuid NOT NULL REFERENCES wk.correspondence_claim(correspondence_id),
  evidence_id uuid NOT NULL REFERENCES wk.evidence(evidence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (correspondence_id, evidence_id)
);

CREATE TABLE IF NOT EXISTS wk.correspondence_claim_basis (
  correspondence_id uuid NOT NULL REFERENCES wk.correspondence_claim(correspondence_id),
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (correspondence_id, claim_id)
);

CREATE TABLE IF NOT EXISTS wk.correspondence_disposition (
  correspondence_disposition_id uuid PRIMARY KEY,
  target_correspondence_id uuid NOT NULL REFERENCES wk.correspondence_claim(correspondence_id),
  relation text NOT NULL CHECK (relation IN ('supports','disputes','supersedes','withdraws')),
  effective_valid_at timestamptz NOT NULL,
  basis jsonb NOT NULL CHECK (jsonb_typeof(basis) = 'object'),
  replacement_correspondence_id uuid REFERENCES wk.correspondence_claim(correspondence_id),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (relation <> 'supersedes' OR replacement_correspondence_id IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS wk.evaluation_spec (
  evaluation_spec_version text PRIMARY KEY,
  evaluation_spec_hash character(64) NOT NULL UNIQUE CHECK (evaluation_spec_hash ~ '^[0-9a-f]{64}$'),
  scorer_version text NOT NULL,
  definition jsonb NOT NULL CHECK (jsonb_typeof(definition) = 'object'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS wk.evaluation_proposition (
  evaluation_spec_version text NOT NULL REFERENCES wk.evaluation_spec(evaluation_spec_version),
  semantic_action_namespace text NOT NULL,
  semantic_action_type text NOT NULL,
  ordinal integer NOT NULL CHECK (ordinal > 0),
  proposition_key text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (evaluation_spec_version, semantic_action_namespace, semantic_action_type, proposition_key),
  UNIQUE (evaluation_spec_version, semantic_action_namespace, semantic_action_type, ordinal)
);

INSERT INTO wk.evaluation_spec (
  evaluation_spec_version, evaluation_spec_hash, scorer_version, definition
) VALUES (
  'build001-evaluation-v1',
  'f182f6b0ac91d85436f077f0c78e3db0ec3b35d2f15f82d0675b699598c93ded',
  'build001-brier-v1',
  '{"source":"schemas/evaluation-spec-v1.json","complete_vectors_required":true}'::jsonb
)
ON CONFLICT (evaluation_spec_version) DO NOTHING;

INSERT INTO wk.evaluation_proposition
  (evaluation_spec_version, semantic_action_namespace, semantic_action_type, ordinal, proposition_key)
VALUES
  ('build001-evaluation-v1','git','create_local_commit',1,'provider_accepts_action'),
  ('build001-evaluation-v1','git','create_local_commit',2,'local_head_changes'),
  ('build001-evaluation-v1','git','create_local_commit',3,'local_head_equals_new_commit'),
  ('build001-evaluation-v1','git','create_local_commit',4,'remote_target_ref_changes_before_push'),
  ('build001-evaluation-v1','git','create_local_commit',5,'local_worktree_clean_after'),
  ('build001-evaluation-v1','git','create_local_commit',6,'current_branch_name_changes'),
  ('build001-evaluation-v1','git','create_local_commit',7,'new_commit_reachable_locally'),
  ('build001-evaluation-v1','git','create_local_commit',8,'new_commit_reachable_remotely_before_push'),
  ('build001-evaluation-v1','git','create_branch',1,'provider_accepts_action'),
  ('build001-evaluation-v1','git','create_branch',2,'new_local_branch_exists'),
  ('build001-evaluation-v1','git','create_branch',3,'current_branch_is_new_branch'),
  ('build001-evaluation-v1','git','create_branch',4,'local_head_sha_changes'),
  ('build001-evaluation-v1','git','create_branch',5,'remote_branch_exists_before_push'),
  ('build001-evaluation-v1','git','create_branch',6,'worktree_content_changes'),
  ('build001-evaluation-v1','git','push_ref',1,'provider_accepts_push'),
  ('build001-evaluation-v1','git','push_ref',2,'remote_ref_exists_at_H1'),
  ('build001-evaluation-v1','git','push_ref',3,'remote_ref_equals_local_head_at_H1'),
  ('build001-evaluation-v1','git','push_ref',4,'local_head_changes_because_of_push'),
  ('build001-evaluation-v1','git','push_ref',5,'local_worktree_changes_because_of_push'),
  ('build001-evaluation-v1','git','push_ref',6,'remote_check_starts_by_H2'),
  ('build001-evaluation-v1','git','push_ref',7,'remote_check_terminal_success_by_H3'),
  ('build001-evaluation-v1','git','push_ref',8,'browser_presentation_reflects_new_remote_head_by_H1'),
  ('build001-evaluation-v1','github','create_remote_commit',1,'provider_accepts_action'),
  ('build001-evaluation-v1','github','create_remote_commit',2,'remote_head_changes'),
  ('build001-evaluation-v1','github','create_remote_commit',3,'remote_head_equals_new_hosted_commit'),
  ('build001-evaluation-v1','github','create_remote_commit',4,'local_head_changes_before_fetch'),
  ('build001-evaluation-v1','github','create_remote_commit',5,'local_worktree_changes_before_fetch'),
  ('build001-evaluation-v1','github','create_remote_commit',6,'local_remote_tracking_ref_changes_before_fetch'),
  ('build001-evaluation-v1','github','create_remote_commit',7,'new_remote_commit_reachable_locally_before_fetch'),
  ('build001-evaluation-v1','github','create_remote_commit',8,'browser_presentation_reflects_new_remote_commit_by_H1'),
  ('build001-evaluation-v1','git','fetch_remote',1,'provider_accepts_action'),
  ('build001-evaluation-v1','git','fetch_remote',2,'local_head_changes'),
  ('build001-evaluation-v1','git','fetch_remote',3,'local_worktree_changes'),
  ('build001-evaluation-v1','git','fetch_remote',4,'remote_tracking_ref_equals_remote_head_at_H1'),
  ('build001-evaluation-v1','git','fetch_remote',5,'remote_head_changes_because_of_fetch'),
  ('build001-evaluation-v1','git','fetch_remote',6,'remote_commit_reachable_locally_after_fetch'),
  ('build001-evaluation-v1','git','fetch_remote',7,'checked_out_branch_content_changes'),
  ('build001-evaluation-v1','git','integrate_fast_forward',1,'fast_forward_is_accepted'),
  ('build001-evaluation-v1','git','integrate_fast_forward',2,'local_head_equals_remote_target_after_H1'),
  ('build001-evaluation-v1','git','integrate_fast_forward',3,'local_head_changes'),
  ('build001-evaluation-v1','git','integrate_fast_forward',4,'local_worktree_content_changes'),
  ('build001-evaluation-v1','git','integrate_fast_forward',5,'local_worktree_clean_after'),
  ('build001-evaluation-v1','git','integrate_fast_forward',6,'remote_head_changes_because_of_integration'),
  ('build001-evaluation-v1','git','integrate_fast_forward',7,'merge_commit_created')
ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS wk.action_attempt (
  action_id uuid PRIMARY KEY,
  trial_id text NOT NULL,
  configuration_block_id text NOT NULL,
  arm text NOT NULL CHECK (arm IN ('cold','memory','structured','acquisition','pilot','hostile','drift')),
  target_manifestations jsonb NOT NULL CHECK (jsonb_typeof(target_manifestations) = 'array'),
  owning_eye text NOT NULL,
  capability_name text NOT NULL,
  capability_version text NOT NULL,
  semantic_action_namespace text NOT NULL,
  semantic_action_type text NOT NULL,
  parameters jsonb NOT NULL CHECK (jsonb_typeof(parameters) = 'object'),
  parameters_hash character(64) NOT NULL CHECK (parameters_hash ~ '^[0-9a-f]{64}$'),
  evaluation_spec_version text NOT NULL REFERENCES wk.evaluation_spec(evaluation_spec_version),
  evaluation_spec_hash character(64) NOT NULL,
  producer_model jsonb NOT NULL CHECK (jsonb_typeof(producer_model) = 'object'),
  fixture_scope_id text NOT NULL,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  declared_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  created_txid bigint NOT NULL DEFAULT ((pg_current_xact_id())::text::bigint),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (
    (semantic_action_namespace, semantic_action_type) IN (
      ('git','create_local_commit'),
      ('git','create_branch'),
      ('git','push_ref'),
      ('github','create_remote_commit'),
      ('git','fetch_remote'),
      ('git','integrate_fast_forward')
    )
  )
);

CREATE TABLE IF NOT EXISTS wk.action_precondition_claim (
  action_id uuid NOT NULL REFERENCES wk.action_attempt(action_id),
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (action_id, claim_id)
);

CREATE TABLE IF NOT EXISTS wk.action_precondition_observation (
  action_id uuid NOT NULL REFERENCES wk.action_attempt(action_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (action_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.action_target (
  action_id uuid NOT NULL REFERENCES wk.action_attempt(action_id),
  manifestation_id uuid NOT NULL REFERENCES wk.manifestation(manifestation_id),
  target_role text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (action_id, manifestation_id, target_role)
);

CREATE TABLE IF NOT EXISTS wk.prediction (
  prediction_id uuid PRIMARY KEY,
  action_id uuid NOT NULL UNIQUE REFERENCES wk.action_attempt(action_id),
  evaluation_spec_version text NOT NULL REFERENCES wk.evaluation_spec(evaluation_spec_version),
  evaluation_spec_hash character(64) NOT NULL CHECK (evaluation_spec_hash ~ '^[0-9a-f]{64}$'),
  outcome_probabilities jsonb NOT NULL CHECK (jsonb_typeof(outcome_probabilities) = 'object'),
  expected_deltas jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(expected_deltas) = 'object'),
  expected_invariants jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(expected_invariants) = 'object'),
  horizons jsonb NOT NULL CHECK (jsonb_typeof(horizons) = 'object'),
  mechanism text NOT NULL,
  mechanism_version text NOT NULL,
  producer_model jsonb NOT NULL CHECK (jsonb_typeof(producer_model) = 'object'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  created_txid bigint NOT NULL DEFAULT ((pg_current_xact_id())::text::bigint),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS wk.prediction_assumed_claim (
  prediction_id uuid NOT NULL REFERENCES wk.prediction(prediction_id),
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (prediction_id, claim_id)
);

CREATE TABLE IF NOT EXISTS wk.prediction_basis_evidence (
  prediction_id uuid NOT NULL REFERENCES wk.prediction(prediction_id),
  evidence_id uuid NOT NULL REFERENCES wk.evidence(evidence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (prediction_id, evidence_id)
);

CREATE TABLE IF NOT EXISTS wk.action_phase (
  action_phase_id uuid PRIMARY KEY,
  action_id uuid NOT NULL REFERENCES wk.action_attempt(action_id),
  phase text NOT NULL CHECK (phase IN (
    'dispatched','provider_acknowledged','post_observed','outcome_resolved','evaluated','interrupted'
  )),
  occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  payload jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(payload) = 'object'),
  evidence_id uuid REFERENCES wk.evidence(evidence_id),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE UNIQUE INDEX IF NOT EXISTS action_phase_one_dispatch
  ON wk.action_phase(action_id) WHERE phase = 'dispatched';

CREATE TABLE IF NOT EXISTS wk.outcome (
  outcome_id uuid PRIMARY KEY,
  action_id uuid NOT NULL REFERENCES wk.action_attempt(action_id),
  horizon_id text NOT NULL,
  resolution_status text NOT NULL CHECK (resolution_status IN ('verified','partial','failed','unknown','censored')),
  actual_propositions jsonb NOT NULL CHECK (jsonb_typeof(actual_propositions) = 'object'),
  actual_deltas jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(actual_deltas) = 'object'),
  actual_invariants jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(actual_invariants) = 'object'),
  attribution_status text NOT NULL CHECK (attribution_status IN ('consistent_with_action','confounded_or_ambiguous','not_attributed')),
  resolver_version text NOT NULL,
  resolved_at timestamptz NOT NULL,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (action_id, horizon_id)
);

CREATE TABLE IF NOT EXISTS wk.outcome_observation (
  outcome_id uuid NOT NULL REFERENCES wk.outcome(outcome_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (outcome_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.outcome_evidence (
  outcome_id uuid NOT NULL REFERENCES wk.outcome(outcome_id),
  evidence_id uuid NOT NULL REFERENCES wk.evidence(evidence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (outcome_id, evidence_id)
);

CREATE TABLE IF NOT EXISTS wk.prediction_evaluation (
  evaluation_id uuid PRIMARY KEY,
  prediction_id uuid NOT NULL REFERENCES wk.prediction(prediction_id),
  outcome_id uuid NOT NULL REFERENCES wk.outcome(outcome_id),
  eligibility_status text NOT NULL CHECK (eligibility_status IN ('eligible','ineligible','censored','unknown')),
  scorer_version text NOT NULL,
  mean_brier_loss double precision CHECK (mean_brier_loss >= 0.0 AND mean_brier_loss <= 1.0),
  brier_components jsonb NOT NULL CHECK (jsonb_typeof(brier_components) = 'object'),
  delta_tp integer NOT NULL DEFAULT 0 CHECK (delta_tp >= 0),
  delta_fp integer NOT NULL DEFAULT 0 CHECK (delta_fp >= 0),
  delta_fn integer NOT NULL DEFAULT 0 CHECK (delta_fn >= 0),
  delta_precision double precision CHECK (delta_precision >= 0.0 AND delta_precision <= 1.0),
  delta_recall double precision CHECK (delta_recall >= 0.0 AND delta_recall <= 1.0),
  delta_f1 double precision CHECK (delta_f1 >= 0.0 AND delta_f1 <= 1.0),
  invariant_violations jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(invariant_violations) = 'array'),
  latency_metrics jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(latency_metrics) = 'object'),
  censor_notes text,
  evaluated_at timestamptz NOT NULL,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (prediction_id, outcome_id, scorer_version),
  CHECK (eligibility_status <> 'eligible' OR mean_brier_loss IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS wk.transition_episode (
  episode_id uuid PRIMARY KEY,
  trial_id text NOT NULL,
  configuration_block_id text NOT NULL,
  arm text NOT NULL CHECK (arm IN ('cold','memory','structured','acquisition','pilot','hostile','drift')),
  action_id uuid NOT NULL UNIQUE REFERENCES wk.action_attempt(action_id),
  prediction_id uuid NOT NULL UNIQUE REFERENCES wk.prediction(prediction_id),
  public_environment_scope jsonb NOT NULL CHECK (jsonb_typeof(public_environment_scope) = 'object'),
  environment_fingerprint character(64) NOT NULL CHECK (environment_fingerprint ~ '^[0-9a-f]{64}$'),
  producer_versions jsonb NOT NULL CHECK (jsonb_typeof(producer_versions) = 'object'),
  closed_at timestamptz NOT NULL,
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS wk.episode_correspondence (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  correspondence_id uuid NOT NULL REFERENCES wk.correspondence_claim(correspondence_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, correspondence_id)
);

CREATE TABLE IF NOT EXISTS wk.episode_pre_observation (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.episode_pre_claim (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  claim_id uuid NOT NULL REFERENCES wk.claim(claim_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, claim_id)
);

CREATE TABLE IF NOT EXISTS wk.episode_post_observation (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  observation_id uuid NOT NULL REFERENCES wk.observation(observation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, observation_id)
);

CREATE TABLE IF NOT EXISTS wk.episode_outcome (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  outcome_id uuid NOT NULL REFERENCES wk.outcome(outcome_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, outcome_id)
);

CREATE TABLE IF NOT EXISTS wk.episode_evaluation (
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  evaluation_id uuid NOT NULL REFERENCES wk.prediction_evaluation(evaluation_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (episode_id, evaluation_id)
);

CREATE TABLE IF NOT EXISTS wk.prediction_basis_episode (
  prediction_id uuid NOT NULL REFERENCES wk.prediction(prediction_id),
  episode_id uuid NOT NULL REFERENCES wk.transition_episode(episode_id),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (prediction_id, episode_id)
);

CREATE TABLE IF NOT EXISTS wk.package_artifact (
  package_id uuid PRIMARY KEY,
  arm text NOT NULL CHECK (arm IN ('memory','structured')),
  serializer_version text NOT NULL,
  serializer_hash character(64) NOT NULL CHECK (serializer_hash ~ '^[0-9a-f]{64}$'),
  source_episode_ids uuid[] NOT NULL,
  content_hash character(64) NOT NULL CHECK (content_hash ~ '^[0-9a-f]{64}$'),
  byte_length bigint NOT NULL CHECK (byte_length >= 0 AND byte_length <= 32768),
  estimated_tokens integer NOT NULL CHECK (estimated_tokens >= 0 AND estimated_tokens <= 8000),
  blob_ref text NOT NULL,
  selection_spec jsonb NOT NULL CHECK (jsonb_typeof(selection_spec) = 'object'),
  schema_version integer NOT NULL DEFAULT 1 CHECK (schema_version = 1),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (arm, serializer_version, content_hash)
);

CREATE INDEX IF NOT EXISTS claim_subject_predicate_idx
  ON wk.claim(subject_manifestation_id, predicate_namespace, predicate, recorded_at);
CREATE INDEX IF NOT EXISTS claim_valid_range_gist_idx ON wk.claim USING gist(valid_range);
CREATE INDEX IF NOT EXISTS locator_manifestation_idx ON wk.locator(manifestation_id, recorded_at);
CREATE INDEX IF NOT EXISTS locator_valid_range_gist_idx ON wk.locator USING gist(valid_range);
CREATE INDEX IF NOT EXISTS observation_manifestation_idx ON wk.observation(target_manifestation_id, recorded_at);
CREATE INDEX IF NOT EXISTS evidence_recorded_idx ON wk.evidence(recorded_at);
CREATE INDEX IF NOT EXISTS correspondence_endpoints_idx ON wk.correspondence_claim(left_manifestation_id, right_manifestation_id, recorded_at);
CREATE INDEX IF NOT EXISTS correspondence_valid_range_gist_idx ON wk.correspondence_claim USING gist(valid_range);
CREATE INDEX IF NOT EXISTS action_semantic_idx ON wk.action_attempt(semantic_action_namespace, semantic_action_type, configuration_block_id);
CREATE INDEX IF NOT EXISTS episode_block_arm_idx ON wk.transition_episode(configuration_block_id, arm, recorded_at);
CREATE INDEX IF NOT EXISTS episode_scope_gin_idx ON wk.transition_episode USING gin(public_environment_scope);

CREATE OR REPLACE FUNCTION wk.validate_action_attempt()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  locked_hash character(64);
BEGIN
  SELECT evaluation_spec_hash INTO STRICT locked_hash
  FROM wk.evaluation_spec
  WHERE evaluation_spec_version = NEW.evaluation_spec_version;
  IF NEW.evaluation_spec_hash <> locked_hash THEN
    RAISE EXCEPTION 'ActionAttempt evaluation spec hash does not match frozen spec' USING ERRCODE = '23514';
  END IF;
  NEW.declared_at := clock_timestamp();
  NEW.recorded_at := NEW.declared_at;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS action_attempt_validate_insert ON wk.action_attempt;
CREATE TRIGGER action_attempt_validate_insert
BEFORE INSERT ON wk.action_attempt
FOR EACH ROW EXECUTE FUNCTION wk.validate_action_attempt();

CREATE OR REPLACE FUNCTION wk.validate_claim_lineage()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF NEW.authority_class IN ('material','provider') OR NEW.production_method IN ('provider_reported','observed') THEN
    IF NOT EXISTS (
      SELECT 1
      FROM wk.observation o
      JOIN wk.observation_evidence oe ON oe.observation_id = o.observation_id
      WHERE o.observation_id = NEW.primary_observation_id
        AND o.target_manifestation_id = NEW.subject_manifestation_id
        AND oe.evidence_id = NEW.primary_evidence_id
    ) THEN
      RAISE EXCEPTION 'material/provider Claim lacks subject-matched Observation/Evidence lineage' USING ERRCODE = '23514';
    END IF;
  END IF;
  NEW.recorded_at := clock_timestamp();
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS claim_validate_lineage_insert ON wk.claim;
CREATE TRIGGER claim_validate_lineage_insert
BEFORE INSERT ON wk.claim
FOR EACH ROW EXECUTE FUNCTION wk.validate_claim_lineage();

CREATE OR REPLACE FUNCTION wk.validate_prediction()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  a wk.action_attempt%ROWTYPE;
  expected_count integer;
  supplied_count integer;
  invalid_count integer;
BEGIN
  SELECT * INTO STRICT a FROM wk.action_attempt WHERE action_id = NEW.action_id;
  IF NEW.evaluation_spec_version <> a.evaluation_spec_version OR
     NEW.evaluation_spec_hash <> a.evaluation_spec_hash THEN
    RAISE EXCEPTION 'prediction evaluation spec differs from action attempt' USING ERRCODE = '23514';
  END IF;

  SELECT count(*) INTO expected_count
  FROM wk.evaluation_proposition
  WHERE evaluation_spec_version = NEW.evaluation_spec_version
    AND semantic_action_namespace = a.semantic_action_namespace
    AND semantic_action_type = a.semantic_action_type;

  SELECT count(*) INTO supplied_count FROM jsonb_object_keys(NEW.outcome_probabilities);

  SELECT count(*) INTO invalid_count
  FROM jsonb_each(NEW.outcome_probabilities) p
  WHERE p.key NOT IN (
      SELECT proposition_key
      FROM wk.evaluation_proposition
      WHERE evaluation_spec_version = NEW.evaluation_spec_version
        AND semantic_action_namespace = a.semantic_action_namespace
        AND semantic_action_type = a.semantic_action_type
    )
    OR jsonb_typeof(p.value) <> 'number'
    OR (p.value::text)::double precision < 0.0
    OR (p.value::text)::double precision > 1.0;

  IF expected_count = 0 OR supplied_count <> expected_count OR invalid_count <> 0 THEN
    RAISE EXCEPTION 'prediction proposition vector is incomplete or invalid (expected %, supplied %, invalid %)',
      expected_count, supplied_count, invalid_count USING ERRCODE = '23514';
  END IF;

  IF EXISTS (
    SELECT 1
    FROM wk.evaluation_proposition ep
    WHERE ep.evaluation_spec_version = NEW.evaluation_spec_version
      AND ep.semantic_action_namespace = a.semantic_action_namespace
      AND ep.semantic_action_type = a.semantic_action_type
      AND NOT (NEW.outcome_probabilities ? ep.proposition_key)
  ) THEN
    RAISE EXCEPTION 'prediction proposition vector omits a locked proposition' USING ERRCODE = '23514';
  END IF;

  NEW.created_at := clock_timestamp();
  NEW.recorded_at := NEW.created_at;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS prediction_validate_insert ON wk.prediction;
CREATE TRIGGER prediction_validate_insert
BEFORE INSERT ON wk.prediction
FOR EACH ROW EXECUTE FUNCTION wk.validate_prediction();

CREATE OR REPLACE FUNCTION wk.guard_action_phase()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  dispatched_at timestamptz;
BEGIN
  IF NEW.phase = 'dispatched' AND current_setting('wk.dispatch_seal', true) IS DISTINCT FROM 'on' THEN
    RAISE EXCEPTION 'dispatch may only be inserted through wk.seal_dispatch' USING ERRCODE = '55000';
  END IF;

  SELECT recorded_at INTO dispatched_at
  FROM wk.action_phase
  WHERE action_id = NEW.action_id AND phase = 'dispatched';

  IF NEW.phase NOT IN ('dispatched','interrupted') AND dispatched_at IS NULL THEN
    RAISE EXCEPTION 'action phase % cannot precede dispatch', NEW.phase USING ERRCODE = '23514';
  END IF;

  NEW.occurred_at := clock_timestamp();
  NEW.recorded_at := NEW.occurred_at;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS action_phase_guard_insert ON wk.action_phase;
CREATE TRIGGER action_phase_guard_insert
BEFORE INSERT ON wk.action_phase
FOR EACH ROW EXECUTE FUNCTION wk.guard_action_phase();

CREATE OR REPLACE FUNCTION wk.seal_dispatch(
  p_action_id uuid,
  p_parameters_hash character(64),
  p_payload jsonb DEFAULT '{}'::jsonb
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
  a wk.action_attempt%ROWTYPE;
  p wk.prediction%ROWTYPE;
  phase_id uuid := gen_random_uuid();
  now_at timestamptz := clock_timestamp();
  current_tx bigint := ((pg_current_xact_id())::text::bigint);
BEGIN
  SELECT * INTO STRICT a FROM wk.action_attempt WHERE action_id = p_action_id FOR SHARE;
  IF a.parameters_hash <> p_parameters_hash THEN
    RAISE EXCEPTION 'execution parameters differ from declared ActionAttempt' USING ERRCODE = '23514';
  END IF;

  SELECT * INTO STRICT p FROM wk.prediction WHERE action_id = p_action_id FOR SHARE;
  IF p.created_txid = current_tx THEN
    RAISE EXCEPTION 'prediction must commit in an earlier database transaction before dispatch' USING ERRCODE = '55000';
  END IF;
  IF p.recorded_at >= now_at THEN
    RAISE EXCEPTION 'prediction is not earlier than dispatch' USING ERRCODE = '55000';
  END IF;
  IF p.evaluation_spec_hash <> a.evaluation_spec_hash THEN
    RAISE EXCEPTION 'evaluation spec hash changed before dispatch' USING ERRCODE = '55000';
  END IF;
  IF EXISTS (SELECT 1 FROM wk.action_phase WHERE action_id = p_action_id AND phase = 'dispatched') THEN
    RAISE EXCEPTION 'ActionAttempt already dispatched' USING ERRCODE = '23505';
  END IF;

  PERFORM set_config('wk.dispatch_seal', 'on', true);
  INSERT INTO wk.action_phase(action_phase_id, action_id, phase, occurred_at, payload, recorded_at)
  VALUES (phase_id, p_action_id, 'dispatched', now_at, p_payload, now_at);
  RETURN phase_id;
END;
$$;

CREATE OR REPLACE FUNCTION wk.validate_outcome()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  a wk.action_attempt%ROWTYPE;
  expected_count integer;
  supplied_count integer;
  invalid_count integer;
BEGIN
  SELECT * INTO STRICT a FROM wk.action_attempt WHERE action_id = NEW.action_id;
  SELECT count(*) INTO expected_count
  FROM wk.evaluation_proposition
  WHERE evaluation_spec_version = a.evaluation_spec_version
    AND semantic_action_namespace = a.semantic_action_namespace
    AND semantic_action_type = a.semantic_action_type;
  SELECT count(*) INTO supplied_count FROM jsonb_object_keys(NEW.actual_propositions);
  SELECT count(*) INTO invalid_count
  FROM jsonb_each(NEW.actual_propositions) p
  WHERE p.key NOT IN (
      SELECT proposition_key FROM wk.evaluation_proposition
      WHERE evaluation_spec_version = a.evaluation_spec_version
        AND semantic_action_namespace = a.semantic_action_namespace
        AND semantic_action_type = a.semantic_action_type
    )
    OR (
      NEW.resolution_status IN ('verified','partial','failed') AND
      (jsonb_typeof(p.value) <> 'boolean')
    )
    OR (
      NEW.resolution_status IN ('unknown','censored') AND
      jsonb_typeof(p.value) NOT IN ('boolean','null')
    );
  IF supplied_count <> expected_count OR invalid_count <> 0 THEN
    RAISE EXCEPTION 'outcome proposition vector is incomplete or invalid' USING ERRCODE = '23514';
  END IF;
  IF EXISTS (
    SELECT 1 FROM wk.evaluation_proposition ep
    WHERE ep.evaluation_spec_version = a.evaluation_spec_version
      AND ep.semantic_action_namespace = a.semantic_action_namespace
      AND ep.semantic_action_type = a.semantic_action_type
      AND NOT (NEW.actual_propositions ? ep.proposition_key)
  ) THEN
    RAISE EXCEPTION 'outcome proposition vector omits a locked proposition' USING ERRCODE = '23514';
  END IF;
  NEW.recorded_at := clock_timestamp();
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS outcome_validate_insert ON wk.outcome;
CREATE TRIGGER outcome_validate_insert
BEFORE INSERT ON wk.outcome
FOR EACH ROW EXECUTE FUNCTION wk.validate_outcome();

CREATE OR REPLACE FUNCTION wk.validate_episode_close()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  a wk.action_attempt%ROWTYPE;
  p wk.prediction%ROWTYPE;
  dispatch_at timestamptz;
BEGIN
  SELECT * INTO STRICT a FROM wk.action_attempt WHERE action_id = NEW.action_id;
  SELECT * INTO STRICT p FROM wk.prediction WHERE prediction_id = NEW.prediction_id;
  IF p.action_id <> a.action_id OR NEW.trial_id <> a.trial_id OR
     NEW.configuration_block_id <> a.configuration_block_id OR NEW.arm <> a.arm THEN
    RAISE EXCEPTION 'episode lineage does not match action and prediction' USING ERRCODE = '23514';
  END IF;
  SELECT recorded_at INTO dispatch_at FROM wk.action_phase
    WHERE action_id = a.action_id AND phase = 'dispatched';
  IF dispatch_at IS NULL THEN
    RAISE EXCEPTION 'cannot close episode without dispatch' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (
    SELECT 1
    FROM wk.prediction_evaluation pe
    JOIN wk.outcome o ON o.outcome_id = pe.outcome_id
    JOIN wk.outcome_observation oo ON oo.outcome_id = o.outcome_id
    JOIN wk.observation ob ON ob.observation_id = oo.observation_id
    JOIN wk.observation_evidence oe ON oe.observation_id = ob.observation_id
    JOIN wk.evidence e ON e.evidence_id = oe.evidence_id
    WHERE pe.prediction_id = p.prediction_id
      AND o.action_id = a.action_id
      AND ob.recorded_at > dispatch_at
      AND e.captured_at > dispatch_at
  ) THEN
    RAISE EXCEPTION 'cannot close episode without fresh post-dispatch provider evidence, outcome, and evaluation' USING ERRCODE = '23514';
  END IF;
  IF NEW.closed_at <= dispatch_at THEN
    RAISE EXCEPTION 'episode closure must be later than dispatch' USING ERRCODE = '23514';
  END IF;
  NEW.recorded_at := clock_timestamp();
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS transition_episode_validate_insert ON wk.transition_episode;
CREATE TRIGGER transition_episode_validate_insert
BEFORE INSERT ON wk.transition_episode
FOR EACH ROW EXECUTE FUNCTION wk.validate_episode_close();

CREATE OR REPLACE FUNCTION wk.validate_episode_links_deferred()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM wk.episode_pre_observation WHERE episode_id = NEW.episode_id) THEN
    RAISE EXCEPTION 'closed episode lacks pre-action Observation links' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM wk.episode_correspondence WHERE episode_id = NEW.episode_id) THEN
    RAISE EXCEPTION 'closed episode lacks correspondence basis' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM wk.episode_post_observation WHERE episode_id = NEW.episode_id) THEN
    RAISE EXCEPTION 'closed episode lacks post-action Observation links' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM wk.episode_outcome eo
    JOIN wk.outcome o ON o.outcome_id = eo.outcome_id
    WHERE eo.episode_id = NEW.episode_id AND o.action_id = NEW.action_id
  ) THEN
    RAISE EXCEPTION 'closed episode lacks action-matched Outcome link' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM wk.episode_evaluation ee
    JOIN wk.prediction_evaluation pe ON pe.evaluation_id = ee.evaluation_id
    WHERE ee.episode_id = NEW.episode_id AND pe.prediction_id = NEW.prediction_id
  ) THEN
    RAISE EXCEPTION 'closed episode lacks prediction-matched Evaluation link' USING ERRCODE = '23514';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM wk.action_phase WHERE action_id = NEW.action_id AND phase = 'evaluated') THEN
    RAISE EXCEPTION 'closed episode lacks evaluated lifecycle phase' USING ERRCODE = '23514';
  END IF;
  RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS transition_episode_links_complete ON wk.transition_episode;
CREATE CONSTRAINT TRIGGER transition_episode_links_complete
AFTER INSERT ON wk.transition_episode
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION wk.validate_episode_links_deferred();

CREATE OR REPLACE FUNCTION wk.validate_hard_correspondence_deferred()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  required_count integer;
  lineage_count integer;
BEGIN
  IF NEW.strength <> 'hard' THEN
    RETURN NULL;
  END IF;
  SELECT count(DISTINCT c.predicate) INTO required_count
  FROM wk.correspondence_claim_basis cb
  JOIN wk.claim c ON c.claim_id = cb.claim_id
  WHERE cb.correspondence_id = NEW.correspondence_id
    AND c.predicate IN ('configured_remote_url','hosted_provider_native_id','shared_exact_commit');
  SELECT count(DISTINCT c.primary_evidence_id) INTO lineage_count
  FROM wk.correspondence_claim_basis cb
  JOIN wk.claim c ON c.claim_id = cb.claim_id
  WHERE cb.correspondence_id = NEW.correspondence_id
    AND c.predicate IN ('configured_remote_url','hosted_provider_native_id','shared_exact_commit')
    AND c.primary_observation_id IS NOT NULL
    AND c.primary_evidence_id IS NOT NULL;
  IF required_count <> 3 OR lineage_count < 2 THEN
    RAISE EXCEPTION 'hard git:working_copy_of requires remote, provider-native identity, and exact shared-history evidence without one-source inflation'
      USING ERRCODE = '23514';
  END IF;
  RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS correspondence_hard_basis_complete ON wk.correspondence_claim;
CREATE CONSTRAINT TRIGGER correspondence_hard_basis_complete
AFTER INSERT ON wk.correspondence_claim
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION wk.validate_hard_correspondence_deferred();

CREATE OR REPLACE FUNCTION wk.claims_as_of(p_valid_at timestamptz, p_known_at timestamptz)
RETURNS SETOF wk.claim
LANGUAGE sql
STABLE
AS $$
  SELECT c.*
  FROM wk.claim c
  LEFT JOIN LATERAL (
    SELECT d.relation
    FROM wk.claim_disposition d
    WHERE d.target_claim_id = c.claim_id
      AND d.recorded_at <= p_known_at
      AND d.effective_valid_at <= p_valid_at
    ORDER BY d.recorded_at DESC, d.claim_disposition_id DESC
    LIMIT 1
  ) latest ON true
  WHERE c.recorded_at <= p_known_at
    AND c.valid_range @> p_valid_at
    AND (latest.relation IS NULL OR latest.relation = 'supports');
$$;

CREATE OR REPLACE FUNCTION wk.correspondences_as_of(p_valid_at timestamptz, p_known_at timestamptz)
RETURNS SETOF wk.correspondence_claim
LANGUAGE sql
STABLE
AS $$
  SELECT c.*
  FROM wk.correspondence_claim c
  LEFT JOIN LATERAL (
    SELECT d.relation
    FROM wk.correspondence_disposition d
    WHERE d.target_correspondence_id = c.correspondence_id
      AND d.recorded_at <= p_known_at
      AND d.effective_valid_at <= p_valid_at
    ORDER BY d.recorded_at DESC, d.correspondence_disposition_id DESC
    LIMIT 1
  ) latest ON true
  WHERE c.recorded_at <= p_known_at
    AND c.valid_range @> p_valid_at
    AND (latest.relation IS NULL OR latest.relation = 'supports');
$$;

CREATE OR REPLACE VIEW wk.current_belief AS
SELECT * FROM wk.claims_as_of(clock_timestamp(), clock_timestamp());

DO $$
DECLARE
  t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'manifestation','locator','evidence','observation','observation_evidence',
    'claim','claim_observation','claim_evidence','claim_derivation','claim_disposition',
    'correspondence_claim','correspondence_observation','correspondence_evidence',
    'correspondence_claim_basis','correspondence_disposition','evaluation_spec',
    'evaluation_proposition','action_attempt','action_precondition_claim',
    'action_precondition_observation','action_target','prediction','prediction_assumed_claim',
    'prediction_basis_evidence','action_phase','outcome','outcome_observation','outcome_evidence',
    'prediction_evaluation','transition_episode','episode_correspondence','episode_pre_observation',
    'episode_pre_claim','episode_post_observation','episode_outcome','episode_evaluation',
    'prediction_basis_episode','package_artifact'
  ] LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS %I_append_only ON wk.%I', t, t);
    EXECUTE format(
      'CREATE TRIGGER %I_append_only BEFORE UPDATE OR DELETE ON wk.%I FOR EACH ROW EXECUTE FUNCTION wk.deny_history_mutation()',
      t, t
    );
  END LOOP;
END;
$$;

DO $$
DECLARE
  t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'manifestation','locator','evidence','observation','observation_evidence',
    'claim','claim_observation','claim_evidence','claim_derivation','claim_disposition',
    'correspondence_claim','correspondence_observation','correspondence_evidence',
    'correspondence_claim_basis','correspondence_disposition','evaluation_spec',
    'evaluation_proposition','action_attempt','action_precondition_claim',
    'action_precondition_observation','action_target','prediction_assumed_claim',
    'prediction_basis_evidence','outcome_observation','outcome_evidence','prediction_evaluation',
    'episode_correspondence','episode_pre_observation','episode_pre_claim','episode_post_observation',
    'episode_outcome','episode_evaluation','prediction_basis_episode','package_artifact'
  ] LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS %I_record_time ON wk.%I', t, t);
    EXECUTE format(
      'CREATE TRIGGER %I_record_time BEFORE INSERT ON wk.%I FOR EACH ROW EXECUTE FUNCTION wk.force_recorded_at()',
      t, t
    );
  END LOOP;
END;
$$;

COMMIT;
