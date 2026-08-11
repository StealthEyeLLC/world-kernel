using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Campaign2Runner;

public sealed class Campaign2Ledger : IAsyncDisposable
{
    public const string HostedProviderNativeId = "1330898503";
    public const string HostedManifestationRef = "github:repo:StealthEyeLLC/world-kernel-build-001-fixture#1330898503";
    public const string HostedUrl = "https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git";
    private readonly KernelDb _kernel;
    private readonly NpgsqlDataSource _evaluator;
    private readonly EvidenceStore _evidence;

    public Campaign2Ledger(string secretFile, string evidenceRoot)
    {
        _kernel = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        _evaluator = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        _evidence = new EvidenceStore(evidenceRoot);
    }

    public async Task<object> BoundaryAsync(CancellationToken ct = default)
    {
        await using var evalConnection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        var evaluator = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in new[] { "seed_commitment", "hidden_configuration", "arm_randomization", "invocation_attestation", "ground_truth", "aggregate_result" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM eval001.{table}", evalConnection);
            evaluator[table] = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        }
        var world = await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var item in new[]
            {
                ("campaign2_actions", "SELECT count(*) FROM wk.action_attempt WHERE trial_id LIKE 'campaign2-%'"),
                ("campaign2_predictions", "SELECT count(*) FROM wk.prediction p JOIN wk.action_attempt a ON a.action_id=p.action_id WHERE a.trial_id LIKE 'campaign2-%'"),
                ("campaign2_episodes", "SELECT count(*) FROM wk.transition_episode WHERE trial_id LIKE 'campaign2-%'")
            })
            {
                await using var cmd = new NpgsqlCommand(item.Item2, connection);
                result[item.Item1] = Convert.ToInt64(await cmd.ExecuteScalarAsync(inner).ConfigureAwait(false));
            }
            return result;
        }, ct).ConfigureAwait(false);
        return new { evaluator, world };
    }

    public async Task RecordSeedCommitmentAsync(SeedCommitInput input, CancellationToken ct = default)
    {
        ValidateSha256(input.CommitmentSha256, nameof(input.CommitmentSha256));
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.seed_commitment(seed_id,phase,configuration_block_id,commitment_sha256,sealed_payload_ref,public_fixture_revision)
            VALUES (@seed,@phase,@block,@commitment,@ref,@revision) ON CONFLICT (seed_id) DO NOTHING;
            """, connection))
        {
            cmd.Parameters.AddWithValue("seed", input.SeedId); cmd.Parameters.AddWithValue("phase", input.Phase);
            cmd.Parameters.AddWithValue("block", input.ConfigurationBlockId); cmd.Parameters.AddWithValue("commitment", input.CommitmentSha256);
            cmd.Parameters.AddWithValue("ref", input.SealedPayloadRef); cmd.Parameters.AddWithValue("revision", input.PublicFixtureRevision);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await using var verify = new NpgsqlCommand("SELECT phase,configuration_block_id,commitment_sha256,sealed_payload_ref,public_fixture_revision FROM eval001.seed_commitment WHERE seed_id=@seed", connection);
        verify.Parameters.AddWithValue("seed", input.SeedId);
        await using var reader = await verify.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetString(0) != input.Phase || reader.GetString(1) != input.ConfigurationBlockId ||
            reader.GetString(2) != input.CommitmentSha256 || reader.GetString(3) != input.SealedPayloadRef || reader.GetString(4) != input.PublicFixtureRevision)
            throw new InvalidOperationException("Existing seed commitment differs from requested commitment.");
    }

    public async Task RecordHiddenConfigurationAsync(HiddenConfigurationInput input, CancellationToken ct = default)
    {
        ValidateSha256(input.ExpectedResetFingerprint, nameof(input.ExpectedResetFingerprint));
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.hidden_configuration(hidden_configuration_id,seed_id,regime_label,configuration,expected_reset_fingerprint,answer_key_version)
            VALUES (@id,@seed,@label,@configuration,@fingerprint,@version) ON CONFLICT (seed_id) DO NOTHING;
            """, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("seed", input.SeedId); cmd.Parameters.AddWithValue("label", input.RegimeLabel);
        AddJson(cmd, "configuration", input.Configuration); cmd.Parameters.AddWithValue("fingerprint", input.ExpectedResetFingerprint); cmd.Parameters.AddWithValue("version", input.AnswerKeyVersion);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task RecordResetVerificationAsync(ResetVerificationInput input, CancellationToken ct = default)
    {
        ValidateSha256(input.ActualFingerprint, nameof(input.ActualFingerprint)); ValidateSha256(input.ExpectedFingerprint, nameof(input.ExpectedFingerprint));
        foreach (var hash in input.ProviderEvidenceHashes) ValidateSha256(hash, "provider evidence hash");
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.reset_verification(reset_verification_id,seed_id,arm,generation_id,actual_fingerprint,expected_fingerprint,provider_evidence_hashes,passed)
            VALUES (@id,@seed,@arm,@generation,@actual,@expected,@hashes,@passed);
            """, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("seed", input.SeedId); cmd.Parameters.AddWithValue("arm", NormalizeArmForKernel(input.Arm));
        cmd.Parameters.AddWithValue("generation", input.GenerationId); cmd.Parameters.AddWithValue("actual", input.ActualFingerprint); cmd.Parameters.AddWithValue("expected", input.ExpectedFingerprint);
        AddJson(cmd, "hashes", JsonSerializer.SerializeToElement(input.ProviderEvidenceHashes, JsonDefaults.Options)); cmd.Parameters.AddWithValue("passed", input.Passed);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task RecordArmRandomizationAsync(ArmRandomizationInput input, CancellationToken ct = default)
    {
        ValidateSha256(input.RandomizationProof, nameof(input.RandomizationProof));
        var order = input.ArmOrder.Select(NormalizeArmForEvaluator).ToArray();
        if (order.Length != 3 || !order.Order(StringComparer.Ordinal).SequenceEqual(new[] { "cold", "memory", "structured" }, StringComparer.Ordinal))
            throw new InvalidDataException("Arm order must contain cold, memory, and structured exactly once.");
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.arm_randomization(configuration_block_id,seed_id,arm_order,randomizer_version,randomization_proof)
            VALUES (@block,@seed,@order,@version,@proof);
            """, connection);
        cmd.Parameters.AddWithValue("block", input.ConfigurationBlockId); cmd.Parameters.AddWithValue("seed", input.SeedId); cmd.Parameters.AddWithValue("order", order);
        cmd.Parameters.AddWithValue("version", input.RandomizerVersion); cmd.Parameters.AddWithValue("proof", input.RandomizationProof);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<TrialLedgerState> DeclareTrialAsync(TrialDeclareInput input, CancellationToken ct = default)
    {
        if (!input.TrialId.StartsWith("campaign2-", StringComparison.Ordinal)) throw new InvalidDataException("Campaign 2 trial ids must start with campaign2-.");
        foreach (var item in new[] { (input.ProviderVersionFingerprint, "provider version"), (input.EnvironmentFingerprint, "environment"),
                     (input.ModelConfigurationHash, "model configuration"), (input.CommonInstructionsHash, "common instructions") }) ValidateSha256(item.Item1, item.Item2);
        if (input.InheritedPackageHash is not null) ValidateSha256(input.InheritedPackageHash, "inherited package");
        var normalized = Build001Contract.NormalizePrediction(input.SemanticAction, input.Prediction, out var defects);
        if (defects.Count != 0) throw new InvalidDataException("Locked prediction vector defects: " + string.Join(',', defects));

        var localId = StableGuid("manifestation|local|" + Path.GetFullPath(input.WorkingCopy).ToLowerInvariant());
        var hostedId = StableGuid("manifestation|hosted|" + HostedProviderNativeId);
        await EnsureManifestationAsync(localId, "git", "working_copy", input.WorkingCopy, CanonicalJson.Sha256Utf8(Path.GetFullPath(input.WorkingCopy).ToLowerInvariant()), null, input.WorkingCopy, ct).ConfigureAwait(false);
        await EnsureManifestationAsync(hostedId, "github", "repository", HostedManifestationRef, HostedProviderNativeId, HostedProviderNativeId, "StealthEyeLLC/world-kernel-build-001-fixture", ct).ConfigureAwait(false);

        var localEvidence = await StoreObservationEvidenceAsync(input.LocalObservation, "campaign2-pre-action-observation", ct).ConfigureAwait(false);
        var providerEvidence = await StoreObservationEvidenceAsync(input.ProviderObservation, "campaign2-pre-action-provider-observation", ct).ConfigureAwait(false);
        var localObservation = await InsertObservationAsync(localId, input.LocalObservation, localEvidence, ct).ConfigureAwait(false);
        var providerObservation = await InsertObservationAsync(hostedId, input.ProviderObservation, providerEvidence, ct).ConfigureAwait(false);

        var correspondenceId = Guid.NewGuid();
        var preClaimIds = new List<Guid>();
        await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            await using var tx = await connection.BeginTransactionAsync(inner).ConfigureAwait(false);
            var remoteClaim = await InsertClaimAsync(connection, tx, localId, "git", "configured_remote_url", JsonSerializer.SerializeToElement(HostedUrl), "observed", "provider", input.LocalObservation.CapturedAt, localObservation.ObservationId, localEvidence.EvidenceId, inner).ConfigureAwait(false);
            var nativeClaim = await InsertClaimAsync(connection, tx, hostedId, "github", "hosted_provider_native_id", JsonSerializer.SerializeToElement(HostedProviderNativeId), "provider_reported", "provider", input.ProviderObservation.CapturedAt, providerObservation.ObservationId, providerEvidence.EvidenceId, inner).ConfigureAwait(false);
            var sharedClaim = await InsertClaimAsync(connection, tx, localId, "git", "shared_exact_commit", JsonSerializer.SerializeToElement(input.SharedExactCommit), "observed", "provider", input.LocalObservation.CapturedAt, localObservation.ObservationId, localEvidence.EvidenceId, inner).ConfigureAwait(false);
            var topologyClaim = await InsertClaimAsync(connection, tx, localId, "git", "topology", JsonSerializer.SerializeToElement(input.PublicTopologyClass), "derived", "derived", input.LocalObservation.CapturedAt, localObservation.ObservationId, localEvidence.EvidenceId, inner).ConfigureAwait(false);
            preClaimIds.AddRange([remoteClaim, nativeClaim, sharedClaim, topologyClaim]);
            var basis = CanonicalJson.Sha256Utf8(string.Join('|', new[] { localEvidence.ContentHash, providerEvidence.ContentHash, input.SharedExactCommit, HostedProviderNativeId }));
            await using (var corr = new NpgsqlCommand("""
                INSERT INTO wk.correspondence_claim(correspondence_id,left_manifestation_id,relation_namespace,relation_type,right_manifestation_id,method,confidence,strength,valid_range,producer,basis_fingerprint)
                VALUES (@id,@left,'git','working_copy_of',@right,'campaign2-provider-and-history-evidence',1.0,'hard',tstzrange(@from,NULL,'[)'),@producer,@basis);
                """, connection, tx))
            {
                corr.Parameters.AddWithValue("id", correspondenceId); corr.Parameters.AddWithValue("left", localId); corr.Parameters.AddWithValue("right", hostedId);
                corr.Parameters.AddWithValue("from", input.LocalObservation.CapturedAt); AddJson(corr, "producer", JsonSerializer.SerializeToElement(new { component = "campaign2-runner", version = "v1" }, JsonDefaults.Options));
                corr.Parameters.AddWithValue("basis", basis); await corr.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
            foreach (var claim in new[] { remoteClaim, nativeClaim, sharedClaim }) await InsertPairAsync(connection, tx, "correspondence_claim_basis", "correspondence_id", "claim_id", correspondenceId, claim, inner).ConfigureAwait(false);
            foreach (var observation in new[] { localObservation.ObservationId, providerObservation.ObservationId }) await InsertPairAsync(connection, tx, "correspondence_observation", "correspondence_id", "observation_id", correspondenceId, observation, inner).ConfigureAwait(false);
            foreach (var evidence in new[] { localEvidence.EvidenceId, providerEvidence.EvidenceId }) await InsertPairAsync(connection, tx, "correspondence_evidence", "correspondence_id", "evidence_id", correspondenceId, evidence, inner).ConfigureAwait(false);
            await tx.CommitAsync(inner).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        var actionId = Guid.NewGuid(); var predictionId = Guid.NewGuid();
        var target = input.SemanticAction.StartsWith("github:", StringComparison.Ordinal) ? hostedId : localId;
        await _kernel.DeclareActionAsync(new ActionDeclaration(actionId, input.TrialId, input.ConfigurationBlockId, NormalizeArmForKernel(input.Arm), [target],
            input.SemanticAction.StartsWith("github:", StringComparison.Ordinal) ? "eyeBROWSE" : "world-kernel-native-git-facet",
            input.SemanticAction, "build001-v1", input.SemanticAction, input.Parameters, input.ProducerModel, input.FixtureScopeId), ct).ConfigureAwait(false);
        var expectedDeltas = JsonSerializer.SerializeToElement(input.ExpectedDeltas.ToDictionary(x => x, _ => true, StringComparer.Ordinal), JsonDefaults.Options);
        var expectedInvariants = JsonSerializer.SerializeToElement(input.ExpectedInvariants.ToDictionary(x => x, _ => true, StringComparer.Ordinal), JsonDefaults.Options);
        await _kernel.CommitPredictionAsync(new PredictionDeclaration(predictionId, actionId, input.SemanticAction,
            normalized.ToDictionary(x => x.Key, x => (double?)x.Value, StringComparer.Ordinal), expectedDeltas, expectedInvariants, input.Horizons,
            "fresh-chat-subject", "campaign2-temporary-chat-isolation-v1", input.ProducerModel), ct).ConfigureAwait(false);
        await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            foreach (var evidence in new[] { localEvidence.EvidenceId, providerEvidence.EvidenceId })
            {
                await using var cmd = new NpgsqlCommand("INSERT INTO wk.prediction_basis_evidence(prediction_id,evidence_id) VALUES (@p,@e);", connection);
                cmd.Parameters.AddWithValue("p", predictionId); cmd.Parameters.AddWithValue("e", evidence); await cmd.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
            foreach (var episode in input.SourceEpisodeIds)
            {
                await using var cmd = new NpgsqlCommand("INSERT INTO wk.prediction_basis_episode(prediction_id,episode_id) VALUES (@p,@e);", connection);
                cmd.Parameters.AddWithValue("p", predictionId); cmd.Parameters.AddWithValue("e", episode); await cmd.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        if (!string.Equals(input.Phase, "acquisition", StringComparison.Ordinal)) await RecordInvocationAttestationAsync(input, ct).ConfigureAwait(false);
        return new TrialLedgerState(input.TrialId, input.ConfigurationBlockId, input.Phase, input.Arm, input.SemanticAction, input.FixtureManifestationRef,
            input.PublicTopologyClass, input.ProviderVersionFingerprint, input.SharedExactCommit, input.EnvironmentFingerprint, input.LocalObservation.CapturedAt,
            localId, hostedId, correspondenceId, [localObservation.ObservationId, providerObservation.ObservationId], preClaimIds,
            [localEvidence.ContentHash, providerEvidence.ContentHash], actionId, predictionId, input.Parameters, normalized,
            input.ExpectedDeltas, input.ExpectedInvariants, input.Horizons, input.SourceEpisodeIds,
            input.LocalObservation.PublicFacts.Concat(input.ProviderObservation.PublicFacts).ToArray());
    }

    public Task<Guid> SealDispatchAsync(TrialLedgerState state, JsonElement payload, CancellationToken ct = default) => _kernel.SealDispatchAsync(state.ActionId, state.Parameters, payload, ct);

    public async Task<TrialCloseResult> CloseTrialAsync(TrialLedgerState state, TrialCloseInput input, string episodeExportPath, CancellationToken ct = default)
    {
        var locked = Build001Contract.PropositionsFor(state.SemanticAction).Order(StringComparer.Ordinal).ToArray();
        if (!locked.SequenceEqual(input.ActualPropositions.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Ground-truth vector differs from locked proposition vector.");
        var localEvidence = await StoreObservationEvidenceAsync(input.LocalObservation, "campaign2-post-action-observation", ct).ConfigureAwait(false);
        var providerEvidence = await StoreObservationEvidenceAsync(input.ProviderObservation, "campaign2-post-action-provider-observation", ct).ConfigureAwait(false);
        var localObservation = await InsertObservationAsync(state.LocalManifestationId, input.LocalObservation, localEvidence, ct).ConfigureAwait(false);
        var providerObservation = await InsertObservationAsync(state.HostedManifestationId, input.ProviderObservation, providerEvidence, ct).ConfigureAwait(false);
        await _kernel.AppendActionPhaseAsync(state.ActionId, "provider_acknowledged", input.Receipt, null, ct).ConfigureAwait(false);
        await _kernel.AppendActionPhaseAsync(state.ActionId, "post_observed", input.ProviderObservation.Payload, providerEvidence.EvidenceId, ct).ConfigureAwait(false);

        var score = PredictionScorer.Score(state.SemanticAction, state.Prediction.ToDictionary(x => x.Key, x => (double?)x.Value, StringComparer.Ordinal),
            input.ActualPropositions, state.ExpectedDeltas, input.MaterialDeltas, state.ExpectedInvariants, input.InvariantViolations);
        var outcomeId = Guid.NewGuid(); var evaluationId = Guid.NewGuid(); var episodeId = Guid.NewGuid();
        var postClaims = new List<PublicClaimExport>();
        await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            await using var tx = await connection.BeginTransactionAsync(inner).ConfigureAwait(false);
            await using (var outcome = new NpgsqlCommand("""
                INSERT INTO wk.outcome(outcome_id,action_id,horizon_id,resolution_status,actual_propositions,actual_deltas,actual_invariants,attribution_status,resolver_version,resolved_at)
                VALUES (@id,@action,'locked-vector',@status,@actual,@deltas,@invariants,@attribution,@resolver,clock_timestamp());
                """, connection, tx))
            {
                outcome.Parameters.AddWithValue("id", outcomeId); outcome.Parameters.AddWithValue("action", state.ActionId); outcome.Parameters.AddWithValue("status", input.ResolutionStatus);
                AddJson(outcome, "actual", JsonSerializer.SerializeToElement(input.ActualPropositions, JsonDefaults.Options));
                AddJson(outcome, "deltas", JsonSerializer.SerializeToElement(input.MaterialDeltas.ToDictionary(x => x, _ => true, StringComparer.Ordinal), JsonDefaults.Options));
                AddJson(outcome, "invariants", JsonSerializer.SerializeToElement(input.InvariantViolations.ToDictionary(x => x, _ => false, StringComparer.Ordinal), JsonDefaults.Options));
                outcome.Parameters.AddWithValue("attribution", input.AttributionStatus); outcome.Parameters.AddWithValue("resolver", input.ResolverVersion);
                await outcome.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
            foreach (var observation in new[] { localObservation.ObservationId, providerObservation.ObservationId }) await InsertPairAsync(connection, tx, "outcome_observation", "outcome_id", "observation_id", outcomeId, observation, inner).ConfigureAwait(false);
            foreach (var evidence in new[] { localEvidence.EvidenceId, providerEvidence.EvidenceId }) await InsertPairAsync(connection, tx, "outcome_evidence", "outcome_id", "evidence_id", outcomeId, evidence, inner).ConfigureAwait(false);
            await using (var evaluation = new NpgsqlCommand("""
                INSERT INTO wk.prediction_evaluation(evaluation_id,prediction_id,outcome_id,eligibility_status,scorer_version,mean_brier_loss,brier_components,delta_tp,delta_fp,delta_fn,delta_precision,delta_recall,delta_f1,invariant_violations,latency_metrics,evaluated_at)
                VALUES (@id,@prediction,@outcome,@eligibility,@scorer,@mean,@components,@tp,@fp,@fn,@precision,@recall,@f1,@violations,@latency,clock_timestamp());
                """, connection, tx))
            {
                evaluation.Parameters.AddWithValue("id", evaluationId); evaluation.Parameters.AddWithValue("prediction", state.PredictionId); evaluation.Parameters.AddWithValue("outcome", outcomeId);
                evaluation.Parameters.AddWithValue("eligibility", score.EligibilityStatus); evaluation.Parameters.AddWithValue("scorer", Build001Contract.ScorerVersion);
                AddNullable(evaluation, "mean", NpgsqlDbType.Double, score.MeanBrierLoss); AddJson(evaluation, "components", JsonSerializer.SerializeToElement(score.BrierComponents, JsonDefaults.Options));
                evaluation.Parameters.AddWithValue("tp", score.DeltaTruePositive); evaluation.Parameters.AddWithValue("fp", score.DeltaFalsePositive); evaluation.Parameters.AddWithValue("fn", score.DeltaFalseNegative);
                AddNullable(evaluation, "precision", NpgsqlDbType.Double, score.DeltaPrecision); AddNullable(evaluation, "recall", NpgsqlDbType.Double, score.DeltaRecall); AddNullable(evaluation, "f1", NpgsqlDbType.Double, score.DeltaF1);
                AddJson(evaluation, "violations", JsonSerializer.SerializeToElement(score.InvariantViolations, JsonDefaults.Options)); AddJson(evaluation, "latency", input.LatencyMetrics);
                await evaluation.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
            foreach (var proposition in input.ActualPropositions)
            {
                var claim = await InsertClaimAsync(connection, tx, state.HostedManifestationId, "outcome", proposition.Key, JsonSerializer.SerializeToElement(proposition.Value, JsonDefaults.Options),
                    "observed", "provider", input.ProviderObservation.CapturedAt, providerObservation.ObservationId, providerEvidence.EvidenceId, inner).ConfigureAwait(false);
                postClaims.Add(new PublicClaimExport(claim, HostedManifestationRef, "outcome:" + proposition.Key, JsonSerializer.SerializeToElement(proposition.Value, JsonDefaults.Options),
                    "provider", "observed", input.ProviderObservation.CapturedAt, null, DateTimeOffset.UtcNow, "historical_at_retrieval", "supported", [providerEvidence.ContentHash]));
            }
            await tx.CommitAsync(inner).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        await _kernel.AppendActionPhaseAsync(state.ActionId, "outcome_resolved", JsonSerializer.SerializeToElement(new { outcome_id = outcomeId, input.ResolutionStatus }, JsonDefaults.Options), providerEvidence.EvidenceId, ct).ConfigureAwait(false);
        await _kernel.AppendActionPhaseAsync(state.ActionId, "evaluated", JsonSerializer.SerializeToElement(new { evaluation_id = evaluationId, score.MeanBrierLoss }, JsonDefaults.Options), null, ct).ConfigureAwait(false);

        await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            await using var tx = await connection.BeginTransactionAsync(inner).ConfigureAwait(false);
            await using (var episode = new NpgsqlCommand("""
                INSERT INTO wk.transition_episode(episode_id,trial_id,configuration_block_id,arm,action_id,prediction_id,public_environment_scope,environment_fingerprint,producer_versions,closed_at)
                VALUES (@id,@trial,@block,@arm,@action,@prediction,@scope,@fingerprint,@versions,clock_timestamp());
                """, connection, tx))
            {
                episode.Parameters.AddWithValue("id", episodeId); episode.Parameters.AddWithValue("trial", state.TrialId); episode.Parameters.AddWithValue("block", state.ConfigurationBlockId);
                episode.Parameters.AddWithValue("arm", NormalizeArmForKernel(state.Arm)); episode.Parameters.AddWithValue("action", state.ActionId); episode.Parameters.AddWithValue("prediction", state.PredictionId);
                AddJson(episode, "scope", JsonSerializer.SerializeToElement(new { fixture = "StealthEyeLLC/world-kernel-build-001-fixture", topology = state.PublicTopologyClass }, JsonDefaults.Options));
                episode.Parameters.AddWithValue("fingerprint", state.EnvironmentFingerprint); AddJson(episode, "versions", JsonSerializer.SerializeToElement(new { kernel = "build001", scorer = Build001Contract.ScorerVersion, runner = "campaign2-runner-v1" }, JsonDefaults.Options));
                await episode.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
            }
            await InsertPairAsync(connection, tx, "episode_correspondence", "episode_id", "correspondence_id", episodeId, state.CorrespondenceId, inner).ConfigureAwait(false);
            foreach (var id in state.PreObservationIds) await InsertPairAsync(connection, tx, "episode_pre_observation", "episode_id", "observation_id", episodeId, id, inner).ConfigureAwait(false);
            foreach (var id in state.PreClaimIds) await InsertPairAsync(connection, tx, "episode_pre_claim", "episode_id", "claim_id", episodeId, id, inner).ConfigureAwait(false);
            foreach (var id in new[] { localObservation.ObservationId, providerObservation.ObservationId }) await InsertPairAsync(connection, tx, "episode_post_observation", "episode_id", "observation_id", episodeId, id, inner).ConfigureAwait(false);
            await InsertPairAsync(connection, tx, "episode_outcome", "episode_id", "outcome_id", episodeId, outcomeId, inner).ConfigureAwait(false);
            await InsertPairAsync(connection, tx, "episode_evaluation", "episode_id", "evaluation_id", episodeId, evaluationId, inner).ConfigureAwait(false);
            await tx.CommitAsync(inner).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        await RecordGroundTruthAsync(state, input, localEvidence, providerEvidence, ct).ConfigureAwait(false);

        var localRef = "git:working-copy:" + state.TrialId;
        var preClaims = new[]
        {
            new PublicClaimExport(state.PreClaimIds[0], localRef, "git:configured_remote_url", JsonSerializer.SerializeToElement(HostedUrl), "provider", "observed", state.PreKnownAt, null, state.PreKnownAt, "historical_at_retrieval", "supported", [state.PreEvidenceHashes[0]]),
            new PublicClaimExport(state.PreClaimIds[1], HostedManifestationRef, "github:hosted_provider_native_id", JsonSerializer.SerializeToElement(HostedProviderNativeId), "provider", "provider_reported", state.PreKnownAt, null, state.PreKnownAt, "historical_at_retrieval", "supported", [state.PreEvidenceHashes[1]]),
            new PublicClaimExport(state.PreClaimIds[2], localRef, "git:shared_exact_commit", JsonSerializer.SerializeToElement(state.SharedExactCommit), "provider", "observed", state.PreKnownAt, null, state.PreKnownAt, "historical_at_retrieval", "supported", [state.PreEvidenceHashes[0]]),
            new PublicClaimExport(state.PreClaimIds[3], localRef, "git:topology", JsonSerializer.SerializeToElement(state.PublicTopologyClass), "derived", "derived", state.PreKnownAt, null, state.PreKnownAt, "historical_at_retrieval", "supported", [state.PreEvidenceHashes[0]])
        };
        var correspondence = new PublicCorrespondenceExport(state.CorrespondenceId, localRef, "git:working_copy_of", HostedManifestationRef, "supported", 1.0, state.PreKnownAt, state.PreKnownAt, state.PreEvidenceHashes);
        var export = new EpisodeExport(episodeId, state.SemanticAction, state.FixtureManifestationRef, state.PublicTopologyClass, DateTimeOffset.UtcNow,
            state.PreObservedFacts, state.Prediction, input.ActualPropositions, score.BrierComponents, score.MeanBrierLoss, input.MaterialDeltas,
            score.InvariantViolations, input.ResolutionStatus, preClaims.Concat(postClaims).ToArray(), [correspondence],
            state.PreEvidenceHashes.Concat([localEvidence.ContentHash, providerEvidence.ContentHash]).Distinct(StringComparer.Ordinal).ToArray(), state.ProviderVersionFingerprint);
        var bytes = CanonicalJson.Serialize(export); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(episodeExportPath))!);
        await File.WriteAllBytesAsync(episodeExportPath, bytes, ct).ConfigureAwait(false);
        return new TrialCloseResult(episodeId, outcomeId, evaluationId, score.MeanBrierLoss, score.EligibilityStatus, episodeExportPath, CanonicalJson.Sha256(bytes));
    }

    private async Task RecordInvocationAttestationAsync(TrialDeclareInput input, CancellationToken ct)
    {
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.invocation_attestation(invocation_id,configuration_block_id,arm,isolated_session_id,isolation_mechanism,memory_state,model_identifier,model_configuration_hash,common_instructions_hash,inherited_package_hash,inherited_tokens,started_at,completed_at,attestation_evidence_ref)
            VALUES (@id,@block,@arm,@session,'campaign2-temporary-chat-isolation-v1','not_available','5.6 Sol',@model,@instructions,@package,@tokens,clock_timestamp(),clock_timestamp(),@ref);
            """, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("block", input.ConfigurationBlockId); cmd.Parameters.AddWithValue("arm", NormalizeArmForEvaluator(input.Arm));
        cmd.Parameters.AddWithValue("session", input.IsolatedSessionId); cmd.Parameters.AddWithValue("model", input.ModelConfigurationHash); cmd.Parameters.AddWithValue("instructions", input.CommonInstructionsHash);
        AddNullable(cmd, "package", NpgsqlDbType.Char, input.InheritedPackageHash); cmd.Parameters.AddWithValue("tokens", input.InheritedTokens); cmd.Parameters.AddWithValue("ref", input.SubjectAttestationRef);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task RecordGroundTruthAsync(TrialLedgerState state, TrialCloseInput input, EvidenceRecord localEvidence, EvidenceRecord providerEvidence, CancellationToken ct)
    {
        await using var connection = await _evaluator.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO eval001.ground_truth(ground_truth_id,action_id,configuration_block_id,horizon_id,actual_propositions,actual_deltas,actual_invariants,provider_evidence_hashes,resolver_version,resolved_at)
            VALUES (@id,@action,@block,'locked-vector',@actual,@deltas,@invariants,@hashes,@resolver,clock_timestamp());
            """, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("action", state.ActionId); cmd.Parameters.AddWithValue("block", state.ConfigurationBlockId);
        AddJson(cmd, "actual", JsonSerializer.SerializeToElement(input.ActualPropositions, JsonDefaults.Options)); AddJson(cmd, "deltas", JsonSerializer.SerializeToElement(input.MaterialDeltas.ToDictionary(x => x, _ => true, StringComparer.Ordinal), JsonDefaults.Options));
        AddJson(cmd, "invariants", JsonSerializer.SerializeToElement(input.InvariantViolations.ToDictionary(x => x, _ => false, StringComparer.Ordinal), JsonDefaults.Options));
        AddJson(cmd, "hashes", JsonSerializer.SerializeToElement(new[] { localEvidence.ContentHash, providerEvidence.ContentHash }, JsonDefaults.Options)); cmd.Parameters.AddWithValue("resolver", input.ResolverVersion);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<EvidenceRecord> StoreObservationEvidenceAsync(ObservationEnvelope envelope, string method, CancellationToken ct)
    {
        var record = await _evidence.PutAsync(CanonicalJson.Canonicalize(envelope.Payload), envelope.ProviderNamespace, envelope.ObserverName,
            "application/json", method, envelope.CapturedAt, envelope.ProviderRevision, null, "utf-8",
            JsonSerializer.SerializeToElement(new { campaign = "build001-campaign-2" }, JsonDefaults.Options), ct).ConfigureAwait(false);
        await _kernel.InsertEvidenceAsync(record, ct).ConfigureAwait(false); return record;
    }

    private async Task<ObservationRecord> InsertObservationAsync(Guid target, ObservationEnvelope envelope, EvidenceRecord evidence, CancellationToken ct)
    {
        var record = new ObservationRecord(Guid.NewGuid(), target, envelope.ObserverName, envelope.ObserverVersion, envelope.ProviderNamespace,
            envelope.CapturedAt, envelope.AcquisitionStatus, JsonSerializer.SerializeToElement(new { scope = "campaign2-trial" }, JsonDefaults.Options),
            envelope.ProviderRevision, null, JsonSerializer.SerializeToElement(new { authority = "provider_native" }, JsonDefaults.Options), envelope.Payload, [evidence.EvidenceId]);
        await _kernel.InsertObservationAsync(record, ct).ConfigureAwait(false); return record;
    }

    private async Task EnsureManifestationAsync(Guid id, string provider, string kind, string identityRef, string incarnation, string? nativeId, string label, CancellationToken ct)
    {
        await _kernel.WithConnectionAsync(async (connection, inner) =>
        {
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO wk.manifestation(manifestation_id,provider_namespace,manifestation_kind,identity_basis,incarnation_key,provider_native_id,observer_native_ids,display_label)
                VALUES (@id,@provider,@kind,@basis,@incarnation,@native,'{}'::jsonb,@label) ON CONFLICT (manifestation_id) DO NOTHING;
                """, connection);
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("provider", provider); cmd.Parameters.AddWithValue("kind", kind);
            AddJson(cmd, "basis", JsonSerializer.SerializeToElement(new { reference = identityRef }, JsonDefaults.Options)); cmd.Parameters.AddWithValue("incarnation", incarnation);
            AddNullable(cmd, "native", NpgsqlDbType.Text, nativeId); cmd.Parameters.AddWithValue("label", label); await cmd.ExecuteNonQueryAsync(inner).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static async Task<Guid> InsertClaimAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid subject, string predicateNamespace, string predicate,
        JsonElement value, string productionMethod, string authorityClass, DateTimeOffset validFrom, Guid observation, Guid evidence, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO wk.claim(claim_id,subject_manifestation_id,predicate_namespace,predicate,value_json,production_method,authority_class,valid_range,scope,producer,confidence,freshness_policy_id,primary_observation_id,primary_evidence_id)
            VALUES (@id,@subject,@namespace,@predicate,@value,@method,@authority,tstzrange(@from,NULL,'[)'),'{}'::jsonb,@producer,1.0,'provider-reobserve-v1',@observation,@evidence);
            """, connection, tx);
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("subject", subject); cmd.Parameters.AddWithValue("namespace", predicateNamespace); cmd.Parameters.AddWithValue("predicate", predicate);
        AddJson(cmd, "value", value); cmd.Parameters.AddWithValue("method", productionMethod); cmd.Parameters.AddWithValue("authority", authorityClass); cmd.Parameters.AddWithValue("from", validFrom);
        AddJson(cmd, "producer", JsonSerializer.SerializeToElement(new { component = "campaign2-runner", version = "v1" }, JsonDefaults.Options)); cmd.Parameters.AddWithValue("observation", observation); cmd.Parameters.AddWithValue("evidence", evidence);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); await InsertPairAsync(connection, tx, "claim_observation", "claim_id", "observation_id", id, observation, ct).ConfigureAwait(false);
        await InsertPairAsync(connection, tx, "claim_evidence", "claim_id", "evidence_id", id, evidence, ct).ConfigureAwait(false); return id;
    }

    private static async Task InsertPairAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string table, string leftName, string rightName, Guid left, Guid right, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"INSERT INTO wk.{table}({leftName},{rightName}) VALUES (@left,@right);", connection, tx);
        cmd.Parameters.AddWithValue("left", left); cmd.Parameters.AddWithValue("right", right); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static Guid StableGuid(string text) => new(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16));
    private static string NormalizeArmForKernel(string arm) => arm switch
    {
        "conventional_memory" or "memory" => "memory", "cold" => "cold", "structured" => "structured", "acquisition" => "acquisition",
        "pilot" => "pilot", "drift" => "drift", "hostile" => "hostile", _ => throw new InvalidDataException("Unsupported kernel arm: " + arm)
    };
    private static string NormalizeArmForEvaluator(string arm) => arm switch
    {
        "conventional_memory" or "memory" => "memory", "cold" => "cold", "structured" => "structured", "pilot" => "pilot", "drift" => "drift",
        _ => throw new InvalidDataException("Unsupported evaluator arm: " + arm)
    };
    private static void ValidateSha256(string value, string name)
    {
        if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)) || value.Any(char.IsUpper)) throw new InvalidDataException(name + " is not a lowercase SHA-256.");
    }
    private static void AddJson(NpgsqlCommand command, string name, JsonElement value) => command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = value.GetRawText() });
    private static void AddNullable(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) => command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
    public async ValueTask DisposeAsync() { await _kernel.DisposeAsync().ConfigureAwait(false); await _evaluator.DisposeAsync().ConfigureAwait(false); }
}
