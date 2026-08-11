using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class DatabaseTests
{
    public static async Task SchemaAndTemporalAsync(string secretFile)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var forbidden = new[]
        {
            "entity", "state", "event", "rule", "hypothesis", "capability", "plan", "experiment", "skill",
            "embedding", "universal_graph_node", "universal_graph_edge", "audit_entry", "approval", "risk_class"
        };
        var forbiddenCount = await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM information_schema.tables WHERE table_schema='wk' AND table_name = ANY(@names);",
                connection);
            command.Parameters.AddWithValue("names", forbidden);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        }).ConfigureAwait(false);
        AssertEx.Equal(0, forbiddenCount, "Forbidden universal/kernel-expansion tables must not exist.");

        var manifestation = Manifestation("codeeye/git-local", "git-working-copy", "local-" + Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(manifestation).ConfigureAwait(false);
        var evidence = FakeEvidence("codeeye/git-local", DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await database.InsertEvidenceAsync(evidence).ConfigureAwait(false);
        var observation = Observation(manifestation.ManifestationId, evidence.EvidenceId, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await database.InsertObservationAsync(observation).ConfigureAwait(false);

        var claimOne = Guid.NewGuid();
        await InsertMaterialClaimAsync(database, claimOne, manifestation.ManifestationId, observation.ObservationId, evidence.EvidenceId,
            "git", "branch_name", JsonSerializer.SerializeToElement("old"), DateTimeOffset.Parse("2026-01-01T00:00:00Z")).ConfigureAwait(false);
        var claimOneKnownAt = await ScalarAsync<DateTimeOffset>(database, "SELECT recorded_at FROM wk.claim WHERE claim_id=@id;", ("id", claimOne)).ConfigureAwait(false);
        await Task.Delay(15).ConfigureAwait(false);
        var claimTwo = Guid.NewGuid();
        await InsertMaterialClaimAsync(database, claimTwo, manifestation.ManifestationId, observation.ObservationId, evidence.EvidenceId,
            "git", "branch_name", JsonSerializer.SerializeToElement("new"), DateTimeOffset.Parse("2026-01-01T00:00:00Z")).ConfigureAwait(false);
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.claim_disposition(
                  claim_disposition_id,target_claim_id,relation,effective_valid_at,basis,producer,replacement_claim_id,rationale_code
                ) VALUES (@id,@target,'supersedes',@valid,'{}'::jsonb,'{"kind":"test"}'::jsonb,@replacement,'historical_correction');
                """, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("target", claimOne);
            command.Parameters.AddWithValue("valid", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
            command.Parameters.AddWithValue("replacement", claimTwo);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var validAt = DateTimeOffset.Parse("2026-01-10T00:00:00Z");
        var earlyIds = await ClaimIdsAsOfAsync(database, manifestation.ManifestationId, validAt, claimOneKnownAt).ConfigureAwait(false);
        AssertEx.True(earlyIds.SequenceEqual([claimOne]), "known_as_of before correction must reconstruct the earlier belief.");
        var currentIds = await ClaimIdsAsOfAsync(database, manifestation.ManifestationId, validAt, DateTimeOffset.UtcNow.AddSeconds(1)).ConfigureAwait(false);
        AssertEx.False(currentIds.Contains(claimOne), "Superseded Claim must leave the derived current belief without rewriting history.");
        AssertEx.True(currentIds.Contains(claimTwo));

        var pathToken = Guid.NewGuid().ToString("N");
        var originalPath = $"X:\\Fixture\\{pathToken}\\repo";
        var renamedPath = $"X:\\Fixture\\{pathToken}\\renamed";
        var locatorOne = Guid.NewGuid();
        await InsertLocatorAsync(database, locatorOne, manifestation.ManifestationId, observation.ObservationId,
            originalPath, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-02-01T00:00:00Z")).ConfigureAwait(false);
        var renamedLocator = Guid.NewGuid();
        await InsertLocatorAsync(database, renamedLocator, manifestation.ManifestationId, observation.ObservationId,
            renamedPath, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), null, locatorOne).ConfigureAwait(false);
        var reusedManifestation = Manifestation("codeeye/git-local", "git-working-copy", "recreated-" + Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(reusedManifestation).ConfigureAwait(false);
        var reusedEvidence = FakeEvidence("codeeye/git-local", DateTimeOffset.UtcNow);
        await database.InsertEvidenceAsync(reusedEvidence).ConfigureAwait(false);
        var reusedObservation = Observation(reusedManifestation.ManifestationId, reusedEvidence.EvidenceId, DateTimeOffset.UtcNow);
        await database.InsertObservationAsync(reusedObservation).ConfigureAwait(false);
        await InsertLocatorAsync(database, Guid.NewGuid(), reusedManifestation.ManifestationId, reusedObservation.ObservationId,
            originalPath, DateTimeOffset.Parse("2026-03-01T00:00:00Z"), null).ConfigureAwait(false);
        var manifestationCountAtReusedPath = await ScalarAsync<long>(database, "SELECT count(DISTINCT manifestation_id) FROM wk.locator WHERE locator_value=@path;", ("path", originalPath)).ConfigureAwait(false);
        AssertEx.Equal(2L, manifestationCountAtReusedPath, "Path reuse must not merge manifestations.");

        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.WithConnectionAsync(async (connection, cancellationToken) =>
            {
                await using var update = new NpgsqlCommand("UPDATE wk.manifestation SET display_label='mutated' WHERE manifestation_id=@id;", connection);
                update.Parameters.AddWithValue("id", manifestation.ManifestationId);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }),
            exception => exception.SqlState == "55000").ConfigureAwait(false);

        var insertedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var backdated = FakeEvidence("hostile/backdating", DateTimeOffset.Parse("1999-01-01T00:00:00Z"));
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.evidence(
                  evidence_id,provider_namespace,observer_name,captured_at,hash_algorithm,content_hash,blob_ref,
                  media_type,acquisition_method,byte_length,metadata,recorded_at
                ) VALUES (@id,'hostile/backdating','attacker',@captured,'sha256',@hash,@blob,'text/plain','hostile',1,'{}'::jsonb,'1999-01-01');
                """, connection);
            command.Parameters.AddWithValue("id", backdated.EvidenceId);
            command.Parameters.AddWithValue("captured", backdated.CapturedAt);
            command.Parameters.AddWithValue("hash", backdated.ContentHash);
            command.Parameters.AddWithValue("blob", backdated.BlobRef);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        var forcedRecordTime = await ScalarAsync<DateTimeOffset>(database, "SELECT recorded_at FROM wk.evidence WHERE evidence_id=@id;", ("id", backdated.EvidenceId)).ConfigureAwait(false);
        AssertEx.True(forcedRecordTime >= insertedAt, "Database record time must defeat client backdating.");
    }

    public static async Task ActionLifecycleAsync(string secretFile, string artifactDirectory)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var store = new EvidenceStore(Path.Combine(artifactDirectory, "lifecycle-evidence"));
        var local = Manifestation("codeeye/git-local", "git-working-copy", "action-local-" + Guid.NewGuid().ToString("N"));
        var remote = Manifestation("github/provider", "github-repository", "action-remote-" + Guid.NewGuid().ToString("N"), "1330898503");
        await database.InsertManifestationAsync(local).ConfigureAwait(false);
        await database.InsertManifestationAsync(remote).ConfigureAwait(false);
        var preEvidence = await store.PutAsync(Encoding.UTF8.GetBytes("pre-state"), "codeeye/git-local", "CODEeye", "application/json", "program-host", DateTimeOffset.UtcNow).ConfigureAwait(false);
        await database.InsertEvidenceAsync(preEvidence).ConfigureAwait(false);
        var preObservation = Observation(local.ManifestationId, preEvidence.EvidenceId, DateTimeOffset.UtcNow);
        await database.InsertObservationAsync(preObservation).ConfigureAwait(false);
        var correspondenceId = await InsertCandidateCorrespondenceAsync(database, local.ManifestationId, remote.ManifestationId).ConfigureAwait(false);

        await SameTransactionPredictionRejectedAsync(database, local.ManifestationId).ConfigureAwait(false);

        var incompleteAction = await DeclareActionAsync(database, local.ManifestationId, "git:create_branch", "hostile-incomplete").ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertPredictionRawAsync(database, incompleteAction.ActionId, new Dictionary<string, double> { ["provider_accepts_action"] = 0.8 }),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var declaration = await DeclareActionAsync(database, local.ManifestationId, "git:create_branch", "valid").ConfigureAwait(false);
        var prediction = Prediction(declaration, "git:create_branch");
        await database.CommitPredictionAsync(prediction).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.SealDispatchAsync(declaration.ActionId, JsonSerializer.SerializeToElement(new { branch = "wk-b001-changed" }), JsonDefaults.EmptyObject),
            exception => exception.SqlState == "23514").ConfigureAwait(false);
        await database.SealDispatchAsync(declaration.ActionId, declaration.Parameters, JsonSerializer.SerializeToElement(new { dispatch_sealed = true })).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.SealDispatchAsync(declaration.ActionId, declaration.Parameters, JsonDefaults.EmptyObject),
            exception => exception.SqlState == "23505").ConfigureAwait(false);

        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.WithConnectionAsync(async (connection, cancellationToken) =>
            {
                await using var command = new NpgsqlCommand("UPDATE wk.prediction SET mechanism='retrofit' WHERE prediction_id=@id;", connection);
                command.Parameters.AddWithValue("id", prediction.PredictionId);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }),
            exception => exception.SqlState == "55000").ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.WithConnectionAsync(async (connection, cancellationToken) =>
            {
                await using var command = new NpgsqlCommand("UPDATE wk.evaluation_spec SET scorer_version='rescued' WHERE evaluation_spec_version=@version;", connection);
                command.Parameters.AddWithValue("version", Build001Contract.EvaluationSpecVersion);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }),
            exception => exception.SqlState == "55000").ConfigureAwait(false);

        var receipt = await store.PutAsync(Encoding.UTF8.GetBytes("provider receipt accepted"), "git/native", "git-facet", "application/json", "provider-return", DateTimeOffset.UtcNow).ConfigureAwait(false);
        await database.InsertEvidenceAsync(receipt).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(declaration.ActionId, "provider_acknowledged", JsonSerializer.SerializeToElement(new { exit_code = 0 }), receipt.EvidenceId).ConfigureAwait(false);
        await Task.Delay(25).ConfigureAwait(false);
        var postEvidence = await store.PutAsync(Encoding.UTF8.GetBytes("fresh post-state"), "codeeye/git-local", "CODEeye", "application/json", "program-host-reobservation", DateTimeOffset.UtcNow).ConfigureAwait(false);
        await database.InsertEvidenceAsync(postEvidence).ConfigureAwait(false);
        var postObservation = Observation(local.ManifestationId, postEvidence.EvidenceId, DateTimeOffset.UtcNow);
        await database.InsertObservationAsync(postObservation).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(declaration.ActionId, "post_observed", JsonDefaults.EmptyObject, postEvidence.EvidenceId).ConfigureAwait(false);

        var actual = Build001Contract.ForAction("git:create_branch").ToDictionary(key => key, _ => (bool?)true, StringComparer.Ordinal);
        actual["local_head_sha_changes"] = false;
        actual["remote_branch_exists_before_push"] = false;
        actual["worktree_content_changes"] = false;
        var score = PredictionScorer.Score("git:create_branch", prediction.Probabilities, actual, ["branch_created"], ["branch_created"], ["local_head_unchanged", "worktree_unchanged"], []);
        var outcomeId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        await InsertOutcomeAndEvaluationAsync(database, declaration.ActionId, prediction.PredictionId, outcomeId, evaluationId, actual, score, postObservation.ObservationId, postEvidence.EvidenceId).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(declaration.ActionId, "outcome_resolved", JsonDefaults.EmptyObject, postEvidence.EvidenceId).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(declaration.ActionId, "evaluated", JsonDefaults.EmptyObject).ConfigureAwait(false);

        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertEpisodeAsync(database, declaration, prediction.PredictionId, correspondenceId, preObservation.ObservationId, postObservation.ObservationId, outcomeId, evaluationId, omitLinks: true),
            exception => exception.SqlState == "23514").ConfigureAwait(false);
        await InsertEpisodeAsync(database, declaration, prediction.PredictionId, correspondenceId, preObservation.ObservationId, postObservation.ObservationId, outcomeId, evaluationId, omitLinks: false).ConfigureAwait(false);
        var episodeCount = await ScalarAsync<long>(database, "SELECT count(*) FROM wk.transition_episode WHERE action_id=@id;", ("id", declaration.ActionId)).ConfigureAwait(false);
        AssertEx.Equal(1L, episodeCount);

        var receiptOnly = await DeclareActionAsync(database, local.ManifestationId, "git:create_branch", "receipt-only").ConfigureAwait(false);
        var receiptOnlyPrediction = Prediction(receiptOnly, "git:create_branch");
        await database.CommitPredictionAsync(receiptOnlyPrediction).ConfigureAwait(false);
        await database.SealDispatchAsync(receiptOnly.ActionId, receiptOnly.Parameters, JsonDefaults.EmptyObject).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(receiptOnly.ActionId, "provider_acknowledged", JsonSerializer.SerializeToElement(new { provider_says_success = true }), receipt.EvidenceId).ConfigureAwait(false);
        var receiptOutcomeId = Guid.NewGuid();
        var receiptEvaluationId = Guid.NewGuid();
        await InsertOutcomeAndEvaluationAsync(database, receiptOnly.ActionId, receiptOnlyPrediction.PredictionId, receiptOutcomeId, receiptEvaluationId, actual, score, null, null).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(receiptOnly.ActionId, "outcome_resolved", JsonDefaults.EmptyObject).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(receiptOnly.ActionId, "evaluated", JsonDefaults.EmptyObject).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertEpisodeAsync(database, receiptOnly, receiptOnlyPrediction.PredictionId, correspondenceId, preObservation.ObservationId, preObservation.ObservationId, receiptOutcomeId, receiptEvaluationId, omitLinks: false),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var replay = await DeclareActionAsync(database, local.ManifestationId, "git:create_branch", "replay").ConfigureAwait(false);
        var replayPrediction = Prediction(replay, "git:create_branch");
        await database.CommitPredictionAsync(replayPrediction).ConfigureAwait(false);
        await database.SealDispatchAsync(replay.ActionId, replay.Parameters, JsonDefaults.EmptyObject).ConfigureAwait(false);
        var replayObservation = Observation(local.ManifestationId, preEvidence.EvidenceId, DateTimeOffset.UtcNow) with { AcquisitionStatus = "stale" };
        await database.InsertObservationAsync(replayObservation).ConfigureAwait(false);
        var replayOutcome = Guid.NewGuid();
        var replayEvaluation = Guid.NewGuid();
        await InsertOutcomeAndEvaluationAsync(database, replay.ActionId, replayPrediction.PredictionId, replayOutcome, replayEvaluation, actual, score, replayObservation.ObservationId, preEvidence.EvidenceId).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(replay.ActionId, "post_observed", JsonDefaults.EmptyObject, preEvidence.EvidenceId).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(replay.ActionId, "outcome_resolved", JsonDefaults.EmptyObject).ConfigureAwait(false);
        await database.AppendActionPhaseAsync(replay.ActionId, "evaluated", JsonDefaults.EmptyObject).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertEpisodeAsync(database, replay, replayPrediction.PredictionId, correspondenceId, preObservation.ObservationId, replayObservation.ObservationId, replayOutcome, replayEvaluation, omitLinks: false),
            exception => exception.SqlState == "23514").ConfigureAwait(false);
    }

    public static async Task EpistemicHostilesAsync(string secretFile, string artifactDirectory)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var store = new EvidenceStore(Path.Combine(artifactDirectory, "epistemic-evidence"));
        var manifestation = Manifestation("codeeye/git-local", "git-working-copy", "epistemic-" + Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(manifestation).ConfigureAwait(false);
        var action = await DeclareActionAsync(database, manifestation.ManifestationId, "git:fetch_remote", "outage").ConfigureAwait(false);
        var prediction = Prediction(action, "git:fetch_remote");
        await database.CommitPredictionAsync(prediction).ConfigureAwait(false);
        await database.SealDispatchAsync(action.ActionId, action.Parameters, JsonDefaults.EmptyObject).ConfigureAwait(false);
        await Task.Delay(15).ConfigureAwait(false);
        var outageEvidence = await store.PutAsync(Encoding.UTF8.GetBytes("provider unavailable"), "codeeye/git-local", "CODEeye", "application/json", "provider-outage", DateTimeOffset.UtcNow).ConfigureAwait(false);
        await database.InsertEvidenceAsync(outageEvidence).ConfigureAwait(false);
        var outageObservation = Observation(manifestation.ManifestationId, outageEvidence.EvidenceId, DateTimeOffset.UtcNow) with { AcquisitionStatus = "outage" };
        await database.InsertObservationAsync(outageObservation).ConfigureAwait(false);
        var nullActual = Build001Contract.ForAction("git:fetch_remote").ToDictionary(key => key, _ => (bool?)null, StringComparer.Ordinal);
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.outcome(
                  outcome_id,action_id,horizon_id,resolution_status,actual_propositions,actual_deltas,
                  actual_invariants,attribution_status,resolver_version,resolved_at
                ) VALUES (@id,@action,'locked','unknown',@actual,'{}'::jsonb,'{}'::jsonb,'not_attributed','build001-evaluator-v1',clock_timestamp());
                """, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("action", action.ActionId);
            AddJson(command, "actual", JsonSerializer.SerializeToElement(nullActual, JsonDefaults.Options));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        var nonNullOutagePropositions = await ScalarAsync<long>(database,
            "SELECT count(*) FROM wk.outcome o, LATERAL jsonb_each(o.actual_propositions) p WHERE o.action_id=@id AND jsonb_typeof(p.value) <> 'null';",
            ("id", action.ActionId)).ConfigureAwait(false);
        AssertEx.Equal(0L, nonNullOutagePropositions, "Provider outage must yield unknown, not assumed unchanged.");

        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.WithConnectionAsync(async (connection, cancellationToken) =>
            {
                await using var command = new NpgsqlCommand("INSERT INTO wk.observation_evidence(observation_id,evidence_id) VALUES (@observation,@prediction);", connection);
                command.Parameters.AddWithValue("observation", outageObservation.ObservationId);
                command.Parameters.AddWithValue("prediction", prediction.PredictionId);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }),
            exception => exception.SqlState == "23503").ConfigureAwait(false);

        var observationColumns = await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema='wk' AND table_name='observation';", connection);
            var result = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(reader.GetString(0));
            return result;
        }).ConfigureAwait(false);
        AssertEx.False(observationColumns.Contains("production_method", StringComparer.Ordinal));
        AssertEx.False(observationColumns.Contains("prediction_id", StringComparer.Ordinal));

        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var hardId = Guid.NewGuid();
            await using (var command = new NpgsqlCommand("""
                INSERT INTO wk.correspondence_claim(
                  correspondence_id,left_manifestation_id,relation_namespace,relation_type,right_manifestation_id,
                  method,confidence,strength,valid_range,producer,basis_fingerprint
                ) VALUES (@id,@left,'git','working_copy_of',@right,'llm-name-match',1.0,'hard',tstzrange(clock_timestamp(),NULL,'[)'),'{"kind":"hostile"}'::jsonb,@fingerprint);
                """, connection, transaction))
            {
                var other = Manifestation("github/provider", "github-repository", "decoy-" + Guid.NewGuid().ToString("N"));
                await database.InsertManifestationAsync(other, cancellationToken).ConfigureAwait(false);
                command.Parameters.AddWithValue("id", hardId);
                command.Parameters.AddWithValue("left", manifestation.ManifestationId);
                command.Parameters.AddWithValue("right", other.ManifestationId);
                command.Parameters.AddWithValue("fingerprint", new string('c', 64));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await AssertEx.ThrowsAsync<PostgresException>(() => transaction.CommitAsync(cancellationToken), exception => exception.SqlState == "23514").ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public static async Task ArmIsolationAsync(string secretFile)
    {
        await AssertConnectionDeniedAsync(ConnectionSecrets.ReadConnectionString(secretFile, "memory_connection")).ConfigureAwait(false);
        await AssertConnectionDeniedAsync(ConnectionSecrets.ReadConnectionString(secretFile, "cold_connection")).ConfigureAwait(false);
        var operatorBuilder = new NpgsqlConnectionStringBuilder(ConnectionSecrets.ReadConnectionString(secretFile, "operator_connection"))
        {
            Database = "world_kernel_evaluator"
        };
        await AssertConnectionDeniedAsync(operatorBuilder.ConnectionString).ConfigureAwait(false);

        await using var evaluator = new NpgsqlConnection(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await evaluator.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='wk';", evaluator);
        var kernelTables = Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        AssertEx.Equal(0L, kernelTables, "Evaluator database is physically separate from candidate world state.");
    }

    private static async Task SameTransactionPredictionRejectedAsync(KernelDb database, Guid target)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var actionId = Guid.NewGuid();
            var predictionId = Guid.NewGuid();
            var parameters = JsonSerializer.SerializeToElement(new { branch = "wk-b001-same-tx" });
            await using (var action = new NpgsqlCommand("""
                INSERT INTO wk.action_attempt(
                  action_id,trial_id,configuration_block_id,arm,target_manifestations,owning_eye,capability_name,
                  capability_version,semantic_action_namespace,semantic_action_type,parameters,parameters_hash,
                  evaluation_spec_version,evaluation_spec_hash,producer_model,fixture_scope_id
                ) VALUES (@id,'same-tx','same-tx','hostile',@targets,'git-facet','git.create_branch','v1','git','create_branch',@parameters,@hash,@version,@spec,'{"model":"test"}'::jsonb,'fixture');
                """, connection, transaction))
            {
                action.Parameters.AddWithValue("id", actionId);
                AddJson(action, "targets", JsonSerializer.SerializeToElement(new[] { target }));
                AddJson(action, "parameters", parameters);
                action.Parameters.AddWithValue("hash", CanonicalJson.HashJson(parameters));
                action.Parameters.AddWithValue("version", Build001Contract.EvaluationSpecVersion);
                action.Parameters.AddWithValue("spec", Build001Contract.EvaluationSpecHash);
                await action.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            var vector = Build001Contract.ForAction("git:create_branch").ToDictionary(key => key, _ => 0.5, StringComparer.Ordinal);
            await using (var prediction = new NpgsqlCommand("""
                INSERT INTO wk.prediction(
                  prediction_id,action_id,evaluation_spec_version,evaluation_spec_hash,outcome_probabilities,
                  expected_deltas,expected_invariants,horizons,mechanism,mechanism_version,producer_model
                ) VALUES (@id,@action,@version,@spec,@probabilities,'{}'::jsonb,'{}'::jsonb,'{"H1":"test"}'::jsonb,'test','v1','{"model":"test"}'::jsonb);
                """, connection, transaction))
            {
                prediction.Parameters.AddWithValue("id", predictionId);
                prediction.Parameters.AddWithValue("action", actionId);
                prediction.Parameters.AddWithValue("version", Build001Contract.EvaluationSpecVersion);
                prediction.Parameters.AddWithValue("spec", Build001Contract.EvaluationSpecHash);
                AddJson(prediction, "probabilities", JsonSerializer.SerializeToElement(vector, JsonDefaults.Options));
                await prediction.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await AssertEx.ThrowsAsync<PostgresException>(async () =>
            {
                await using var seal = new NpgsqlCommand("SELECT wk.seal_dispatch(@action,@hash,'{}'::jsonb);", connection, transaction);
                seal.Parameters.AddWithValue("action", actionId);
                seal.Parameters.AddWithValue("hash", CanonicalJson.HashJson(parameters));
                await seal.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }, exception => exception.SqlState == "55000").ConfigureAwait(false);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task<ActionDeclaration> DeclareActionAsync(KernelDb database, Guid target, string semanticAction, string suffix)
    {
        var rawBranch = $"wk-b001-{suffix}-{Guid.NewGuid():N}";
        var parameters = JsonSerializer.SerializeToElement(new { branch = rawBranch[..Math.Min(63, rawBranch.Length)] });
        var declaration = new ActionDeclaration(
            Guid.NewGuid(),
            "trial-" + Guid.NewGuid().ToString("N"),
            "block-" + Guid.NewGuid().ToString("N"),
            suffix.StartsWith("valid", StringComparison.Ordinal) ? "pilot" : "hostile",
            [target],
            semanticAction.StartsWith("github:", StringComparison.Ordinal) ? "eyeBROWSE" : "experiment-git-facet",
            semanticAction,
            "build001-v1",
            semanticAction,
            parameters,
            JsonSerializer.SerializeToElement(new { model = "test-harness", version = "v1" }),
            "world-kernel-build-001-fixture");
        await database.DeclareActionAsync(declaration).ConfigureAwait(false);
        return declaration;
    }

    private static PredictionDeclaration Prediction(ActionDeclaration action, string semanticAction)
    {
        var probabilities = Build001Contract.ForAction(semanticAction).ToDictionary(key => key, _ => (double?)0.7, StringComparer.Ordinal);
        return new PredictionDeclaration(
            Guid.NewGuid(), action.ActionId, semanticAction, probabilities,
            JsonSerializer.SerializeToElement(new { predicted = new[] { "branch_created" } }),
            JsonSerializer.SerializeToElement(new { expected = new[] { "worktree_unchanged" } }),
            JsonSerializer.SerializeToElement(new { H1 = "test", H2 = "test", H3 = "test" }),
            "fresh-cognitive-output", "test-v1",
            JsonSerializer.SerializeToElement(new { model = "test-harness", version = "v1" }));
    }

    private static async Task InsertPredictionRawAsync(KernelDb database, Guid actionId, IReadOnlyDictionary<string, double> probabilities)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.prediction(
                  prediction_id,action_id,evaluation_spec_version,evaluation_spec_hash,outcome_probabilities,
                  expected_deltas,expected_invariants,horizons,mechanism,mechanism_version,producer_model
                ) VALUES (@id,@action,@version,@spec,@probabilities,'{}'::jsonb,'{}'::jsonb,'{"H1":"test"}'::jsonb,'hostile','v1','{"model":"test"}'::jsonb);
                """, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("action", actionId);
            command.Parameters.AddWithValue("version", Build001Contract.EvaluationSpecVersion);
            command.Parameters.AddWithValue("spec", Build001Contract.EvaluationSpecHash);
            AddJson(command, "probabilities", JsonSerializer.SerializeToElement(probabilities, JsonDefaults.Options));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task InsertOutcomeAndEvaluationAsync(
        KernelDb database,
        Guid actionId,
        Guid predictionId,
        Guid outcomeId,
        Guid evaluationId,
        IReadOnlyDictionary<string, bool?> actual,
        PredictionScore score,
        Guid? observationId,
        Guid? evidenceId)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var outcome = new NpgsqlCommand("""
                INSERT INTO wk.outcome(
                  outcome_id,action_id,horizon_id,resolution_status,actual_propositions,actual_deltas,actual_invariants,
                  attribution_status,resolver_version,resolved_at
                ) VALUES (@id,@action,'locked','verified',@actual,'{"branch_created":true}'::jsonb,'{}'::jsonb,
                  'consistent_with_action','build001-evaluator-v1',clock_timestamp());
                """, connection, transaction))
            {
                outcome.Parameters.AddWithValue("id", outcomeId);
                outcome.Parameters.AddWithValue("action", actionId);
                AddJson(outcome, "actual", JsonSerializer.SerializeToElement(actual, JsonDefaults.Options));
                await outcome.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            if (observationId is not null && evidenceId is not null)
            {
                await using var outcomeObservation = new NpgsqlCommand("INSERT INTO wk.outcome_observation(outcome_id,observation_id) VALUES (@outcome,@observation);", connection, transaction);
                outcomeObservation.Parameters.AddWithValue("outcome", outcomeId);
                outcomeObservation.Parameters.AddWithValue("observation", observationId.Value);
                await outcomeObservation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await using var outcomeEvidence = new NpgsqlCommand("INSERT INTO wk.outcome_evidence(outcome_id,evidence_id) VALUES (@outcome,@evidence);", connection, transaction);
                outcomeEvidence.Parameters.AddWithValue("outcome", outcomeId);
                outcomeEvidence.Parameters.AddWithValue("evidence", evidenceId.Value);
                await outcomeEvidence.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await using (var evaluation = new NpgsqlCommand("""
                INSERT INTO wk.prediction_evaluation(
                  evaluation_id,prediction_id,outcome_id,eligibility_status,scorer_version,mean_brier_loss,
                  brier_components,delta_tp,delta_fp,delta_fn,delta_precision,delta_recall,delta_f1,
                  invariant_violations,latency_metrics,evaluated_at
                ) VALUES (@id,@prediction,@outcome,'eligible',@scorer,@mean,@components,@tp,@fp,@fn,@precision,@recall,@f1,@violations,'{}'::jsonb,clock_timestamp());
                """, connection, transaction))
            {
                evaluation.Parameters.AddWithValue("id", evaluationId);
                evaluation.Parameters.AddWithValue("prediction", predictionId);
                evaluation.Parameters.AddWithValue("outcome", outcomeId);
                evaluation.Parameters.AddWithValue("scorer", Build001Contract.ScorerVersion);
                evaluation.Parameters.AddWithValue("mean", score.MeanBrierLoss!.Value);
                AddJson(evaluation, "components", JsonSerializer.SerializeToElement(score.BrierComponents, JsonDefaults.Options));
                evaluation.Parameters.AddWithValue("tp", score.DeltaTruePositive);
                evaluation.Parameters.AddWithValue("fp", score.DeltaFalsePositive);
                evaluation.Parameters.AddWithValue("fn", score.DeltaFalseNegative);
                KernelDb.AddNullable(evaluation, "precision", NpgsqlDbType.Double, score.DeltaPrecision);
                KernelDb.AddNullable(evaluation, "recall", NpgsqlDbType.Double, score.DeltaRecall);
                KernelDb.AddNullable(evaluation, "f1", NpgsqlDbType.Double, score.DeltaF1);
                AddJson(evaluation, "violations", JsonSerializer.SerializeToElement(score.InvariantViolations, JsonDefaults.Options));
                await evaluation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task InsertEpisodeAsync(
        KernelDb database,
        ActionDeclaration action,
        Guid predictionId,
        Guid correspondenceId,
        Guid preObservationId,
        Guid postObservationId,
        Guid outcomeId,
        Guid evaluationId,
        bool omitLinks)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var episodeId = Guid.NewGuid();
            await using (var command = new NpgsqlCommand("""
                INSERT INTO wk.transition_episode(
                  episode_id,trial_id,configuration_block_id,arm,action_id,prediction_id,public_environment_scope,
                  environment_fingerprint,producer_versions,closed_at
                ) VALUES (@id,@trial,@block,@arm,@action,@prediction,'{"fixture":"world-kernel-build-001-fixture"}'::jsonb,@fingerprint,
                  '{"kernel":"build001","git":"2.55"}'::jsonb,clock_timestamp());
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("id", episodeId);
                command.Parameters.AddWithValue("trial", action.TrialId);
                command.Parameters.AddWithValue("block", action.ConfigurationBlockId);
                command.Parameters.AddWithValue("arm", action.Arm);
                command.Parameters.AddWithValue("action", action.ActionId);
                command.Parameters.AddWithValue("prediction", predictionId);
                command.Parameters.AddWithValue("fingerprint", new string('e', 64));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            if (!omitLinks)
            {
                var statements = new[]
                {
                    ("INSERT INTO wk.episode_correspondence VALUES (@episode,@value,DEFAULT);", correspondenceId),
                    ("INSERT INTO wk.episode_pre_observation VALUES (@episode,@value,DEFAULT);", preObservationId),
                    ("INSERT INTO wk.episode_post_observation VALUES (@episode,@value,DEFAULT);", postObservationId),
                    ("INSERT INTO wk.episode_outcome VALUES (@episode,@value,DEFAULT);", outcomeId),
                    ("INSERT INTO wk.episode_evaluation VALUES (@episode,@value,DEFAULT);", evaluationId)
                };
                foreach (var statement in statements)
                {
                    await using var link = new NpgsqlCommand(statement.Item1, connection, transaction);
                    link.Parameters.AddWithValue("episode", episodeId);
                    link.Parameters.AddWithValue("value", statement.Item2);
                    await link.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task<Guid> InsertCandidateCorrespondenceAsync(KernelDb database, Guid left, Guid right)
    {
        var id = Guid.NewGuid();
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.correspondence_claim(
                  correspondence_id,left_manifestation_id,relation_namespace,relation_type,right_manifestation_id,
                  method,confidence,strength,valid_range,producer,basis_fingerprint
                ) VALUES (@id,@left,'git','working_copy_of',@right,'conservative-test',0.5,'candidate',
                  tstzrange(clock_timestamp(),NULL,'[)'),'{"producer":"test"}'::jsonb,@fingerprint);
                """, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("left", left);
            command.Parameters.AddWithValue("right", right);
            command.Parameters.AddWithValue("fingerprint", new string('d', 64));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return id;
    }

    private static ManifestationRecord Manifestation(string provider, string kind, string incarnation, string? nativeId = null) => new(
        Guid.NewGuid(), provider, kind,
        JsonSerializer.SerializeToElement(new { conservative = true, provider_native_required = nativeId is not null }),
        incarnation,
        nativeId,
        JsonDefaults.EmptyObject,
        incarnation);

    private static EvidenceRecord FakeEvidence(string provider, DateTimeOffset capturedAt)
    {
        var id = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes(id.ToString("N"));
        var hash = CanonicalJson.Sha256(bytes);
        return new EvidenceRecord(id, provider, "test-observer", capturedAt, "sha256", hash, $"sha256/{hash[..2]}/{hash.Substring(2, 2)}/{hash}", "application/json", "test", bytes.Length, null, null, "utf-8", JsonDefaults.EmptyObject);
    }

    private static ObservationRecord Observation(Guid manifestation, Guid evidence, DateTimeOffset observedAt) => new(
        Guid.NewGuid(), manifestation, "CODEeye", "test-v1", "codeeye/git-local", observedAt,
        "succeeded", JsonSerializer.SerializeToElement(new { complete = true }), null, null,
        JsonSerializer.SerializeToElement(new { dependency_group = "codeeye-native" }), null, [evidence]);

    private static async Task InsertMaterialClaimAsync(
        KernelDb database,
        Guid claimId,
        Guid manifestationId,
        Guid observationId,
        Guid evidenceId,
        string predicateNamespace,
        string predicate,
        JsonElement value,
        DateTimeOffset validFrom)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var command = new NpgsqlCommand("""
                INSERT INTO wk.claim(
                  claim_id,subject_manifestation_id,predicate_namespace,predicate,value_json,production_method,
                  authority_class,valid_range,scope,producer,confidence,primary_observation_id,primary_evidence_id
                ) VALUES (@id,@subject,@namespace,@predicate,@value,'provider_reported','provider',
                  tstzrange(@valid,NULL,'[)'),'{}'::jsonb,'{"producer":"test"}'::jsonb,1.0,@observation,@evidence);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("id", claimId);
                command.Parameters.AddWithValue("subject", manifestationId);
                command.Parameters.AddWithValue("namespace", predicateNamespace);
                command.Parameters.AddWithValue("predicate", predicate);
                AddJson(command, "value", value);
                command.Parameters.AddWithValue("valid", validFrom);
                command.Parameters.AddWithValue("observation", observationId);
                command.Parameters.AddWithValue("evidence", evidenceId);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (var sql in new[]
                     {
                         "INSERT INTO wk.claim_observation VALUES (@claim,@source,DEFAULT);",
                         "INSERT INTO wk.claim_evidence VALUES (@claim,@source,DEFAULT);"
                     })
            {
                await using var link = new NpgsqlCommand(sql, connection, transaction);
                link.Parameters.AddWithValue("claim", claimId);
                link.Parameters.AddWithValue("source", sql.Contains("observation", StringComparison.Ordinal) ? observationId : evidenceId);
                await link.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task InsertLocatorAsync(
        KernelDb database,
        Guid locatorId,
        Guid manifestationId,
        Guid observationId,
        string value,
        DateTimeOffset from,
        DateTimeOffset? to,
        Guid? supersedes = null)
    {
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO wk.locator(
                  locator_id,manifestation_id,locator_namespace,locator_type,locator_value,valid_range,
                  source_observation_id,supersedes_locator_id,normalization_metadata
                ) VALUES (@id,@manifestation,'filesystem','windows_path',@value,tstzrange(@from,@to,'[)'),@observation,@supersedes,'{}'::jsonb);
                """, connection);
            command.Parameters.AddWithValue("id", locatorId);
            command.Parameters.AddWithValue("manifestation", manifestationId);
            command.Parameters.AddWithValue("value", value);
            command.Parameters.AddWithValue("from", from);
            KernelDb.AddNullable(command, "to", NpgsqlDbType.TimestampTz, to);
            command.Parameters.AddWithValue("observation", observationId);
            KernelDb.AddNullable(command, "supersedes", NpgsqlDbType.Uuid, supersedes);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<Guid>> ClaimIdsAsOfAsync(KernelDb database, Guid subject, DateTimeOffset validAt, DateTimeOffset knownAt) =>
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand("SELECT claim_id FROM wk.claims_as_of(@valid,@known) WHERE subject_manifestation_id=@subject AND predicate='branch_name' ORDER BY claim_id;", connection);
            command.Parameters.AddWithValue("valid", validAt);
            command.Parameters.AddWithValue("known", knownAt);
            command.Parameters.AddWithValue("subject", subject);
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) ids.Add(reader.GetGuid(0));
            return ids;
        }).ConfigureAwait(false);

    private static async Task<T> ScalarAsync<T>(KernelDb database, string sql, params (string Name, object Value)[] parameters) =>
        await database.WithConnectionAsync(async (connection, cancellationToken) =>
        {
            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is T typed) return typed;
            if (typeof(T) == typeof(DateTimeOffset) && value is DateTime dateTime)
            {
                return (T)(object)new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            }
            return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }).ConfigureAwait(false);

    private static void AddJson(NpgsqlCommand command, string name, JsonElement value) => KernelDb.AddJson(command, name, value);

    private static async Task AssertConnectionDeniedAsync(string connectionString)
    {
        var denied = false;
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            denied = true;
        }
        catch (NpgsqlException)
        {
            denied = true;
        }
        AssertEx.True(denied, "Arm isolation credential unexpectedly connected to a forbidden database.");
    }
}
