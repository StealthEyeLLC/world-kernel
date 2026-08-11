BEGIN;

REVOKE ALL ON SCHEMA wk FROM PUBLIC;
GRANT USAGE ON SCHEMA wk TO wk_operator;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA wk TO wk_operator;
GRANT EXECUTE ON FUNCTION wk.seal_dispatch(uuid, character, jsonb) TO wk_operator;
GRANT EXECUTE ON FUNCTION wk.claims_as_of(timestamptz, timestamptz) TO wk_operator;
GRANT EXECUTE ON FUNCTION wk.correspondences_as_of(timestamptz, timestamptz) TO wk_operator;
ALTER DEFAULT PRIVILEGES IN SCHEMA wk GRANT SELECT, INSERT ON TABLES TO wk_operator;

COMMIT;

