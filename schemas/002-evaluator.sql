BEGIN;

CREATE SCHEMA IF NOT EXISTS eval001;

CREATE OR REPLACE FUNCTION eval001.force_recorded_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.recorded_at := clock_timestamp();
  RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION eval001.deny_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'Build 001 evaluator history is append-only: %.% cannot be changed by %',
    TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP USING ERRCODE = '55000';
END;
$$;

CREATE TABLE IF NOT EXISTS eval001.preregistration_contract (
  contract_version text PRIMARY KEY,
  machine_preregistration_sha256 character(64) NOT NULL,
  human_spec_sha256 character(64) NOT NULL,
  evaluation_spec_sha256 character(64) NOT NULL,
  scorer_version text NOT NULL,
  serializer_versions jsonb NOT NULL CHECK (jsonb_typeof(serializer_versions) = 'object'),
  contract_blob_ref text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS eval001.boundary_event (
  boundary_event_id uuid PRIMARY KEY,
  event_type text NOT NULL CHECK (event_type IN (
    'original_frozen','pilot_started','pilot_closed','amendment_frozen',
    'confirmatory_started','confirmatory_closed','drift_started','drift_closed'
  )),
  contract_version text NOT NULL REFERENCES eval001.preregistration_contract(contract_version),
  evidence_hash character(64) NOT NULL CHECK (evidence_hash ~ '^[0-9a-f]{64}$'),
  details jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
  occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE UNIQUE INDEX IF NOT EXISTS one_confirmatory_start
  ON eval001.boundary_event(event_type) WHERE event_type = 'confirmatory_started';

CREATE TABLE IF NOT EXISTS eval001.seed_commitment (
  seed_id text PRIMARY KEY,
  phase text NOT NULL CHECK (phase IN ('acquisition','pilot','confirmatory','drift','hostile')),
  configuration_block_id text NOT NULL,
  commitment_sha256 character(64) NOT NULL CHECK (commitment_sha256 ~ '^[0-9a-f]{64}$'),
  sealed_payload_ref text NOT NULL,
  public_fixture_revision text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (phase, configuration_block_id)
);

CREATE TABLE IF NOT EXISTS eval001.hidden_configuration (
  hidden_configuration_id uuid PRIMARY KEY,
  seed_id text NOT NULL REFERENCES eval001.seed_commitment(seed_id),
  regime_label text NOT NULL,
  configuration jsonb NOT NULL CHECK (jsonb_typeof(configuration) = 'object'),
  expected_reset_fingerprint character(64) NOT NULL CHECK (expected_reset_fingerprint ~ '^[0-9a-f]{64}$'),
  answer_key_version text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (seed_id)
);

CREATE TABLE IF NOT EXISTS eval001.arm_randomization (
  configuration_block_id text PRIMARY KEY,
  seed_id text NOT NULL REFERENCES eval001.seed_commitment(seed_id),
  arm_order text[] NOT NULL,
  randomizer_version text NOT NULL,
  randomization_proof character(64) NOT NULL CHECK (randomization_proof ~ '^[0-9a-f]{64}$'),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK (array_length(arm_order, 1) = 3),
  CHECK (arm_order @> ARRAY['cold','memory','structured']::text[])
);

CREATE TABLE IF NOT EXISTS eval001.reset_verification (
  reset_verification_id uuid PRIMARY KEY,
  seed_id text NOT NULL REFERENCES eval001.seed_commitment(seed_id),
  arm text NOT NULL CHECK (arm IN ('cold','memory','structured','acquisition','pilot','drift','hostile')),
  generation_id uuid NOT NULL,
  actual_fingerprint character(64) NOT NULL CHECK (actual_fingerprint ~ '^[0-9a-f]{64}$'),
  expected_fingerprint character(64) NOT NULL CHECK (expected_fingerprint ~ '^[0-9a-f]{64}$'),
  provider_evidence_hashes jsonb NOT NULL CHECK (jsonb_typeof(provider_evidence_hashes) = 'array'),
  passed boolean NOT NULL,
  verified_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (seed_id, arm, generation_id)
);

CREATE TABLE IF NOT EXISTS eval001.invocation_attestation (
  invocation_id uuid PRIMARY KEY,
  configuration_block_id text NOT NULL,
  arm text NOT NULL CHECK (arm IN ('cold','memory','structured','pilot','drift')),
  isolated_session_id text NOT NULL,
  isolation_mechanism text NOT NULL,
  memory_state text NOT NULL CHECK (memory_state IN ('disabled','not_available','unknown')),
  model_identifier text NOT NULL,
  model_configuration_hash character(64) NOT NULL CHECK (model_configuration_hash ~ '^[0-9a-f]{64}$'),
  common_instructions_hash character(64) NOT NULL CHECK (common_instructions_hash ~ '^[0-9a-f]{64}$'),
  inherited_package_hash character(64),
  inherited_tokens integer NOT NULL CHECK (inherited_tokens >= 0),
  started_at timestamptz NOT NULL,
  completed_at timestamptz,
  attestation_evidence_ref text NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (configuration_block_id, arm, isolated_session_id)
);

CREATE TABLE IF NOT EXISTS eval001.ground_truth (
  ground_truth_id uuid PRIMARY KEY,
  action_id uuid NOT NULL,
  configuration_block_id text NOT NULL,
  horizon_id text NOT NULL,
  actual_propositions jsonb NOT NULL CHECK (jsonb_typeof(actual_propositions) = 'object'),
  actual_deltas jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(actual_deltas) = 'object'),
  actual_invariants jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(actual_invariants) = 'object'),
  provider_evidence_hashes jsonb NOT NULL CHECK (jsonb_typeof(provider_evidence_hashes) = 'array'),
  resolver_version text NOT NULL,
  resolved_at timestamptz NOT NULL,
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (action_id, horizon_id)
);

