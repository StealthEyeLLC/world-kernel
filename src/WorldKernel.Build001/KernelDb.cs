using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace StealthEye.WorldKernel.Build001;

public sealed class KernelDb : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public KernelDb(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
        _dataSource = builder.Build();
    }

    public async Task MigrateAsync(IEnumerable<string> sqlFiles, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var sqlFile in sqlFiles)
        {
            var sql = await File.ReadAllTextAsync(sqlFile, cancellationToken).ConfigureAwait(false);
            await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 180 };
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task InsertEvidenceAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO wk.evidence (
              evidence_id, provider_namespace, observer_name, captured_at, hash_algorithm,
              content_hash, blob_ref, media_type, acquisition_method, byte_length,
              provider_revision, provider_event_at, encoding, metadata
            ) VALUES (
              @id, @provider, @observer, @captured, @algorithm,
              @hash, @blob, @media, @method, @length,
              @revision, @event_at, @encoding, @metadata
            );
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", evidence.EvidenceId);
        command.Parameters.AddWithValue("provider", evidence.ProviderNamespace);
        command.Parameters.AddWithValue("observer", evidence.ObserverName);
        command.Parameters.AddWithValue("captured", evidence.CapturedAt);
        command.Parameters.AddWithValue("algorithm", evidence.HashAlgorithm);
        command.Parameters.AddWithValue("hash", evidence.ContentHash);
        command.Parameters.AddWithValue("blob", evidence.BlobRef);
        command.Parameters.AddWithValue("media", evidence.MediaType);
        command.Parameters.AddWithValue("method", evidence.AcquisitionMethod);
        command.Parameters.AddWithValue("length", evidence.ByteLength);
        AddNullable(command, "revision", NpgsqlDbType.Text, evidence.ProviderRevision);
        AddNullable(command, "event_at", NpgsqlDbType.TimestampTz, evidence.ProviderEventAt);
        AddNullable(command, "encoding", NpgsqlDbType.Text, evidence.Encoding);
        AddJson(command, "metadata", evidence.Metadata);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertManifestationAsync(ManifestationRecord manifestation, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO wk.manifestation (
              manifestation_id, provider_namespace, manifestation_kind, identity_basis,
              incarnation_key, provider_native_id, observer_native_ids, display_label
            ) VALUES (@id, @provider, @kind, @basis, @incarnation, @native, @observer_ids, @label);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", manifestation.ManifestationId);
        command.Parameters.AddWithValue("provider", manifestation.ProviderNamespace);
        command.Parameters.AddWithValue("kind", manifestation.ManifestationKind);
        AddJson(command, "basis", manifestation.IdentityBasis);
        command.Parameters.AddWithValue("incarnation", manifestation.IncarnationKey);
        AddNullable(command, "native", NpgsqlDbType.Text, manifestation.ProviderNativeId);
        AddJson(command, "observer_ids", manifestation.ObserverNativeIds);
        AddNullable(command, "label", NpgsqlDbType.Text, manifestation.DisplayLabel);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertObservationAsync(ObservationRecord observation, CancellationToken cancellationToken = default)
    {
        const string observationSql = """
            INSERT INTO wk.observation (
              observation_id, target_manifestation_id, observer_name, observer_version,
              provider_namespace, observed_at, acquisition_status, coverage, provider_revision,
              provider_event_at, source_dependency, raw_normalized_payload
            ) VALUES (
              @id, @target, @observer, @version, @provider, @observed, @status, @coverage,
              @revision, @event_at, @dependency, @payload
            );
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = new NpgsqlCommand(observationSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", observation.ObservationId);
            command.Parameters.AddWithValue("target", observation.TargetManifestationId);
            command.Parameters.AddWithValue("observer", observation.ObserverName);
            command.Parameters.AddWithValue("version", observation.ObserverVersion);
            command.Parameters.AddWithValue("provider", observation.ProviderNamespace);
            command.Parameters.AddWithValue("observed", observation.ObservedAt);
            command.Parameters.AddWithValue("status", observation.AcquisitionStatus);
            AddJson(command, "coverage", observation.Coverage);
            AddNullable(command, "revision", NpgsqlDbType.Text, observation.ProviderRevision);
            AddNullable(command, "event_at", NpgsqlDbType.TimestampTz, observation.ProviderEventAt);
            AddJson(command, "dependency", observation.SourceDependency);
            if (observation.RawNormalizedPayload is { } payload)
            {
                AddJson(command, "payload", payload);
            }
            else
            {
                AddNullable(command, "payload", NpgsqlDbType.Jsonb, null);
            }
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var evidenceId in observation.EvidenceIds.Distinct())
        {
            await using var link = new NpgsqlCommand(
                "INSERT INTO wk.observation_evidence(observation_id,evidence_id) VALUES (@observation,@evidence);",
                connection,
                transaction);
            link.Parameters.AddWithValue("observation", observation.ObservationId);
            link.Parameters.AddWithValue("evidence", evidenceId);
            await link.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeclareActionAsync(ActionDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var action = Build001Contract.SplitAction(declaration.SemanticAction);
        var parametersHash = CanonicalJson.HashJson(declaration.Parameters);
        var targetsJson = JsonSerializer.SerializeToElement(declaration.TargetManifestations, JsonDefaults.Options);
        const string sql = """
            INSERT INTO wk.action_attempt (
              action_id, trial_id, configuration_block_id, arm, target_manifestations,
              owning_eye, capability_name, capability_version, semantic_action_namespace,
              semantic_action_type, parameters, parameters_hash, evaluation_spec_version,
              evaluation_spec_hash, producer_model, fixture_scope_id
            ) VALUES (
              @id, @trial, @block, @arm, @targets, @eye, @capability, @capability_version,
              @namespace, @type, @parameters, @parameters_hash, @spec_version, @spec_hash,
              @producer, @fixture
            );
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", declaration.ActionId);
            command.Parameters.AddWithValue("trial", declaration.TrialId);
            command.Parameters.AddWithValue("block", declaration.ConfigurationBlockId);
            command.Parameters.AddWithValue("arm", declaration.Arm);
            AddJson(command, "targets", targetsJson);
            command.Parameters.AddWithValue("eye", declaration.OwningEye);
            command.Parameters.AddWithValue("capability", declaration.CapabilityName);
            command.Parameters.AddWithValue("capability_version", declaration.CapabilityVersion);
            command.Parameters.AddWithValue("namespace", action.Namespace);
            command.Parameters.AddWithValue("type", action.Type);
            AddJson(command, "parameters", declaration.Parameters);
            command.Parameters.AddWithValue("parameters_hash", parametersHash);
            command.Parameters.AddWithValue("spec_version", Build001Contract.EvaluationSpecVersion);
            command.Parameters.AddWithValue("spec_hash", Build001Contract.EvaluationSpecHash);
            AddJson(command, "producer", declaration.ProducerModel);
            command.Parameters.AddWithValue("fixture", declaration.FixtureScopeId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var target in declaration.TargetManifestations.Distinct())
        {
            await using var targetCommand = new NpgsqlCommand(
                "INSERT INTO wk.action_target(action_id,manifestation_id,target_role) VALUES (@action,@target,'material_target');",
                connection,
                transaction);
            targetCommand.Parameters.AddWithValue("action", declaration.ActionId);
            targetCommand.Parameters.AddWithValue("target", target);
            await targetCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> CommitPredictionAsync(
        PredictionDeclaration declaration,
        CancellationToken cancellationToken = default)
    {
        var normalized = Build001Contract.NormalizePrediction(
            declaration.SemanticAction,
            declaration.Probabilities,
            out var defects);
        var probabilitiesJson = JsonSerializer.SerializeToElement(normalized, JsonDefaults.Options);
        const string sql = """
            INSERT INTO wk.prediction (
              prediction_id, action_id, evaluation_spec_version, evaluation_spec_hash,
              outcome_probabilities, expected_deltas, expected_invariants, horizons,
              mechanism, mechanism_version, producer_model
            ) VALUES (
              @id, @action, @spec_version, @spec_hash, @probabilities, @deltas,
              @invariants, @horizons, @mechanism, @mechanism_version, @producer
            );
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", declaration.PredictionId);
        command.Parameters.AddWithValue("action", declaration.ActionId);
        command.Parameters.AddWithValue("spec_version", Build001Contract.EvaluationSpecVersion);
        command.Parameters.AddWithValue("spec_hash", Build001Contract.EvaluationSpecHash);
        AddJson(command, "probabilities", probabilitiesJson);
        AddJson(command, "deltas", declaration.ExpectedDeltas);
        AddJson(command, "invariants", declaration.ExpectedInvariants);
        AddJson(command, "horizons", declaration.Horizons);
        command.Parameters.AddWithValue("mechanism", declaration.Mechanism);
        command.Parameters.AddWithValue("mechanism_version", declaration.MechanismVersion);
        AddJson(command, "producer", declaration.ProducerModel);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return defects;
    }

    public async Task<Guid> SealDispatchAsync(
        Guid actionId,
        JsonElement parameters,
        JsonElement dispatchPayload,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT wk.seal_dispatch(@action, @parameters_hash, @payload);";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("action", actionId);
        command.Parameters.AddWithValue("parameters_hash", CanonicalJson.HashJson(parameters));
        AddJson(command, "payload", dispatchPayload);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result is Guid id ? id : throw new DataException("Dispatch seal did not return an action phase id.");
    }

    public async Task AppendActionPhaseAsync(
        Guid actionId,
        string phase,
        JsonElement payload,
        Guid? evidenceId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO wk.action_phase(action_phase_id,action_id,phase,payload,evidence_id)
            VALUES (@id,@action,@phase,@payload,@evidence);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("action", actionId);
        command.Parameters.AddWithValue("phase", phase);
        AddJson(command, "payload", payload);
        AddNullable(command, "evidence", NpgsqlDbType.Uuid, evidenceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> WithConnectionAsync<T>(
        Func<NpgsqlConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await operation(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task WithConnectionAsync(
        Func<NpgsqlConnection, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await operation(connection, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    public static void AddJson(NpgsqlCommand command, string name, JsonElement value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = value.GetRawText() });
    }

    public static void AddNullable(NpgsqlCommand command, string name, NpgsqlDbType type, object? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
    }
}
