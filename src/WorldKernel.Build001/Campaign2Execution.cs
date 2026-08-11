using System.Data;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace StealthEye.WorldKernel.Build001;

public sealed record Campaign2StateObservation(
    string Schema,
    DateTimeOffset ObservedAt,
    string Branch,
    string LocalHead,
    string CurrentBranch,
    string LocalTree,
    string WorktreeContentSha256,
    bool WorktreeClean,
    IReadOnlyList<string> LocalBranches,
    string? RemoteHead,
    string? RemoteTrackingHead,
    bool RemoteHeadReachableLocally,
    int LocalHeadParentCount,
    string RemoteUrl,
    string PublicTopologyClass,
    JsonElement Commands);

public sealed record Campaign2CheckOutcome(
    bool Observed,
    bool Started,
    bool TerminalSuccess,
    string? Conclusion,
    JsonElement Runs);

public sealed record Campaign2BrowserOutcome(
    bool Observed,
    string? PresentedHead,
    string? Href,
    JsonElement Evidence);

public sealed record Campaign2ProviderOutcome(
    string Schema,
    DateTimeOffset ObservedAt,
    Campaign2CheckOutcome Check,
    Campaign2BrowserOutcome Browser);

public sealed record Campaign2ResolvedOutcome(
    IReadOnlyDictionary<string, bool?> ActualPropositions,
    IReadOnlyList<string> ActualDeltas,
    IReadOnlyList<string> ViolatedInvariants);

public static class Campaign2OutcomeResolver
{
    public const string StateSchema = "world-kernel-build001-campaign2-state-observation-v1";
    public const string ProviderSchema = "world-kernel-build001-campaign2-provider-outcome-v1";