CREATE TABLE IF NOT EXISTS eval001.arm_isolation_probe (
  probe_id uuid PRIMARY KEY,
  configuration_block_id text NOT NULL,
  arm text NOT NULL,
  probe_type text NOT NULL CHECK (probe_type IN (
    'cold_history_absent','memory_kernel_denied','treatment_evaluator_denied',
    'cross_arm_path_denied','hidden_label_absent','package_lineage_parity'
  )),
  passed boolean NOT NULL,
  evidence_hash character(64) NOT NULL CHECK (evidence_hash ~ '^[0-9a-f]{64}$'),
  details jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS eval001.aggregate_result (
  result_id uuid PRIMARY KEY,
  analysis_version text NOT NULL,
  input_manifest_hash character(64) NOT NULL CHECK (input_manifest_hash ~ '^[0-9a-f]{64}$'),
  statistics jsonb NOT NULL CHECK (jsonb_typeof(statistics) = 'object'),
  generated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (analysis_version, input_manifest_hash)
);

DO $$
DECLARE
  t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'preregistration_contract','boundary_event','seed_commitment','hidden_configuration',
    'arm_randomization','reset_verification','invocation_attestation','ground_truth',
    'arm_isolation_probe','aggregate_result'
  ] LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS %I_append_only ON eval001.%I', t, t);
    EXECUTE format(
      'CREATE TRIGGER %I_append_only BEFORE UPDATE OR DELETE ON eval001.%I FOR EACH ROW EXECUTE FUNCTION eval001.deny_mutation()',
      t, t
    );
    EXECUTE format('DROP TRIGGER IF EXISTS %I_record_time ON eval001.%I', t, t);
    EXECUTE format(
      'CREATE TRIGGER %I_record_time BEFORE INSERT ON eval001.%I FOR EACH ROW EXECUTE FUNCTION eval001.force_recorded_at()',
      t, t
    );
  END LOOP;
END;
$$;

COMMIT;
