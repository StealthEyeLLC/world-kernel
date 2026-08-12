using System.Text.Json;
using Npgsql;

namespace StealthEye.WorldKernel.Build001;

public sealed record Campaign3LineageHarnessResult(
    string Schema,
    string SemanticAction,
    string TrialId,
    string WorkingCopy,
    string Branch,
    Guid LocalManifestationId,
    Guid RemoteManifestationId,
    Guid LocalObservationId,
    Guid RemoteObservationId,
    Guid LocalEvidenceId,
    Guid RemoteEvidenceId,
    IReadOnlyDictionary<string, Guid> ClaimIds,
    Guid CorrespondenceId,
    Guid ActionId,
    Guid PredictionId,
    Guid DispatchSealPhaseId,
    bool PredictionBeforeDispatch,
    bool RemoteClaimSubjectMatched,
    bool RemoteClaimProviderMatched,
    bool LocalClaimsSubjectMatched,
    int OutcomeCount,
    int PredictionEvaluationCount,
    int TransitionEpisodeCount,
    bool MaterialActionDispatched,
    DateTimeOffset CompletedAt);

public static partial class Campaign3Execution
{
    public static async Task<Campaign3LineageHarnessResult> RunPrefreezeLineageHarnessAsync(
        string secretFile,
        string evidenceRoot,
        string workingCopy,
        string stateObservationPath,
        string semanticAction,
        CancellationToken cancellationToken = default)
    {
        var stateBytes = await File.ReadAllBytesAsync(stateObservationPath, cancellationToken).ConfigureAwait(false);
        var state = Deserialize<Campaign3StateObservation>(stateBytes);
        _ = Build001Contract.ForAction(semanticAction);
        var trialId = $"c3-harness-{semanticAction.Replace(':', '-')}-{Guid.NewGuid():N}";
        var blockId = $"c3-harness-{Guid.NewGuid():N}";
        var generationId = Guid.NewGuid().ToString("D");
        var fingerprint = CanonicalJson.Sha256(stateBytes);
        var parameters = HarnessParameters(semanticAction, state);
        var input = new Campaign3BeginInput(
            BeginInputSchema, CampaignId, "hostile", trialId, blockId, "non-scientific-harness", "hostile",
            semanticAction, semanticAction.StartsWith("github:", StringComparison.Ordinal) ? FixtureRepository : workingCopy,
            parameters, workingCopy, state.Branch, state.Branch, string.Empty, stateObservationPath, string.Empty, string.Empty);

        var store = new EvidenceStore(evidenceRoot);
        var localEvidence = await store.PutAsync(
            stateBytes, "git/native", "campaign3-state-observer", "application/json",
            "prefreeze-lineage-harness-observation", state.ObservedAt, providerRevision: state.LocalHead,
            encoding: "utf-8", metadata: JsonSerializer.SerializeToElement(new
            {
                non_scientific_harness = true,
                semantic_action = semanticAction,
                campaign_id = CampaignId
            }, JsonDefaults.Options), cancellationToken: cancellationToken).ConfigureAwait(false);

        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        localEvidence = await EnsureEvidenceAsync(database, localEvidence, cancellationToken).ConfigureAwait(false);
        var localId = await EnsureLocalManifestationAsync(database, input, generationId, fingerprint, cancellationToken).ConfigureAwait(false);
        var remoteId = await EnsureRemoteManifestationAsync(database, cancellationToken).ConfigureAwait(false);
        var remote = await EnsureRemoteRefLineageAsync(database, store, remoteId, state, cancellationToken).ConfigureAwait(false);

        var localObservation = await EnsureObservationAsync(database, new ObservationRecord(
            Guid.NewGuid(), localId, "campaign3-state-observer", "campaign3-prefreeze-lineage-harness-v1", "git/native",
            state.ObservedAt, "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, local = true, non_scientific_harness = true }, JsonDefaults.Options),
            state.LocalHead, null,
            JsonSerializer.SerializeToElement(new { dependency_group = "native-git-local", non_scientific_harness = true }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(state, JsonDefaults.Options), [localEvidence.EvidenceId]), cancellationToken).ConfigureAwait(false);

        var claims = await InsertStateClaimsAsync(
            database, localId, remoteId, localObservation.ObservationId, localEvidence.EvidenceId, state,
            remote.Observation.ObservationId, remote.Evidence.EvidenceId, null, null, null, cancellationToken).ConfigureAwait(false);
        var correspondenceId = await InsertCorrespondenceAsync(
            database, localId, remoteId,
            [localObservation.ObservationId, remote.Observation.ObservationId],
            [localEvidence.EvidenceId, remote.Evidence.EvidenceId], claims.Values,
            CanonicalJson.Sha256Utf8("campaign3-prefreeze-lineage-harness|" + trialId), cancellationToken).ConfigureAwait(false);

        var producer = JsonSerializer.SerializeToElement(new
        {
            mechanism = "campaign3-prefreeze-lineage-harness",
            non_scientific_harness = true,
            scored = false,
            material_dispatch_permitted = false
        }, JsonDefaults.Options);
        var actionId = Guid.NewGuid();
        await database.DeclareActionAsync(new ActionDeclaration(
            actionId, trialId, blockId, "hostile", [localId, remoteId],
            semanticAction.StartsWith("github:", StringComparison.Ordinal) ? "eyeBROWSE" : "experiment-git-facet",
            semanticAction, "build001-v1", semanticAction, parameters, producer, FixtureRepository), cancellationToken).ConfigureAwait(false);

        var predictionId = Guid.NewGuid();
        var probabilities = Build001Contract.ForAction(semanticAction).ToDictionary(key => key, _ => (double?)0.5, StringComparer.Ordinal);
        var defects = await database.CommitPredictionAsync(new PredictionDeclaration(
            predictionId, actionId, semanticAction, probabilities, JsonDefaults.EmptyObject, JsonDefaults.EmptyObject,
            Build001Contract.DefaultHorizons(), "campaign3-prefreeze-lineage-harness", "campaign3-prefreeze-lineage-harness-v1", producer),
            cancellationToken).ConfigureAwait(false);
        if (defects.Count != 0)
            throw new InvalidDataException("Prefreeze lineage harness Prediction acquired defects: " + string.Join("; ", defects));
        await InsertPredictionLineageAsync(database, actionId, predictionId,
            [localObservation.ObservationId, remote.Observation.ObservationId],
            [localEvidence.EvidenceId, remote.Evidence.EvidenceId], cancellationToken).ConfigureAwait(false);
        var dispatchSealPhaseId = await database.SealDispatchAsync(actionId, parameters,
            JsonSerializer.SerializeToElement(new
            {
                non_scientific_harness = true,
                prediction_recorded_before_dispatch = true,
                material_dispatch_permitted = false
            }, JsonDefaults.Options), cancellationToken).ConfigureAwait(false);

        return await database.WithConnectionAsync(async (connection, token) =>
        {
            var remoteClaimId = claims["github:remote_ref_head"];
            bool remoteSubjectMatched;
            bool remoteProviderMatched;
            await using (var command = new NpgsqlCommand("""
                SELECT c.subject_manifestation_id=o.target_manifestation_id,
                       c.subject_manifestation_id=@remote,
                       o.provider_namespace='github/provider',
                       e.provider_namespace='github/provider',
                       o.acquisition_status='succeeded',
                       e.acquisition_method='git-ls-remote-exact-hosted-ref'
                FROM wk.claim c
                JOIN wk.observation o ON o.observation_id=c.primary_observation_id
                JOIN wk.evidence e ON e.evidence_id=c.primary_evidence_id
                WHERE c.claim_id=@claim;
                """, connection))
            {
                command.Parameters.AddWithValue("remote", remoteId);
                command.Parameters.AddWithValue("claim", remoteClaimId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("Harness remote Claim disappeared.");
                remoteSubjectMatched = reader.GetBoolean(0) && reader.GetBoolean(1);
                remoteProviderMatched = reader.GetBoolean(2) && reader.GetBoolean(3) && reader.GetBoolean(4) && reader.GetBoolean(5);
            }

            await using var localCheck = new NpgsqlCommand("""
                SELECT count(*) FILTER (WHERE c.subject_manifestation_id<>@local OR o.target_manifestation_id<>@local OR
                    o.provider_namespace NOT IN ('codeeye/git-local','git/native') OR
                    e.provider_namespace NOT IN ('codeeye/git-local','git/native'))
                FROM wk.claim c
                JOIN wk.observation o ON o.observation_id=c.primary_observation_id
                JOIN wk.evidence e ON e.evidence_id=c.primary_evidence_id
                WHERE c.claim_id = ANY(@claims) AND c.subject_manifestation_id=@local;
                """, connection);
            localCheck.Parameters.AddWithValue("local", localId);
            localCheck.Parameters.AddWithValue("claims", claims.Values.ToArray());
            var localMismatches = Convert.ToInt32(await localCheck.ExecuteScalarAsync(token).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

            await using var lifecycle = new NpgsqlCommand("""
                SELECT
                  (SELECT p.recorded_at <= ap.recorded_at FROM wk.prediction p JOIN wk.action_phase ap ON ap.action_id=p.action_id AND ap.phase='dispatched' WHERE p.prediction_id=@prediction),
                  (SELECT count(*) FROM wk.outcome WHERE action_id=@action),
                  (SELECT count(*) FROM wk.prediction_evaluation pe JOIN wk.outcome o ON o.outcome_id=pe.outcome_id WHERE o.action_id=@action),
                  (SELECT count(*) FROM wk.transition_episode WHERE action_id=@action);
                """, connection);
            lifecycle.Parameters.AddWithValue("prediction", predictionId);
            lifecycle.Parameters.AddWithValue("action", actionId);
            await using var lifecycleReader = await lifecycle.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await lifecycleReader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("Harness lifecycle audit returned no row.");
            var predictionBeforeDispatch = lifecycleReader.GetBoolean(0);
            var outcomeCount = Convert.ToInt32(lifecycleReader.GetInt64(1));
            var evaluationCount = Convert.ToInt32(lifecycleReader.GetInt64(2));
            var episodeCount = Convert.ToInt32(lifecycleReader.GetInt64(3));

            if (!remoteSubjectMatched || !remoteProviderMatched || localMismatches != 0 || !predictionBeforeDispatch ||
                outcomeCount != 0 || evaluationCount != 0 || episodeCount != 0)
                throw new InvalidDataException("Prefreeze lineage harness integrity assertion failed.");

            return new Campaign3LineageHarnessResult(
                "world-kernel-build001-campaign3-prefreeze-lineage-harness-v1", semanticAction, trialId,
                Path.GetFullPath(workingCopy), state.Branch, localId, remoteId, localObservation.ObservationId,
                remote.Observation.ObservationId, localEvidence.EvidenceId, remote.Evidence.EvidenceId, claims,
                correspondenceId, actionId, predictionId, dispatchSealPhaseId, predictionBeforeDispatch,
                remoteSubjectMatched, remoteProviderMatched, true, outcomeCount, evaluationCount, episodeCount,
                false, DateTimeOffset.UtcNow);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static JsonElement HarnessParameters(string semanticAction, Campaign3StateObservation state) => semanticAction switch
    {
        "git:create_local_commit" => JsonSerializer.SerializeToElement(new { relative_path = "fixture/state.txt", message = "Campaign 3 prefreeze harness local commit", timestamp = DateTimeOffset.UtcNow }),
        "git:create_branch" => JsonSerializer.SerializeToElement(new { branch = "wk-b001-c3-harness-new" }),
        "git:push_ref" => JsonSerializer.SerializeToElement(new { branch = state.Branch }),
        "github:create_remote_commit" => JsonSerializer.SerializeToElement(new { branch = state.Branch, file = "fixture/state.txt", text = "campaign3-prefreeze-harness", message = "Campaign 3 prefreeze harness remote commit" }),
        "git:fetch_remote" => JsonDefaults.EmptyObject,
        "git:integrate_fast_forward" => JsonSerializer.SerializeToElement(new { branch = state.Branch }),
        _ => throw new ArgumentOutOfRangeException(nameof(semanticAction))
    };
}