    public static Campaign2ResolvedOutcome Resolve(
        string semanticAction,
        Campaign2StateObservation before,
        Campaign2StateObservation after,
        bool receiptAccepted,
        Campaign2ProviderOutcome provider)
    {
        _ = Build001Contract.ForAction(semanticAction);
        if (before.Schema != StateSchema || after.Schema != StateSchema)
        {
            throw new InvalidDataException("Campaign 2 state observation schema mismatch.");
        }
        if (provider.Schema != ProviderSchema)
        {
            throw new InvalidDataException("Campaign 2 provider outcome schema mismatch.");
        }
        if (!string.Equals(before.Branch, after.Branch, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Pre/post observations do not address the same disposable branch.");
        }

        var localHeadChanged = before.LocalHead != after.LocalHead;
        var remoteHeadChanged = before.RemoteHead != after.RemoteHead;
        var worktreeChanged = before.WorktreeContentSha256 != after.WorktreeContentSha256;
        var trackingChanged = before.RemoteTrackingHead != after.RemoteTrackingHead;
        var branchChanged = before.CurrentBranch != after.CurrentBranch;
        var browserMatchesRemote = provider.Browser.Observed &&
                                   ShaMatches(provider.Browser.PresentedHead, after.RemoteHead);

        var actual = semanticAction switch
        {
            "git:create_local_commit" => Map(
                ("provider_accepts_action", receiptAccepted),
                ("local_head_changes", localHeadChanged),
                ("local_head_equals_new_commit", receiptAccepted && localHeadChanged),
                ("remote_target_ref_changes_before_push", remoteHeadChanged),
                ("local_worktree_clean_after", after.WorktreeClean),
                ("current_branch_name_changes", branchChanged),
                ("new_commit_reachable_locally", receiptAccepted && localHeadChanged),
                ("new_commit_reachable_remotely_before_push", receiptAccepted &&
                    ShaMatches(after.LocalHead, after.RemoteHead))),
            "git:create_branch" => Map(
                ("provider_accepts_action", receiptAccepted),
                ("new_local_branch_exists", after.LocalBranches.Contains(after.CurrentBranch, StringComparer.Ordinal) && branchChanged),
                ("current_branch_is_new_branch", branchChanged && after.CurrentBranch != before.CurrentBranch),
                ("local_head_sha_changes", localHeadChanged),
                ("remote_branch_exists_before_push", after.RemoteHead is not null),
                ("worktree_content_changes", worktreeChanged)),
            "git:push_ref" => Map(
                ("provider_accepts_push", receiptAccepted),
                ("remote_ref_exists_at_H1", after.RemoteHead is not null),
                ("remote_ref_equals_local_head_at_H1", ShaMatches(after.RemoteHead, after.LocalHead)),
                ("local_head_changes_because_of_push", localHeadChanged),
                ("local_worktree_changes_because_of_push", worktreeChanged),
                ("remote_check_starts_by_H2", provider.Check.Started),
                ("remote_check_terminal_success_by_H3", provider.Check.TerminalSuccess),
                ("browser_presentation_reflects_new_remote_head_by_H1", remoteHeadChanged && browserMatchesRemote)),
            "github:create_remote_commit" => Map(
                ("provider_accepts_action", receiptAccepted && remoteHeadChanged),
                ("remote_head_changes", remoteHeadChanged),
                ("remote_head_equals_new_hosted_commit", receiptAccepted && remoteHeadChanged),
                ("local_head_changes_before_fetch", localHeadChanged),
                ("local_worktree_changes_before_fetch", worktreeChanged),
                ("local_remote_tracking_ref_changes_before_fetch", trackingChanged),
                ("new_remote_commit_reachable_locally_before_fetch", after.RemoteHeadReachableLocally),
                ("browser_presentation_reflects_new_remote_commit_by_H1", browserMatchesRemote)),
            "git:fetch_remote" => Map(
                ("provider_accepts_action", receiptAccepted),
                ("local_head_changes", localHeadChanged),
                ("local_worktree_changes", worktreeChanged),
                ("remote_tracking_ref_equals_remote_head_at_H1", ShaMatches(after.RemoteTrackingHead, after.RemoteHead)),
                ("remote_head_changes_because_of_fetch", remoteHeadChanged),
                ("remote_commit_reachable_locally_after_fetch", after.RemoteHeadReachableLocally),
                ("checked_out_branch_content_changes", worktreeChanged)),
            "git:integrate_fast_forward" => Map(
                ("fast_forward_is_accepted", receiptAccepted),
                ("local_head_equals_remote_target_after_H1", ShaMatches(after.LocalHead, after.RemoteHead)),
                ("local_head_changes", localHeadChanged),
                ("local_worktree_content_changes", worktreeChanged),
                ("local_worktree_clean_after", after.WorktreeClean),
                ("remote_head_changes_because_of_integration", remoteHeadChanged),
                ("merge_commit_created", localHeadChanged && after.LocalHeadParentCount > 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(semanticAction))
        };

        var deltas = new List<string>();
        if (localHeadChanged) deltas.Add("local_head_changed");
        if (remoteHeadChanged) deltas.Add("remote_ref_changed");
        if (worktreeChanged) deltas.Add("working_tree_changed");
        if (trackingChanged) deltas.Add("remote_tracking_ref_changed");
        if (branchChanged) deltas.Add("branch_created");
        if (provider.Check.Started) deltas.Add("check_started");
        if (provider.Check.TerminalSuccess) deltas.Add("check_terminal_success");
        return new Campaign2ResolvedOutcome(actual, deltas, Array.Empty<string>());
    }

    private static IReadOnlyDictionary<string, bool?> Map(params (string Key, bool Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => (bool?)entry.Value, StringComparer.Ordinal);

    private static bool ShaMatches(string? left, string? right) =>
        left is not null && right is not null &&
        (left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
         right.StartsWith(left, StringComparison.OrdinalIgnoreCase));
}

public sealed record Campaign2BeginInput(
    string Schema,
    string CampaignId,
    string Phase,
    string TrialId,
    string ConfigurationBlockId,
    string Arm,
    string SemanticAction,
    string Target,
    JsonElement Parameters,
    string WorkingCopy,
    string ResetBranch,
    string Branch,
    string ResetManifestPath,
    string PreObservationPath,
    string SubjectRequestPath,
    string SubjectResultPath);

public sealed record Campaign2AcquisitionBlockRegistrationInput(
    string Schema,
    string CampaignId,
    string Phase,
    string ConfigurationBlockId,
    string SeedId,
    string CommitmentSha256,
    string SealedPayloadRef,
    string PublicFixtureRevision,
    JsonElement HiddenConfiguration,
    string ExpectedConfigurationFingerprint);

public sealed record Campaign2AcquisitionBlockRegistrationRecord(
    string Schema,
    string CampaignId,
    string ConfigurationBlockId,
    string SeedId,
    string CommitmentSha256,
    string ExpectedConfigurationFingerprint,
    DateTimeOffset RegisteredAt);

public sealed record Campaign2ResetRegistrationRecord(
    string Schema,
    string CampaignId,
    string ConfigurationBlockId,
    string SeedId,
    Guid GenerationId,
    string ActualFingerprint,
    string ResetManifestSha256,
    bool Passed,
    DateTimeOffset RegisteredAt);
public sealed record Campaign2BeginRecord(
    string Schema,
    string CampaignId,
    string Phase,
    string TrialId,
    string ConfigurationBlockId,
    string Arm,
    string SemanticAction,
    string Target,
    JsonElement Parameters,
    string WorkingCopy,
    string ResetBranch,
    string Branch,
    string EnvironmentFingerprint,
    string SeedCommitmentSha256,
    string FreezeManifestSha256,
    Guid LocalManifestationId,
    Guid RemoteManifestationId,
    Guid CorrespondenceId,
    Guid PreObservationId,
    Guid PreEvidenceId,
    IReadOnlyDictionary<string, Guid> PreClaimIds,
    string ResetManifestSha256,
    Guid RequestEvidenceId,
    Guid SubjectEvidenceId,
    Guid ActionId,
    Guid PredictionId,
    Guid DispatchPhaseId,
    DateTimeOffset DispatchedAt,
    string ResetManifestPath,
    string PreObservationPath,
    string SubjectRequestPath,
    string SubjectResultPath,
    string SubjectResultSha256,
    DateTimeOffset RecordedAt);

public sealed record Campaign2CloseRecord(
    string Schema,
    string CampaignId,
    string TrialId,
    string ConfigurationBlockId,
    string SemanticAction,
    Guid ActionId,
    Guid PredictionId,
    Guid OutcomeId,
    Guid EvaluationId,
    Guid EpisodeId,
    string EligibilityStatus,
    double MeanBrierLoss,
    IReadOnlyDictionary<string, bool?> ActualPropositions,
    IReadOnlyDictionary<string, double?> BrierComponents,
    IReadOnlyList<string> ActualDeltas,
    string ReceiptSha256,
    string PostObservationSha256,
    string ProviderOutcomeSha256,
    string PublicEpisodePath,
    string PublicEpisodeSha256,
    DateTimeOffset ClosedAt);

public sealed record Campaign2ActionCoverage(
    string SemanticAction,
    int EligibleClosedEpisodes,
    int DistinctConfigurationBlocks,
    int DistinctSeeds);

public sealed record Campaign2AcquisitionCoverage(
    string Schema,
    string CampaignId,
    DateTimeOffset GeneratedAt,
    int ConfigurationBlocks,
    IReadOnlyList<Campaign2ActionCoverage> Actions,
    int PushAccepted,
    int PushRejected,
    int FastForwardAccepted,
    int FastForwardRejected,
    int CheckStartedTrue,
    int CheckStartedFalse,
    bool MinimumTwentyEachAction,
    bool PushBalance,
    bool FastForwardBalance,
    bool CheckBalance,
    bool SixSeedsEachAction,
    bool InitialTwentyFourBlocks,
    bool StopRuleSatisfied);

public static class Campaign2Execution
{
    public const string CampaignId = "build001-campaign-2";
public const string BlockRegistrationInputSchema = "world-kernel-build001-campaign2-block-registration-input-v1";
    public const string BlockRegistrationRecordSchema = "world-kernel-build001-campaign2-block-registration-v1";
    public const string ResetRegistrationRecordSchema = "world-kernel-build001-campaign2-reset-registration-v1";
    public const string BeginInputSchema = "world-kernel-build001-campaign2-begin-input-v1";
    public const string BeginRecordSchema = "world-kernel-build001-campaign2-begin-record-v1";
    public const string CloseRecordSchema = "world-kernel-build001-campaign2-close-record-v1";
    public const string CoverageSchema = "world-kernel-build001-campaign2-acquisition-coverage-v1";
    public const string FreezeManifestSchema = "world-kernel-build001-campaign2-execution-freeze-v2";
    public const string SubjectResultSchema = "world-kernel-build001-campaign2-subject-adapter-result-v1";
    private const string FixtureRepository = "StealthEyeLLC/world-kernel-build-001-fixture";
    private const string FixtureNativeId = "1330898503";
    private static readonly string FixtureManifestationRef = $"github:repo:{FixtureRepository}#{FixtureNativeId}";
    private static readonly string ProviderVersionFingerprint = CanonicalJson.Sha256Utf8(
        $"github|{FixtureRepository}|{FixtureNativeId}|build001-provider-surface-v1");

    public static async Task<Campaign2AcquisitionBlockRegistrationRecord> RegisterAcquisitionBlockAsync(
        string repositoryRoot,
        string secretFile,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        ValidateFreeze(root);
        PreflightGateEvaluator.EnsurePhaseAuthorized(
            Path.Combine(root, "artifacts", "campaign-2", "preflight", "preflight-gates.json"),
            "acquisition");
        var inputFile = Path.GetFullPath(inputPath);
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (inputFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hidden acquisition registration input must remain outside the repository tree.");
        }
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2");
        var outputFile = EnsureUnder(campaignRoot, outputPath);
        EnsureAbsent(outputFile);
        var input = Deserialize<Campaign2AcquisitionBlockRegistrationInput>(
            await File.ReadAllBytesAsync(inputFile, cancellationToken).ConfigureAwait(false));
        if (input.Schema != BlockRegistrationInputSchema || input.CampaignId != CampaignId || input.Phase != "acquisition" ||
            string.IsNullOrWhiteSpace(input.ConfigurationBlockId) || string.IsNullOrWhiteSpace(input.SeedId) ||
            input.HiddenConfiguration.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Campaign 2 acquisition block registration is invalid.");
        }
        var hiddenHash = CanonicalJson.HashJson(input.HiddenConfiguration);
        if ((!string.IsNullOrEmpty(input.CommitmentSha256) && input.CommitmentSha256 != hiddenHash) ||
            (!string.IsNullOrEmpty(input.ExpectedConfigurationFingerprint) && input.ExpectedConfigurationFingerprint != hiddenHash) ||
            input.PublicFixtureRevision.Length != 40)
        {
            throw new InvalidDataException("Acquisition hidden configuration commitment does not match the prospective payload.");
        }
        if (Path.GetFullPath(input.SealedPayloadRef) != inputFile)
        {
            throw new InvalidDataException("Sealed acquisition payload reference does not point to the registered hidden input.");
        }

        await using var source = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO eval001.seed_commitment(seed_id,phase,configuration_block_id,commitment_sha256,sealed_payload_ref,public_fixture_revision)
            VALUES (@seed,'acquisition',@block,@commitment,@ref,@revision);
            """, connection, transaction))
        {
            seed.Parameters.AddWithValue("seed", input.SeedId);
            seed.Parameters.AddWithValue("block", input.ConfigurationBlockId);
            seed.Parameters.AddWithValue("commitment", hiddenHash);
            seed.Parameters.AddWithValue("ref", input.SealedPayloadRef);
            seed.Parameters.AddWithValue("revision", input.PublicFixtureRevision);
            await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var hidden = new NpgsqlCommand("""
            INSERT INTO eval001.hidden_configuration(hidden_configuration_id,seed_id,regime_label,configuration,expected_reset_fingerprint,answer_key_version)
            VALUES (@id,@seed,'acquisition-scheduled',@configuration,@fingerprint,'campaign2-acquisition-schedule-v1');
            """, connection, transaction))
        {
            hidden.Parameters.AddWithValue("id", Guid.NewGuid());
            hidden.Parameters.AddWithValue("seed", input.SeedId);
            KernelDb.AddJson(hidden, "configuration", input.HiddenConfiguration);
            hidden.Parameters.AddWithValue("fingerprint", hiddenHash);
            await hidden.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var record = new Campaign2AcquisitionBlockRegistrationRecord(
            BlockRegistrationRecordSchema, CampaignId, input.ConfigurationBlockId, input.SeedId,
            hiddenHash, hiddenHash, DateTimeOffset.UtcNow);
        await WriteNewAsync(outputFile, CanonicalJson.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record;
    }

    public static async Task<Campaign2ResetRegistrationRecord> RegisterAcquisitionResetAsync(
        string repositoryRoot,
        string secretFile,
        string configurationBlockId,
        string seedId,
        string resetManifestPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        ValidateFreeze(root);
        PreflightGateEvaluator.EnsurePhaseAuthorized(
            Path.Combine(root, "artifacts", "campaign-2", "preflight", "preflight-gates.json"),
            "acquisition");
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2");
        var resetFile = EnsureUnder(campaignRoot, resetManifestPath);
        var outputFile = EnsureUnder(campaignRoot, outputPath);
        EnsureAbsent(outputFile);
        var resetBytes = await File.ReadAllBytesAsync(resetFile, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(resetBytes);
        var reset = document.RootElement;
        if (RequiredString(reset, "reset_version") != "build001-fixture-reset-v1" ||
            RequiredString(reset, "phase") != "acquisition" || RequiredString(reset, "arm") != "acquisition" ||
            !RequiredBoolean(reset, "reset_verified"))
        {
            throw new InvalidDataException("Acquisition reset registration rejected an invalid reset manifest.");
        }
        var generationId = Guid.Parse(RequiredString(reset, "generation_id"));
        var fingerprint = RequiredString(reset, "actual_fingerprint");
        if (fingerprint.Length != 64) throw new InvalidDataException("Reset fingerprint is invalid.");
        var manifestHash = CanonicalJson.Sha256(resetBytes);
        await using var source = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var verify = new NpgsqlCommand("""
            SELECT count(*) FROM eval001.seed_commitment
            WHERE seed_id=@seed AND phase='acquisition' AND configuration_block_id=@block;
            """, connection))
        {
            verify.Parameters.AddWithValue("seed", seedId);
            verify.Parameters.AddWithValue("block", configurationBlockId);
            if (Convert.ToInt64(await verify.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 1)
                throw new InvalidDataException("Acquisition reset has no prospective seed commitment for its configuration block.");
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO eval001.reset_verification(reset_verification_id,seed_id,arm,generation_id,actual_fingerprint,expected_fingerprint,provider_evidence_hashes,passed)
            VALUES (@id,@seed,'acquisition',@generation,@actual,@expected,@hashes,true);
            """, connection))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("seed", seedId);
            insert.Parameters.AddWithValue("generation", generationId);
            insert.Parameters.AddWithValue("actual", fingerprint);
            insert.Parameters.AddWithValue("expected", fingerprint);
            KernelDb.AddJson(insert, "hashes", JsonSerializer.SerializeToElement(new[] { manifestHash }, JsonDefaults.Options));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var record = new Campaign2ResetRegistrationRecord(
            ResetRegistrationRecordSchema, CampaignId, configurationBlockId, seedId, generationId,
            fingerprint, manifestHash, true, DateTimeOffset.UtcNow);
        await WriteNewAsync(outputFile, CanonicalJson.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record;
    }
    public static async Task<Campaign2BeginRecord> BeginAsync(
        string repositoryRoot,
        string secretFile,
        string evidenceRoot,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        ValidateFreeze(root);
        PreflightGateEvaluator.EnsurePhaseAuthorized(
            Path.Combine(root, "artifacts", "campaign-2", "preflight", "preflight-gates.json"),
            "acquisition");
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2");
        var inputFile = EnsureUnder(campaignRoot, inputPath);
        var outputFile = EnsureUnder(campaignRoot, outputPath);
        EnsureAbsent(outputFile);
        var input = Deserialize<Campaign2BeginInput>(await File.ReadAllBytesAsync(inputFile, cancellationToken).ConfigureAwait(false));
        ValidateBeginInput(input);

        var resetFile = EnsureUnder(campaignRoot, input.ResetManifestPath);
        var preFile = EnsureUnder(campaignRoot, input.PreObservationPath);
        var requestFile = EnsureUnder(campaignRoot, input.SubjectRequestPath);
        var subjectFile = EnsureUnder(campaignRoot, input.SubjectResultPath);
        var resetBytes = await File.ReadAllBytesAsync(resetFile, cancellationToken).ConfigureAwait(false);
        var preBytes = await File.ReadAllBytesAsync(preFile, cancellationToken).ConfigureAwait(false);
        var requestBytes = await File.ReadAllBytesAsync(requestFile, cancellationToken).ConfigureAwait(false);
        var subjectBytes = await File.ReadAllBytesAsync(subjectFile, cancellationToken).ConfigureAwait(false);
        using var resetDocument = JsonDocument.Parse(resetBytes);
        using var requestDocument = JsonDocument.Parse(requestBytes);
        using var subjectDocument = JsonDocument.Parse(subjectBytes);
        var reset = resetDocument.RootElement;
        var request = requestDocument.RootElement;
        var subject = subjectDocument.RootElement;
        var pre = Deserialize<Campaign2StateObservation>(preBytes);
        ValidateReset(input, reset, pre);
        await EnsureEvaluatorReadyAsync(secretFile, input, reset, cancellationToken).ConfigureAwait(false);
        var prediction = ValidateSubject(input, requestBytes, request, subject);

        var store = new EvidenceStore(evidenceRoot);
        var resetAt = RequiredDateTime(reset, "reset_at");
        var requestAt = RequiredDateTime(request, "generated_at");
        var subjectAt = RequiredDateTime(subject, "completed_at");
        var resetManifestSha256 = CanonicalJson.Sha256(resetBytes);
        var preEvidence = await store.PutAsync(preBytes, "git/native", "campaign2-state-observer",
            "application/json", "fresh-pre-dispatch-observation", pre.ObservedAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);
        var requestEvidence = await store.PutAsync(requestBytes, "chatgpt/product", "campaign2-request-builder",
            "application/json", "locked-subject-request", requestAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);
        var subjectEvidence = await store.PutAsync(subjectBytes, "chatgpt/product", "campaign2-subject-adapter",
            "application/json", "fresh-temporary-chat", subjectAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);

        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        foreach (var evidence in new[] { preEvidence, requestEvidence, subjectEvidence })
        {
            await database.InsertEvidenceAsync(evidence, cancellationToken).ConfigureAwait(false);
        }

        var localId = Guid.NewGuid();
        var generationId = RequiredString(reset, "generation_id");
        var fingerprint = RequiredString(reset, "actual_fingerprint");
        var seed = RequiredString(reset, "seed_commitment_sha256");
        var local = new ManifestationRecord(
            localId,
            "codeeye/git-local",
            "git-working-copy",
            JsonSerializer.SerializeToElement(new
            {
                fixture_repository = FixtureRepository,
                working_copy = Path.GetFullPath(input.WorkingCopy),
                generation_id = generationId,
                environment_fingerprint = fingerprint
            }, JsonDefaults.Options),
            $"campaign2:{input.TrialId}:{generationId}",
            null,
            JsonSerializer.SerializeToElement(new { reset_generation_id = generationId }, JsonDefaults.Options),
            input.WorkingCopy);
        await database.InsertManifestationAsync(local, cancellationToken).ConfigureAwait(false);
        var remoteId = await EnsureRemoteManifestationAsync(database, cancellationToken).ConfigureAwait(false);

        var preObservationId = Guid.NewGuid();
        var preObservation = new ObservationRecord(
            preObservationId,
            localId,
            "campaign2-state-observer",
            "campaign2-state-observation-v1",
            "git/native",
            pre.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, local = true, remote = true }, JsonDefaults.Options),
            pre.RemoteHead,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "native-git-provider-reset" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(pre, JsonDefaults.Options),
            [preEvidence.EvidenceId]);
        await database.InsertObservationAsync(preObservation, cancellationToken).ConfigureAwait(false);
        var preClaims = await InsertStateClaimsAsync(
            database, localId, remoteId, preObservationId, preEvidence.EvidenceId, pre, null, null, null, cancellationToken).ConfigureAwait(false);

        var correspondenceId = await InsertCorrespondenceAsync(
            database, localId, remoteId, preObservationId, preEvidence.EvidenceId, preClaims.Values,
            resetManifestSha256, cancellationToken).ConfigureAwait(false);

        var actionId = Guid.NewGuid();
        var producer = JsonSerializer.SerializeToElement(new
        {
            product = "ChatGPT web",
            selected_model = "5.6 Sol",
            reasoning_selection = "Extra High",
            temporary_chat = true,
            subject_result_sha256 = CanonicalJson.Sha256(subjectBytes),
            request_sha256 = CanonicalJson.Sha256(requestBytes)
        }, JsonDefaults.Options);
        var action = new ActionDeclaration(
            actionId,
            input.TrialId,
            input.ConfigurationBlockId,
            input.Arm,
            [localId, remoteId],
            input.SemanticAction.StartsWith("github:", StringComparison.Ordinal) ? "eyeBROWSE" : "experiment-git-facet",
            input.SemanticAction,
            "build001-v1",
            input.SemanticAction,
            input.Parameters,
            producer,
            FixtureRepository);
        await database.DeclareActionAsync(action, cancellationToken).ConfigureAwait(false);

        var predictionId = Guid.NewGuid();
        var predictionDeclaration = new PredictionDeclaration(
            predictionId,
            actionId,
            input.SemanticAction,
            prediction,
            JsonDefaults.EmptyArray,
            JsonDefaults.EmptyArray,
            Build001Contract.DefaultHorizons(),
            "fresh-campaign2-temporary-chat",
            Campaign2Attestation.FreshInvocationMethodVersion,
            producer);
        var defects = await database.CommitPredictionAsync(predictionDeclaration, cancellationToken).ConfigureAwait(false);
        if (defects.Count != 0)
        {
            throw new InvalidDataException("Validated Campaign 2 subject prediction acquired format defects: " + string.Join("; ", defects));
        }
        await InsertPredictionLineageAsync(
            database, actionId, predictionId, preObservationId,
            [preEvidence.EvidenceId, requestEvidence.EvidenceId, subjectEvidence.EvidenceId], cancellationToken).ConfigureAwait(false);

        var dispatchPhaseId = await database.SealDispatchAsync(
            actionId,
            input.Parameters,
            JsonSerializer.SerializeToElement(new
            {
                campaign_id = CampaignId,
                trial_id = input.TrialId,
                subject_result_sha256 = CanonicalJson.Sha256(subjectBytes),
                request_sha256 = CanonicalJson.Sha256(requestBytes),
                prediction_recorded_before_dispatch = true
            }, JsonDefaults.Options),
            cancellationToken).ConfigureAwait(false);
        var dispatchedAt = await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("SELECT recorded_at FROM wk.action_phase WHERE action_phase_id=@id;", connection);
            command.Parameters.AddWithValue("id", dispatchPhaseId);
            var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return value is DateTimeOffset dto ? dto : new DateTimeOffset(DateTime.SpecifyKind((DateTime)value!, DateTimeKind.Utc));
        }, cancellationToken).ConfigureAwait(false);

        var record = new Campaign2BeginRecord(
            BeginRecordSchema,
            CampaignId,
            input.Phase,
            input.TrialId,
            input.ConfigurationBlockId,
            input.Arm,
            input.SemanticAction,
            input.Target,
            input.Parameters,
            input.WorkingCopy,
            input.ResetBranch,
            input.Branch,
            fingerprint,
            seed,
            GetFreezeManifestSha256(root),
            localId,
            remoteId,
            correspondenceId,
            preObservationId,
            preEvidence.EvidenceId,
            preClaims,
            resetManifestSha256,
            requestEvidence.EvidenceId,
            subjectEvidence.EvidenceId,
            actionId,
            predictionId,
            dispatchPhaseId,
            dispatchedAt,
            resetFile,
            preFile,
            requestFile,
            subjectFile,
            CanonicalJson.Sha256(subjectBytes),
            DateTimeOffset.UtcNow);
        await WriteNewAsync(outputFile, CanonicalJson.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record;
    }

    public static async Task<Campaign2CloseRecord> CloseAsync(
        string repositoryRoot,
        string secretFile,
        string evidenceRoot,
        string beginPath,
        string receiptPath,
        string postObservationPath,
        string providerOutcomePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        ValidateFreeze(root);
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2");
        var beginFile = EnsureUnder(campaignRoot, beginPath);
        var receiptFile = EnsureUnder(campaignRoot, receiptPath);
        var postFile = EnsureUnder(campaignRoot, postObservationPath);
        var providerFile = EnsureUnder(campaignRoot, providerOutcomePath);
        var outputFile = EnsureUnder(campaignRoot, outputPath);
        EnsureAbsent(outputFile);
        var begin = Deserialize<Campaign2BeginRecord>(await File.ReadAllBytesAsync(beginFile, cancellationToken).ConfigureAwait(false));
        if (begin.Schema != BeginRecordSchema || begin.CampaignId != CampaignId || begin.Phase != "acquisition" || begin.Arm != "acquisition")
        {
            throw new InvalidDataException("Campaign 2 begin record is not an acquisition dispatch.");
        }
        var before = Deserialize<Campaign2StateObservation>(await File.ReadAllBytesAsync(begin.PreObservationPath, cancellationToken).ConfigureAwait(false));
        var receiptBytes = await File.ReadAllBytesAsync(receiptFile, cancellationToken).ConfigureAwait(false);
        var postBytes = await File.ReadAllBytesAsync(postFile, cancellationToken).ConfigureAwait(false);
        var providerBytes = await File.ReadAllBytesAsync(providerFile, cancellationToken).ConfigureAwait(false);
        using var receiptDocument = JsonDocument.Parse(receiptBytes);
        var receiptRoot = receiptDocument.RootElement;
        var post = Deserialize<Campaign2StateObservation>(postBytes);
        var provider = Deserialize<Campaign2ProviderOutcome>(providerBytes);
        if (post.ObservedAt <= begin.DispatchedAt || provider.ObservedAt <= begin.DispatchedAt)
        {
            throw new InvalidDataException("Post-dispatch evidence timestamps must be later than durable dispatch.");
        }
        if (post.Branch != begin.Branch)
        {
            throw new InvalidDataException("Post observation branch differs from the sealed dispatch branch.");
        }
        var receiptAction = RequiredString(receiptRoot, "semantic_action");
        if (receiptAction != begin.SemanticAction)
        {
            throw new InvalidDataException("Provider receipt action does not match the sealed action.");
        }
        var receiptAccepted = RequiredBoolean(receiptRoot, "receipt_accepted");
        var resolved = Campaign2OutcomeResolver.Resolve(begin.SemanticAction, before, post, receiptAccepted, provider);
        var prediction = await LoadPredictionAsync(secretFile, begin.PredictionId, cancellationToken).ConfigureAwait(false);
        var score = PredictionScorer.Score(
            begin.SemanticAction,
            prediction,
            resolved.ActualPropositions,
            Array.Empty<string>(),
            resolved.ActualDeltas,
            Array.Empty<string>(),
            resolved.ViolatedInvariants);
        if (score.EligibilityStatus != "eligible" || score.MeanBrierLoss is null)
        {
            throw new InvalidDataException("A complete Campaign 2 acquisition outcome unexpectedly became ineligible.");
        }

        var store = new EvidenceStore(evidenceRoot);
        var receiptAt = RequiredDateTime(receiptRoot, "completed_at");
        var receiptEvidence = await store.PutAsync(receiptBytes,
            begin.SemanticAction.StartsWith("github:", StringComparison.Ordinal) ? "github/provider" : "git/native",
            begin.SemanticAction.StartsWith("github:", StringComparison.Ordinal) ? "eyeBROWSE" : "experiment-git-facet",
            "application/json", "provider-action-receipt", receiptAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);
        var postEvidence = await store.PutAsync(postBytes, "git/native", "campaign2-state-observer",
            "application/json", "fresh-post-dispatch-observation", post.ObservedAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);
        var providerEvidence = await store.PutAsync(providerBytes, "github/provider", "campaign2-provider-outcome-observer",
            "application/json", "locked-horizon-provider-observation", provider.ObservedAt, encoding: "utf-8", cancellationToken: cancellationToken).ConfigureAwait(false);

        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        foreach (var evidence in new[] { receiptEvidence, postEvidence, providerEvidence })
        {
            await database.InsertEvidenceAsync(evidence, cancellationToken).ConfigureAwait(false);
        }
        var postObservationId = Guid.NewGuid();
        var postObservation = new ObservationRecord(
            postObservationId,
            begin.LocalManifestationId,
            "campaign2-state-observer",
            "campaign2-state-observation-v1",
            "git/native",
            post.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, local = true, remote = true, locked_horizons = true }, JsonDefaults.Options),
            post.RemoteHead,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "native-git-plus-provider-outcome" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(post, JsonDefaults.Options),
            [postEvidence.EvidenceId, providerEvidence.EvidenceId]);
        await database.InsertObservationAsync(postObservation, cancellationToken).ConfigureAwait(false);
        var providerObservationId = Guid.NewGuid();
        var providerObservation = new ObservationRecord(
            providerObservationId,
            begin.RemoteManifestationId,
            "campaign2-provider-outcome-observer",
            "campaign2-provider-outcome-v1",
            "github/provider",
            provider.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, provider_native = true }, JsonDefaults.Options),
            post.RemoteHead,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "github-provider-outcome" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(provider, JsonDefaults.Options),
            [providerEvidence.EvidenceId]);
        await database.InsertObservationAsync(providerObservation, cancellationToken).ConfigureAwait(false);
        var postClaims = await InsertStateClaimsAsync(
            database, begin.LocalManifestationId, begin.RemoteManifestationId, postObservationId, postEvidence.EvidenceId,
            post, provider, providerObservationId, providerEvidence.EvidenceId, cancellationToken).ConfigureAwait(false);
        await InsertReobservationDispositionsAsync(database, begin.PreClaimIds, postClaims, post.ObservedAt, cancellationToken).ConfigureAwait(false);

        var outcomeId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var closedAt = DateTimeOffset.UtcNow;
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            await InsertActionPhaseAsync(connection, transaction, begin.ActionId, "provider_acknowledged",
                JsonSerializer.SerializeToElement(new { receipt_accepted = receiptAccepted, receipt_sha256 = receiptEvidence.ContentHash }, JsonDefaults.Options),
                receiptEvidence.EvidenceId, token).ConfigureAwait(false);
            await InsertActionPhaseAsync(connection, transaction, begin.ActionId, "post_observed",
                JsonSerializer.SerializeToElement(new { post_sha256 = postEvidence.ContentHash, provider_sha256 = providerEvidence.ContentHash }, JsonDefaults.Options),
                postEvidence.EvidenceId, token).ConfigureAwait(false);

            await using (var outcome = new NpgsqlCommand("""
                INSERT INTO wk.outcome(
                  outcome_id,action_id,horizon_id,resolution_status,actual_propositions,actual_deltas,actual_invariants,
                  attribution_status,resolver_version,resolved_at
                ) VALUES (@id,@action,'locked','verified',@actual,@deltas,@invariants,
                  'consistent_with_action','campaign2-outcome-resolver-v1',clock_timestamp());
                """, connection, transaction))
            {
                outcome.Parameters.AddWithValue("id", outcomeId);
                outcome.Parameters.AddWithValue("action", begin.ActionId);
                KernelDb.AddJson(outcome, "actual", JsonSerializer.SerializeToElement(resolved.ActualPropositions, JsonDefaults.Options));
                KernelDb.AddJson(outcome, "deltas", JsonSerializer.SerializeToElement(resolved.ActualDeltas.ToDictionary(value => value, _ => true, StringComparer.Ordinal), JsonDefaults.Options));
                KernelDb.AddJson(outcome, "invariants", JsonSerializer.SerializeToElement(new Dictionary<string, bool>(), JsonDefaults.Options));
                await outcome.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await InsertOutcomeLinksAsync(connection, transaction, outcomeId, [postObservationId, providerObservationId],
                [postEvidence.EvidenceId, providerEvidence.EvidenceId, receiptEvidence.EvidenceId], token).ConfigureAwait(false);

            await using (var evaluation = new NpgsqlCommand("""
                INSERT INTO wk.prediction_evaluation(
                  evaluation_id,prediction_id,outcome_id,eligibility_status,scorer_version,mean_brier_loss,
                  brier_components,delta_tp,delta_fp,delta_fn,delta_precision,delta_recall,delta_f1,
                  invariant_violations,latency_metrics,evaluated_at
                ) VALUES (@id,@prediction,@outcome,@eligibility,@scorer,@mean,@components,@tp,@fp,@fn,
                  @precision,@recall,@f1,@violations,@latency,clock_timestamp());
                """, connection, transaction))
            {
                evaluation.Parameters.AddWithValue("id", evaluationId);
                evaluation.Parameters.AddWithValue("prediction", begin.PredictionId);
                evaluation.Parameters.AddWithValue("outcome", outcomeId);
                evaluation.Parameters.AddWithValue("eligibility", score.EligibilityStatus);
                evaluation.Parameters.AddWithValue("scorer", Build001Contract.ScorerVersion);
                evaluation.Parameters.AddWithValue("mean", score.MeanBrierLoss.Value);
                KernelDb.AddJson(evaluation, "components", JsonSerializer.SerializeToElement(score.BrierComponents, JsonDefaults.Options));
                evaluation.Parameters.AddWithValue("tp", score.DeltaTruePositive);
                evaluation.Parameters.AddWithValue("fp", score.DeltaFalsePositive);
                evaluation.Parameters.AddWithValue("fn", score.DeltaFalseNegative);
                KernelDb.AddNullable(evaluation, "precision", NpgsqlDbType.Double, score.DeltaPrecision);
                KernelDb.AddNullable(evaluation, "recall", NpgsqlDbType.Double, score.DeltaRecall);
                KernelDb.AddNullable(evaluation, "f1", NpgsqlDbType.Double, score.DeltaF1);
                KernelDb.AddJson(evaluation, "violations", JsonSerializer.SerializeToElement(score.InvariantViolations, JsonDefaults.Options));
                KernelDb.AddJson(evaluation, "latency", JsonSerializer.SerializeToElement(new
                {
                    dispatch_to_post_ms = (post.ObservedAt - begin.DispatchedAt).TotalMilliseconds,
                    dispatch_to_provider_outcome_ms = (provider.ObservedAt - begin.DispatchedAt).TotalMilliseconds
                }, JsonDefaults.Options));
                await evaluation.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await InsertActionPhaseAsync(connection, transaction, begin.ActionId, "outcome_resolved",
                JsonSerializer.SerializeToElement(new { outcome_id = outcomeId, resolution_status = "verified" }, JsonDefaults.Options),
                providerEvidence.EvidenceId, token).ConfigureAwait(false);
            await InsertActionPhaseAsync(connection, transaction, begin.ActionId, "evaluated",
                JsonSerializer.SerializeToElement(new { evaluation_id = evaluationId, mean_brier_loss = score.MeanBrierLoss }, JsonDefaults.Options),
                null, token).ConfigureAwait(false);

            await using (var episode = new NpgsqlCommand("""
                INSERT INTO wk.transition_episode(
                  episode_id,trial_id,configuration_block_id,arm,action_id,prediction_id,public_environment_scope,
                  environment_fingerprint,producer_versions,closed_at
                ) VALUES (@id,@trial,@block,@arm,@action,@prediction,@scope,@fingerprint,@versions,clock_timestamp());
                """, connection, transaction))
            {
                episode.Parameters.AddWithValue("id", episodeId);
                episode.Parameters.AddWithValue("trial", begin.TrialId);
                episode.Parameters.AddWithValue("block", begin.ConfigurationBlockId);
                episode.Parameters.AddWithValue("arm", begin.Arm);
                episode.Parameters.AddWithValue("action", begin.ActionId);
                episode.Parameters.AddWithValue("prediction", begin.PredictionId);
                KernelDb.AddJson(episode, "scope", JsonSerializer.SerializeToElement(new
                {
                    campaign_id = CampaignId,
                    fixture_repository = FixtureRepository,
                    branch = begin.Branch,
                    seed_commitment_sha256 = begin.SeedCommitmentSha256,
                    public_topology_class = before.PublicTopologyClass
                }, JsonDefaults.Options));
                episode.Parameters.AddWithValue("fingerprint", begin.EnvironmentFingerprint);
                KernelDb.AddJson(episode, "versions", JsonSerializer.SerializeToElement(new
                {
                    kernel = "build001",
                    freeze_manifest_sha256 = begin.FreezeManifestSha256,
                    scorer = Build001Contract.ScorerVersion,
                    subject_adapter = Campaign2Attestation.FreshInvocationMethodVersion
                }, JsonDefaults.Options));
                await episode.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var link in new[]
                     {
                         ("episode_correspondence", "correspondence_id", begin.CorrespondenceId),
                         ("episode_pre_observation", "observation_id", begin.PreObservationId),
                         ("episode_post_observation", "observation_id", postObservationId),
                         ("episode_post_observation", "observation_id", providerObservationId),
                         ("episode_outcome", "outcome_id", outcomeId),
                         ("episode_evaluation", "evaluation_id", evaluationId)
                     })
            {
                await using var command = new NpgsqlCommand(
                    $"INSERT INTO wk.{link.Item1}(episode_id,{link.Item2}) VALUES (@episode,@value);", connection, transaction);
                command.Parameters.AddWithValue("episode", episodeId);
                command.Parameters.AddWithValue("value", link.Item3);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var claimId in begin.PreClaimIds.Values)
            {
                await using var command = new NpgsqlCommand(
                    "INSERT INTO wk.episode_pre_claim(episode_id,claim_id) VALUES (@episode,@claim);", connection, transaction);
                command.Parameters.AddWithValue("episode", episodeId);
                command.Parameters.AddWithValue("claim", claimId);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        await RecordEvaluatorGroundTruthAsync(
            secretFile, begin, resolved, postEvidence.ContentHash, providerEvidence.ContentHash, provider.ObservedAt, cancellationToken).ConfigureAwait(false);

        var preObservationSha256 = CanonicalJson.Sha256(await File.ReadAllBytesAsync(begin.PreObservationPath, cancellationToken).ConfigureAwait(false));
        var localManifestationRef = $"git:working-copy:{begin.TrialId}";
        var publicClaims = BuildPublicClaimExports(
                begin.PreClaimIds, before, null, localManifestationRef, preObservationSha256, null, "historical_pre_reobservation")
            .Concat(BuildPublicClaimExports(
                postClaims, post, provider, localManifestationRef, postEvidence.ContentHash, providerEvidence.ContentHash, "supported_at_episode_close"))
            .ToArray();
        var publicCorrespondence = new PublicCorrespondenceExport(
            begin.CorrespondenceId,
            localManifestationRef,
            "git:working_copy_of",
            FixtureManifestationRef,
            "candidate",
            1.0,
            before.ObservedAt,
            before.ObservedAt,
            [preObservationSha256]);
        var publicEpisode = new EpisodeExport(
            episodeId,
            begin.SemanticAction,
            FixtureManifestationRef,
            before.PublicTopologyClass,
            closedAt,
            BuildPublicObservedFacts(before),
            prediction.ToDictionary(value => value.Key, value => value.Value!.Value, StringComparer.Ordinal),
            resolved.ActualPropositions,
            score.BrierComponents,
            score.MeanBrierLoss,
            resolved.ActualDeltas,
            score.InvariantViolations,
            "verified",
            publicClaims,
            [publicCorrespondence],
            new[] { preObservationSha256, receiptEvidence.ContentHash, postEvidence.ContentHash, providerEvidence.ContentHash }
                .Distinct(StringComparer.Ordinal).ToArray(),
            ProviderVersionFingerprint);
        var publicEpisodePath = Path.Combine(Path.GetDirectoryName(outputFile)!, "episode-public.json");
        EnsureAbsent(publicEpisodePath);
        var publicEpisodeBytes = CanonicalJson.Serialize(publicEpisode);
        await WriteNewAsync(publicEpisodePath, publicEpisodeBytes, cancellationToken).ConfigureAwait(false);
        var publicEpisodeSha256 = CanonicalJson.Sha256(publicEpisodeBytes);

        var record = new Campaign2CloseRecord(
            CloseRecordSchema,
            CampaignId,
            begin.TrialId,
            begin.ConfigurationBlockId,
            begin.SemanticAction,
            begin.ActionId,
            begin.PredictionId,
            outcomeId,
            evaluationId,
            episodeId,
            score.EligibilityStatus,
            score.MeanBrierLoss.Value,
            resolved.ActualPropositions,
            score.BrierComponents,
            resolved.ActualDeltas,
            CanonicalJson.Sha256(receiptBytes),
            CanonicalJson.Sha256(postBytes),
            CanonicalJson.Sha256(providerBytes),
            publicEpisodePath,
            publicEpisodeSha256,
            closedAt);
        await WriteNewAsync(outputFile, CanonicalJson.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record;
    }

    public static async Task<Campaign2AcquisitionCoverage> WriteCoverageAsync(
        string repositoryRoot,
        string secretFile,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var output = EnsureUnder(Path.Combine(root, "experiments", "build001", "campaign-2"), outputPath);
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var rows = await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("""
                SELECT e.configuration_block_id,
                       e.public_environment_scope->>'seed_commitment_sha256' AS seed,
                       a.semantic_action_namespace || ':' || a.semantic_action_type AS semantic_action,
                       pe.eligibility_status,
                       o.actual_propositions
                FROM wk.transition_episode e
                JOIN wk.action_attempt a ON a.action_id=e.action_id
                JOIN wk.episode_outcome eo ON eo.episode_id=e.episode_id
                JOIN wk.outcome o ON o.outcome_id=eo.outcome_id
                JOIN wk.episode_evaluation ee ON ee.episode_id=e.episode_id
                JOIN wk.prediction_evaluation pe ON pe.evaluation_id=ee.evaluation_id
                WHERE e.arm='acquisition'
                  AND e.public_environment_scope->>'campaign_id'=@campaign
                ORDER BY e.configuration_block_id, semantic_action;
                """, connection);
            command.Parameters.AddWithValue("campaign", CampaignId);
            var result = new List<CoverageRow>();
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                using var actual = JsonDocument.Parse(reader.GetString(4));
                result.Add(new CoverageRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), actual.RootElement.Clone()));
            }
            return result;
        }, cancellationToken).ConfigureAwait(false);

        var eligible = rows.Where(row => row.Eligibility == "eligible").ToArray();
        var actionCoverage = Build001Contract.Propositions.Keys.OrderBy(value => value, StringComparer.Ordinal).Select(action =>
        {
            var actionRows = eligible.Where(row => row.SemanticAction == action).ToArray();
            return new Campaign2ActionCoverage(
                action,
                actionRows.Length,
                actionRows.Select(row => row.Block).Distinct(StringComparer.Ordinal).Count(),
                actionRows.Select(row => row.Seed).Distinct(StringComparer.Ordinal).Count());
        }).ToArray();
        var pushRows = eligible.Where(row => row.SemanticAction == "git:push_ref").ToArray();
        var integrateRows = eligible.Where(row => row.SemanticAction == "git:integrate_fast_forward").ToArray();
        var pushAccepted = CountBoolean(pushRows, "provider_accepts_push", true);
        var pushRejected = CountBoolean(pushRows, "provider_accepts_push", false);
        var ffAccepted = CountBoolean(integrateRows, "fast_forward_is_accepted", true);
        var ffRejected = CountBoolean(integrateRows, "fast_forward_is_accepted", false);
        var checkTrue = CountBoolean(pushRows, "remote_check_starts_by_H2", true);
        var checkFalse = CountBoolean(pushRows, "remote_check_starts_by_H2", false);
        var blocks = eligible.Select(row => row.Block).Distinct(StringComparer.Ordinal).Count();
        var twenty = actionCoverage.All(value => value.EligibleClosedEpisodes >= 20);
        var pushBalance = pushAccepted >= 8 && pushRejected >= 8;
        var ffBalance = ffAccepted >= 8 && ffRejected >= 8;
        var checkBalance = checkTrue >= 8 && checkFalse >= 8;
        var seeds = actionCoverage.All(value => value.DistinctSeeds >= 6);
        var initial = blocks >= 24;
        var coverage = new Campaign2AcquisitionCoverage(
            CoverageSchema,
            CampaignId,
            DateTimeOffset.UtcNow,
            blocks,
            actionCoverage,
            pushAccepted,
            pushRejected,
            ffAccepted,
            ffRejected,
            checkTrue,
            checkFalse,
            twenty,
            pushBalance,
            ffBalance,
            checkBalance,
            seeds,
            initial,
            twenty && pushBalance && ffBalance && checkBalance && seeds && initial);
        await WriteReplaceAsync(output, CanonicalJson.Serialize(coverage), cancellationToken).ConfigureAwait(false);
        return coverage;
    }

    private sealed record CoverageRow(string Block, string Seed, string SemanticAction, string Eligibility, JsonElement Actual);

    private static int CountBoolean(IEnumerable<CoverageRow> rows, string key, bool expected) =>
        rows.Count(row => row.Actual.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean() == expected);

    private static async Task RecordEvaluatorGroundTruthAsync(
        string secretFile,
        Campaign2BeginRecord begin,
        Campaign2ResolvedOutcome resolved,
        string postObservationSha256,
        string providerOutcomeSha256,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        await using var source = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            INSERT INTO eval001.ground_truth(
              ground_truth_id,action_id,configuration_block_id,horizon_id,actual_propositions,actual_deltas,
              actual_invariants,provider_evidence_hashes,resolver_version,resolved_at)
            VALUES (@id,@action,@block,'locked',@actual,@deltas,@invariants,@hashes,'campaign2-outcome-resolver-v1',@resolved);
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("action", begin.ActionId);
        command.Parameters.AddWithValue("block", begin.ConfigurationBlockId);
        KernelDb.AddJson(command, "actual", JsonSerializer.SerializeToElement(resolved.ActualPropositions, JsonDefaults.Options));
        KernelDb.AddJson(command, "deltas", JsonSerializer.SerializeToElement(
            resolved.ActualDeltas.ToDictionary(value => value, _ => true, StringComparer.Ordinal), JsonDefaults.Options));
        KernelDb.AddJson(command, "invariants", JsonSerializer.SerializeToElement(
            resolved.ViolatedInvariants.ToDictionary(value => value, _ => false, StringComparer.Ordinal), JsonDefaults.Options));
        KernelDb.AddJson(command, "hashes", JsonSerializer.SerializeToElement(
            new[] { postObservationSha256, providerOutcomeSha256 }, JsonDefaults.Options));
        command.Parameters.AddWithValue("resolved", resolvedAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    private static IReadOnlyList<string> BuildPublicObservedFacts(Campaign2StateObservation state) =>
    [
        $"local_head={state.LocalHead}",
        $"current_branch={state.CurrentBranch}",
        $"local_tree={state.LocalTree}",
        $"worktree_clean={state.WorktreeClean.ToString().ToLowerInvariant()}",
        $"remote_head={state.RemoteHead ?? "absent"}",
        $"remote_tracking_head={state.RemoteTrackingHead ?? "absent"}",
        $"remote_head_reachable_locally={state.RemoteHeadReachableLocally.ToString().ToLowerInvariant()}",
        $"public_topology_class={state.PublicTopologyClass}"
    ];

    private static IReadOnlyList<PublicClaimExport> BuildPublicClaimExports(
        IReadOnlyDictionary<string, Guid> ids, Campaign2StateObservation state, Campaign2ProviderOutcome? provider,
        string localRef, string stateEvidenceHash, string? providerEvidenceHash, string disposition)
    {
        var result = new List<PublicClaimExport>();
        foreach (var pair in ids.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            var providerSpecific = pair.Key is "github:check_started" or "github:check_terminal_success" or "github:browser_presented_head";
            var value = pair.Key switch
            {
                "git:local_head" => JsonSerializer.SerializeToElement(state.LocalHead),
                "git:current_branch" => JsonSerializer.SerializeToElement(state.CurrentBranch),
                "git:worktree_clean" => JsonSerializer.SerializeToElement(state.WorktreeClean),
                "git:remote_tracking_head" => JsonSerializer.SerializeToElement(state.RemoteTrackingHead),
                "git:remote_url" => JsonSerializer.SerializeToElement(state.RemoteUrl),
                "github:remote_ref_head" => JsonSerializer.SerializeToElement(state.RemoteHead),
                "git:public_topology_class" => JsonSerializer.SerializeToElement(state.PublicTopologyClass),
                "github:check_started" => JsonSerializer.SerializeToElement(provider!.Check.Started),
                "github:check_terminal_success" => JsonSerializer.SerializeToElement(provider!.Check.TerminalSuccess),
                "github:browser_presented_head" => JsonSerializer.SerializeToElement(provider!.Browser.PresentedHead),
                _ => throw new InvalidDataException("Unknown Campaign 2 public claim key: " + pair.Key)
            };
            var knownAt = providerSpecific ? provider!.ObservedAt : state.ObservedAt;
            var evidenceHash = providerSpecific ? providerEvidenceHash! : stateEvidenceHash;
            result.Add(new PublicClaimExport(
                pair.Value,
                pair.Key.StartsWith("github:", StringComparison.Ordinal) ? FixtureManifestationRef : localRef,
                pair.Key,
                value,
                pair.Key == "git:public_topology_class" ? "derived" : "provider",
                pair.Key == "git:public_topology_class" ? "derived" : "observed",
                knownAt,
                null,
                knownAt,
                "historical_at_retrieval",
                disposition,
                [evidenceHash]));
        }
        return result;
    }
    private static async Task<IReadOnlyDictionary<string, double?>> LoadPredictionAsync(
        string secretFile,
        Guid predictionId,
        CancellationToken cancellationToken)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        return await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("SELECT outcome_probabilities FROM wk.prediction WHERE prediction_id=@id;", connection);
            command.Parameters.AddWithValue("id", predictionId);
            var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string
                        ?? throw new DataException("Prediction was not found.");
            return JsonSerializer.Deserialize<Dictionary<string, double?>>(value, JsonDefaults.Options)
                   ?? throw new DataException("Stored prediction is invalid.");
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Guid> EnsureRemoteManifestationAsync(KernelDb database, CancellationToken cancellationToken)
    {
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var command = new NpgsqlCommand("""
                SELECT manifestation_id FROM wk.manifestation
                WHERE provider_namespace='github/provider' AND provider_native_id=@native
                ORDER BY recorded_at LIMIT 1;
                """, connection);
            command.Parameters.AddWithValue("native", FixtureNativeId);
            return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        if (existing is Guid id) return id;
        var created = Guid.NewGuid();
        await database.InsertManifestationAsync(new ManifestationRecord(
            created,
            "github/provider",
            "github-repository",
            JsonSerializer.SerializeToElement(new { provider_native_id = FixtureNativeId, repository = FixtureRepository }, JsonDefaults.Options),
            "github-repository-1330898503",
            FixtureNativeId,
            JsonSerializer.SerializeToElement(new { github_repository_id = FixtureNativeId }, JsonDefaults.Options),
            FixtureRepository), cancellationToken).ConfigureAwait(false);
        return created;
    }

    private static async Task<IReadOnlyDictionary<string, Guid>> InsertStateClaimsAsync(
        KernelDb database, Guid localId, Guid remoteId, Guid localObservationId, Guid localEvidenceId,
        Campaign2StateObservation state, Campaign2ProviderOutcome? provider, Guid? providerObservationId,
        Guid? providerEvidenceId, CancellationToken cancellationToken)
    {
        var claims = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            async Task AddAsync(string key, Guid subject, string ns, string predicate, JsonElement value,
                string method, string authority, DateTimeOffset validAt, Guid observationId, Guid evidenceId)
            {
                claims[key] = await InsertClaimRowAsync(connection, transaction, subject, ns, predicate, value,
                    method, authority, validAt, state.Branch, observationId, evidenceId, token).ConfigureAwait(false);
            }
            await AddAsync("git:local_head", localId, "git", "local_head", JsonSerializer.SerializeToElement(state.LocalHead), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("git:current_branch", localId, "git", "current_branch", JsonSerializer.SerializeToElement(state.CurrentBranch), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("git:worktree_clean", localId, "git", "worktree_clean", JsonSerializer.SerializeToElement(state.WorktreeClean), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("git:remote_tracking_head", localId, "git", "remote_tracking_head", JsonSerializer.SerializeToElement(state.RemoteTrackingHead), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("git:remote_url", localId, "git", "configured_remote_url", JsonSerializer.SerializeToElement(state.RemoteUrl), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("github:remote_ref_head", remoteId, "github", "remote_ref_head", JsonSerializer.SerializeToElement(state.RemoteHead), "observed", "provider", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            await AddAsync("git:public_topology_class", localId, "git", "public_topology_class", JsonSerializer.SerializeToElement(state.PublicTopologyClass), "derived", "derived", state.ObservedAt, localObservationId, localEvidenceId).ConfigureAwait(false);
            if (provider is not null && providerObservationId is Guid po && providerEvidenceId is Guid pe)
            {
                await AddAsync("github:check_started", remoteId, "github", "check_started", JsonSerializer.SerializeToElement(provider.Check.Started), "observed", "provider", provider.ObservedAt, po, pe).ConfigureAwait(false);
                await AddAsync("github:check_terminal_success", remoteId, "github", "check_terminal_success", JsonSerializer.SerializeToElement(provider.Check.TerminalSuccess), "observed", "provider", provider.ObservedAt, po, pe).ConfigureAwait(false);
                await AddAsync("github:browser_presented_head", remoteId, "github", "browser_presented_head", JsonSerializer.SerializeToElement(provider.Browser.PresentedHead), "observed", "provider", provider.ObservedAt, po, pe).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return claims;
    }
    private static async Task<Guid> InsertClaimRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid subjectId, string predicateNamespace,
        string predicate, JsonElement value, string productionMethod, string authorityClass, DateTimeOffset validAt,
        string branch, Guid observationId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var claimId = Guid.NewGuid();
        await using (var command = new NpgsqlCommand("""
            INSERT INTO wk.claim(
              claim_id,subject_manifestation_id,predicate_namespace,predicate,value_json,production_method,authority_class,
              valid_range,scope,producer,confidence,freshness_policy_id,primary_observation_id,primary_evidence_id)
            VALUES (@id,@subject,@namespace,@predicate,@value,@method,@authority,tstzrange(@valid,NULL,'[)'),@scope,@producer,
              1.0,'fresh-provider-reobservation-v1',@observation,@evidence);
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", claimId); command.Parameters.AddWithValue("subject", subjectId);
            command.Parameters.AddWithValue("namespace", predicateNamespace); command.Parameters.AddWithValue("predicate", predicate);
            KernelDb.AddJson(command, "value", value); command.Parameters.AddWithValue("method", productionMethod);
            command.Parameters.AddWithValue("authority", authorityClass); command.Parameters.AddWithValue("valid", validAt);
            KernelDb.AddJson(command, "scope", JsonSerializer.SerializeToElement(new { campaign_id = CampaignId, branch }, JsonDefaults.Options));
            KernelDb.AddJson(command, "producer", JsonSerializer.SerializeToElement(new { component = "campaign2-execution", version = "v1" }, JsonDefaults.Options));
            command.Parameters.AddWithValue("observation", observationId); command.Parameters.AddWithValue("evidence", evidenceId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var observation = new NpgsqlCommand("INSERT INTO wk.claim_observation(claim_id,observation_id) VALUES (@claim,@value);", connection, transaction))
        { observation.Parameters.AddWithValue("claim", claimId); observation.Parameters.AddWithValue("value", observationId); await observation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var evidence = new NpgsqlCommand("INSERT INTO wk.claim_evidence(claim_id,evidence_id) VALUES (@claim,@value);", connection, transaction))
        { evidence.Parameters.AddWithValue("claim", claimId); evidence.Parameters.AddWithValue("value", evidenceId); await evidence.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        return claimId;
    }
    private static async Task InsertReobservationDispositionsAsync(
        KernelDb database, IReadOnlyDictionary<string, Guid> prior, IReadOnlyDictionary<string, Guid> replacement,
        DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            foreach (var key in prior.Keys.Intersect(replacement.Keys, StringComparer.Ordinal))
            {
                bool equal;
                await using (var compare = new NpgsqlCommand("""
                    SELECT p.value_json = r.value_json
                    FROM wk.claim p, wk.claim r
                    WHERE p.claim_id=@prior AND r.claim_id=@replacement;
                    """, connection, transaction))
                {
                    compare.Parameters.AddWithValue("prior", prior[key]);
                    compare.Parameters.AddWithValue("replacement", replacement[key]);
                    equal = Convert.ToBoolean(await compare.ExecuteScalarAsync(token).ConfigureAwait(false));
                }
                await using var command = new NpgsqlCommand("""
                    INSERT INTO wk.claim_disposition(
                      claim_disposition_id,target_claim_id,relation,effective_valid_at,basis,producer,replacement_claim_id,rationale_code)
                    VALUES (@id,@target,@relation,@effective,@basis,@producer,@replacement,@rationale);
                    """, connection, transaction);
                command.Parameters.AddWithValue("id", Guid.NewGuid());
                command.Parameters.AddWithValue("target", prior[key]);
                command.Parameters.AddWithValue("relation", equal ? "supports" : "supersedes");
                command.Parameters.AddWithValue("effective", effectiveAt);
                KernelDb.AddJson(command, "basis", JsonSerializer.SerializeToElement(new { campaign_id = CampaignId, predicate_key = key, fresh_provider_reobservation = true }, JsonDefaults.Options));
                KernelDb.AddJson(command, "producer", JsonSerializer.SerializeToElement(new { component = "campaign2-execution", version = "v1" }, JsonDefaults.Options));
                KernelDb.AddNullable(command, "replacement", NpgsqlDbType.Uuid, equal ? null : replacement[key]);
                command.Parameters.AddWithValue("rationale", equal ? "fresh_provider_reobservation_same_value" : "fresh_provider_reobservation_changed_value");
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
    private static async Task<Guid> InsertCorrespondenceAsync(
        KernelDb database,
        Guid localId,
        Guid remoteId,
        Guid observationId,
        Guid preEvidenceId,
        IEnumerable<Guid> basisClaimIds,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            await using (var command = new NpgsqlCommand("""
                INSERT INTO wk.correspondence_claim(
                  correspondence_id,left_manifestation_id,relation_namespace,relation_type,right_manifestation_id,
                  method,confidence,strength,valid_range,producer,basis_fingerprint
                ) VALUES (@id,@left,'git','working_copy_of',@right,'campaign2-provider-reset',1.0,'candidate',
                  tstzrange(clock_timestamp(),NULL,'[)'),@producer,@fingerprint);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("left", localId);
                command.Parameters.AddWithValue("right", remoteId);
                KernelDb.AddJson(command, "producer", JsonSerializer.SerializeToElement(new { campaign_id = CampaignId, method = "fixture-reset-plus-native-git" }, JsonDefaults.Options));
                command.Parameters.AddWithValue("fingerprint", fingerprint);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await using (var observation = new NpgsqlCommand(
                             "INSERT INTO wk.correspondence_observation(correspondence_id,observation_id) VALUES (@id,@value);", connection, transaction))
            {
                observation.Parameters.AddWithValue("id", id);
                observation.Parameters.AddWithValue("value", observationId);
                await observation.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var evidenceId in new[] { preEvidenceId })
            {
                await using var evidence = new NpgsqlCommand(
                    "INSERT INTO wk.correspondence_evidence(correspondence_id,evidence_id) VALUES (@id,@value);", connection, transaction);
                evidence.Parameters.AddWithValue("id", id);
                evidence.Parameters.AddWithValue("value", evidenceId);
                await evidence.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var claimId in basisClaimIds)
            {
                await using var claim = new NpgsqlCommand(
                    "INSERT INTO wk.correspondence_claim_basis(correspondence_id,claim_id) VALUES (@id,@value);", connection, transaction);
                claim.Parameters.AddWithValue("id", id);
                claim.Parameters.AddWithValue("value", claimId);
                await claim.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static async Task InsertPredictionLineageAsync(
        KernelDb database,
        Guid actionId,
        Guid predictionId,
        Guid observationId,
        IReadOnlyList<Guid> evidenceIds,
        CancellationToken cancellationToken)
    {
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            await using (var precondition = new NpgsqlCommand(
                             "INSERT INTO wk.action_precondition_observation(action_id,observation_id) VALUES (@action,@observation);", connection, transaction))
            {
                precondition.Parameters.AddWithValue("action", actionId);
                precondition.Parameters.AddWithValue("observation", observationId);
                await precondition.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var evidenceId in evidenceIds)
            {
                await using var evidence = new NpgsqlCommand(
                    "INSERT INTO wk.prediction_basis_evidence(prediction_id,evidence_id) VALUES (@prediction,@evidence);", connection, transaction);
                evidence.Parameters.AddWithValue("prediction", predictionId);
                evidence.Parameters.AddWithValue("evidence", evidenceId);
                await evidence.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertActionPhaseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actionId,
        string phase,
        JsonElement payload,
        Guid? evidenceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO wk.action_phase(action_phase_id,action_id,phase,payload,evidence_id)
            VALUES (@id,@action,@phase,@payload,@evidence);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("action", actionId);
        command.Parameters.AddWithValue("phase", phase);
        KernelDb.AddJson(command, "payload", payload);
        KernelDb.AddNullable(command, "evidence", NpgsqlDbType.Uuid, evidenceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertOutcomeLinksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid outcomeId,
        IReadOnlyList<Guid> observationIds,
        IReadOnlyList<Guid> evidenceIds,
        CancellationToken cancellationToken)
    {
        foreach (var observationId in observationIds)
        {
            await using var observation = new NpgsqlCommand(
                "INSERT INTO wk.outcome_observation(outcome_id,observation_id) VALUES (@outcome,@observation);", connection, transaction);
            observation.Parameters.AddWithValue("outcome", outcomeId);
            observation.Parameters.AddWithValue("observation", observationId);
            await observation.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (var evidenceId in evidenceIds)
        {
            await using var evidence = new NpgsqlCommand(
                "INSERT INTO wk.outcome_evidence(outcome_id,evidence_id) VALUES (@outcome,@evidence);", connection, transaction);
            evidence.Parameters.AddWithValue("outcome", outcomeId);
            evidence.Parameters.AddWithValue("evidence", evidenceId);
            await evidence.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetFreezeManifestSha256(string root)
    {
        var path = Path.Combine(root, "experiments", "build001", "campaign-2", "preregistration-freeze-manifest.json");
        var sidecar = path + ".sha256";
        var actual = CanonicalJson.Sha256(File.ReadAllBytes(path));
        if (!File.Exists(sidecar)) throw new InvalidDataException("Campaign 2 freeze manifest SHA-256 sidecar is absent.");
        var expected = File.ReadAllText(sidecar).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Campaign 2 freeze manifest hash mismatch: {actual}.");
        return actual;
    }

    private static void ValidateFreeze(string root)
    {
        _ = GetFreezeManifestSha256(root);
        var path = Path.Combine(root, "experiments", "build001", "campaign-2", "preregistration-freeze-manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var value = document.RootElement;
        if (RequiredString(value, "schema") != FreezeManifestSchema || RequiredString(value, "campaign_id") != CampaignId ||
            !RequiredBoolean(value, "valid") || !RequiredBoolean(value, "frozen_before_acquisition"))
            throw new InvalidDataException("Campaign 2 execution freeze manifest is not valid and prospective.");
        var implementation = value.GetProperty("implementation");
        var commit = RequiredString(implementation, "commit");
        if (commit.Length != 40 || commit.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("Campaign 2 execution implementation commit is invalid.");
        var files = implementation.GetProperty("frozen_files");
        if (files.ValueKind != JsonValueKind.Object || !files.EnumerateObject().Any())
            throw new InvalidDataException("Campaign 2 execution freeze contains no frozen implementation files.");
        foreach (var property in files.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Frozen implementation file hash is invalid.");
            var expected = property.Value.GetString()!;
            if (expected.Length != 64) throw new InvalidDataException("Frozen implementation file hash length is invalid.");
            var relative = property.Name.Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new InvalidDataException($"Frozen implementation file escaped or disappeared: {property.Name}.");
            var normalized = File.ReadAllText(full).Replace("\r\n", "\n", StringComparison.Ordinal);
            var actual = CanonicalJson.Sha256(Encoding.UTF8.GetBytes(normalized));
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException($"Frozen implementation file changed: {property.Name} {actual}.");
        }
    }

    private static void ValidateBeginInput(Campaign2BeginInput input)
    {
        if (input.Schema != BeginInputSchema || input.CampaignId != CampaignId || input.Phase != "acquisition" || input.Arm != "acquisition")
        {
            throw new InvalidDataException("Campaign 2 begin input is outside the authorized acquisition phase.");
        }
        _ = Build001Contract.ForAction(input.SemanticAction);
        if (input.Parameters.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(input.TrialId) ||
            string.IsNullOrWhiteSpace(input.ConfigurationBlockId) || string.IsNullOrWhiteSpace(input.Target))
        {
            throw new InvalidDataException("Campaign 2 begin input is incomplete.");
        }
    }

    private static async Task EnsureEvaluatorReadyAsync(
        string secretFile,
        Campaign2BeginInput input,
        JsonElement reset,
        CancellationToken cancellationToken)
    {
        var generationId = Guid.Parse(RequiredString(reset, "generation_id"));
        var fingerprint = RequiredString(reset, "actual_fingerprint");
        await using var source = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT s.seed_id, r.actual_fingerprint, r.passed
            FROM eval001.seed_commitment s
            JOIN eval001.reset_verification r ON r.seed_id=s.seed_id
            WHERE s.phase='acquisition' AND s.configuration_block_id=@block
              AND r.arm='acquisition' AND r.generation_id=@generation;
            """, connection);
        command.Parameters.AddWithValue("block", input.ConfigurationBlockId);
        command.Parameters.AddWithValue("generation", generationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.GetString(1) != fingerprint || !reader.GetBoolean(2) ||
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidDataException("Campaign 2 dispatch lacks a unique prospective evaluator seed/reset registration.");
        }
    }
    private static void ValidateReset(Campaign2BeginInput input, JsonElement reset, Campaign2StateObservation pre)
    {
        if (RequiredString(reset, "reset_version") != "build001-fixture-reset-v1" ||
            RequiredString(reset, "phase") != "acquisition" || RequiredString(reset, "arm") != "acquisition" ||
            !RequiredBoolean(reset, "reset_verified"))
        {
            throw new InvalidDataException("Fixture reset was not verified for Campaign 2 acquisition.");
        }
        var material = reset.GetProperty("material");
        if (RequiredString(material, "repository") != FixtureRepository ||
            RequiredString(material, "branch") != input.ResetBranch || pre.Branch != input.Branch ||
            pre.RemoteUrl != $"https://github.com/{FixtureRepository}.git")
        {
            throw new InvalidDataException("Fixture reset or pre-observation escaped the frozen fixture/branch.");
        }
        var fingerprint = RequiredString(reset, "actual_fingerprint");
        if (fingerprint.Length != 64 || RequiredString(reset, "seed_commitment_sha256").Length != 64)
        {
            throw new InvalidDataException("Fixture reset fingerprint/seed commitment is invalid.");
        }
    }

    private static IReadOnlyDictionary<string, double?> ValidateSubject(
        Campaign2BeginInput input,
        byte[] requestBytes,
        JsonElement request,
        JsonElement result)
    {
        if (RequiredString(result, "schema") != SubjectResultSchema || !RequiredBoolean(result, "passed") ||
            RequiredString(result, "mode") != "invoke" || RequiredString(result, "trial_id") != input.TrialId ||
            RequiredString(result, "arm") != input.Arm || RequiredBoolean(result, "observable_product_fallback") ||
            !RequiredBoolean(result, "machine_readable_response_parsed"))
        {
            throw new InvalidDataException("Campaign 2 subject adapter did not produce a valid fresh invocation.");
        }
        if (RequiredString(result, "request_sha256") != CanonicalJson.Sha256(requestBytes))
        {
            throw new InvalidDataException("Subject adapter request hash does not match the locked request bytes.");
        }
        if (RequiredString(request, "semantic_action") != input.SemanticAction || RequiredString(request, "target") != input.Target ||
            RequiredString(request, "arm") != input.Arm)
        {
            throw new InvalidDataException("Subject request differs from the scheduled action.");
        }
        var reasons = result.GetProperty("invalidation_reasons");
        if ((reasons.ValueKind == JsonValueKind.Array && reasons.GetArrayLength() != 0) ||
            (reasons.ValueKind == JsonValueKind.Object && reasons.EnumerateObject().Any()) ||
            reasons.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
        {
            throw new InvalidDataException("Subject invocation contains invalidation reasons.");
        }
        var ui = result.GetProperty("ui_evidence");
        var before = ui.GetProperty("before");
        var modelBefore = ui.GetProperty("model_before");
        var modelAfter = ui.GetProperty("model_after");
        if (!RequiredBoolean(before, "temporary_chat") || !RequiredBoolean(before, "signed_in") ||
            RequiredInt32(before, "message_marker_count") != 0 || RequiredInt32(before, "attachment_marker_count") != 0 ||
            RequiredBoolean(before, "project_context_present") || RequiredBoolean(before, "file_library_context_present") ||
            RequiredString(modelBefore, "selected_model") != "5.6 Sol" || RequiredString(modelAfter, "selected_model") != "5.6 Sol" ||
            RequiredString(modelBefore, "reasoning_selection") != "Extra High" || RequiredString(modelAfter, "reasoning_selection") != "Extra High")
        {
            throw new InvalidDataException("Subject UI evidence does not satisfy the frozen P0/P5 configuration.");
        }
        var output = result.GetProperty("subject_output");
        if (RequiredString(output, "action_class") != input.SemanticAction || RequiredString(output, "target") != input.Target ||
            output.GetProperty("requested_observations").GetArrayLength() != 0 ||
            string.IsNullOrWhiteSpace(RequiredString(output, "material_action")) ||
            CanonicalJson.HashJson(output.GetProperty("parameters")) != CanonicalJson.HashJson(input.Parameters))
        {
            throw new InvalidDataException("Subject output does not match the exact scheduled action and parameters.");
        }
        var vector = output.GetProperty("prediction");
        if (vector.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Subject prediction is not an object.");
        }
        var expected = Build001Contract.ForAction(input.SemanticAction);
        var supplied = vector.EnumerateObject().ToArray();
        if (supplied.Length != expected.Count || !supplied.Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Subject prediction does not contain the exact frozen proposition vector.");
        }
        var probabilities = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var property in supplied)
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out var value) ||
                double.IsNaN(value) || double.IsInfinity(value) || value is < 0 or > 1)
            {
                throw new InvalidDataException($"Invalid probability for {property.Name}.");
            }
            probabilities[property.Name] = value;
        }
        return probabilities;
    }

    private static T Deserialize<T>(byte[] bytes) =>
        JsonSerializer.Deserialize<T>(bytes, JsonDefaults.Options) ?? throw new InvalidDataException($"Unable to deserialize {typeof(T).Name}.");

    private static string RequiredString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new InvalidDataException($"Required string {name} is absent.");

    private static bool RequiredBoolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : throw new InvalidDataException($"Required boolean {name} is absent.");

    private static int RequiredInt32(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException($"Required integer {name} is absent.");

    private static DateTimeOffset RequiredDateTime(JsonElement value, string name) =>
        DateTimeOffset.Parse(RequiredString(value, name), System.Globalization.CultureInfo.InvariantCulture);

    private static string EnsureUnder(string parent, string child)
    {
        var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(child);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Campaign 2 path escapes the experiment namespace: {path}");
        }
        return path;
    }

    private static void EnsureAbsent(string path)
    {
        if (File.Exists(path)) throw new IOException($"Campaign 2 immutable output already exists: {path}");
    }

    private static async Task WriteNewAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteReplaceAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }
}
