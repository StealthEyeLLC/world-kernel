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
                ("merge_commit_created", false)),
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
    string EvaluatorSeedId,
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
    DateTimeOffset RegisteredAt);

public sealed record Campaign2ResetRegistrationRecord(
    string Schema,
    string CampaignId,
    string ConfigurationBlockId,
    string SeedId,
    Guid GenerationId,
    string ActualFingerprint,
    string ExpectedFingerprint,
    string ResetManifestSha256,
    string IndependentVerificationSha256,
    bool Passed,
    DateTimeOffset RegisteredAt);
public sealed record Campaign2BeginRecord(
    string Schema,
    string CampaignId,
    string Phase,
    string TrialId,
    string ConfigurationBlockId,
    string EvaluatorSeedId,
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
    Guid PreRemoteObservationId,
    Guid PreRemoteEvidenceId,
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

public static partial class Campaign2Execution
{
    public const string CampaignId = "build001-campaign-2r";
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
    private sealed record ExistingCloseState(
        Guid EpisodeId,
        Guid OutcomeId,
        Guid EvaluationId,
        string EligibilityStatus,
        double MeanBrierLoss,
        string BrierComponentsJson,
        string ActualPropositionsJson,
        DateTimeOffset ClosedAt,
        IReadOnlyDictionary<string, Guid> PostClaimIds);

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
            Path.Combine(root, "artifacts", "campaign-2r", "preflight", "preflight-gates.json"),
            "acquisition");
        var inputFile = Path.GetFullPath(inputPath);
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (inputFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hidden acquisition registration input must remain outside the repository tree.");
        }
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2r");
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
        DateTimeOffset registeredAt;
        var existing = new List<(string Phase, string Block, string Commitment, string Ref, string Revision, DateTimeOffset RecordedAt)>();
        await using (var select = new NpgsqlCommand("""
            SELECT phase,configuration_block_id,commitment_sha256,sealed_payload_ref,public_fixture_revision,recorded_at
            FROM eval001.seed_commitment WHERE seed_id=@seed ORDER BY recorded_at;
            """, connection))
        {
            select.Parameters.AddWithValue("seed", input.SeedId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                existing.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), ReadDbTimestamp(reader, 5)));
        }
        if (existing.Count > 1) throw new InvalidDataException("Prospective Campaign 2 seed commitment is duplicated.");
        if (existing.Count == 1)
        {
            var row = existing[0];
            if (row.Phase != "acquisition" || row.Block != input.ConfigurationBlockId || row.Commitment != hiddenHash ||
                Path.GetFullPath(row.Ref) != inputFile || row.Revision != input.PublicFixtureRevision)
                throw new InvalidDataException("Existing prospective seed commitment differs from the sealed retry input.");
            registeredAt = row.RecordedAt;
        }
        else
        {
            await using var seed = new NpgsqlCommand("""
                INSERT INTO eval001.seed_commitment(seed_id,phase,configuration_block_id,commitment_sha256,sealed_payload_ref,public_fixture_revision)
                VALUES (@seed,'acquisition',@block,@commitment,@ref,@revision) RETURNING recorded_at;
                """, connection);
            seed.Parameters.AddWithValue("seed", input.SeedId);
            seed.Parameters.AddWithValue("block", input.ConfigurationBlockId);
            seed.Parameters.AddWithValue("commitment", hiddenHash);
            seed.Parameters.AddWithValue("ref", input.SealedPayloadRef);
            seed.Parameters.AddWithValue("revision", input.PublicFixtureRevision);
            registeredAt = ReadDbTimestamp(await seed.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new DataException("Prospective seed commitment did not return a record time."));
        }
        var record = new Campaign2AcquisitionBlockRegistrationRecord(
            BlockRegistrationRecordSchema, CampaignId, input.ConfigurationBlockId, input.SeedId,
            hiddenHash, registeredAt);
        await WriteNewAsync(outputFile, CanonicalJson.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record;
    }

    public static async Task<Campaign2ResetRegistrationRecord> RegisterAcquisitionResetAsync(
        string repositoryRoot,
        string secretFile,
        string configurationBlockId,
        string seedId,
        string resetManifestPath,
        string independentVerificationPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        ValidateFreeze(root);
        PreflightGateEvaluator.EnsurePhaseAuthorized(
            Path.Combine(root, "artifacts", "campaign-2r", "preflight", "preflight-gates.json"),
            "acquisition");
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2r");
        var resetFile = EnsureUnder(campaignRoot, resetManifestPath);
        var verificationFile = EnsureUnder(campaignRoot, independentVerificationPath);
        var outputFile = EnsureUnder(campaignRoot, outputPath);
        EnsureAbsent(outputFile);
        var resetBytes = await File.ReadAllBytesAsync(resetFile, cancellationToken).ConfigureAwait(false);
        var verificationBytes = await File.ReadAllBytesAsync(verificationFile, cancellationToken).ConfigureAwait(false);
        using var resetDocument = JsonDocument.Parse(resetBytes);
        using var verificationDocument = JsonDocument.Parse(verificationBytes);
        var reset = resetDocument.RootElement;
        var verification = verificationDocument.RootElement;
        if (RequiredString(reset, "reset_version") != "build001-fixture-reset-v1" ||
            RequiredString(reset, "phase") != "acquisition" || RequiredString(reset, "arm") != "acquisition" ||
            !RequiredBoolean(reset, "reset_verified"))
            throw new InvalidDataException("Acquisition reset registration rejected an invalid reset manifest.");
        if (RequiredString(verification, "schema") != "world-kernel-build001-campaign2-independent-reset-verification-v1" ||
            !RequiredBoolean(verification, "exact_local_remote_match"))
            throw new InvalidDataException("Independent acquisition reset verification is invalid.");
        var generationId = Guid.Parse(RequiredString(reset, "generation_id"));
        var actualFingerprint = RequiredString(reset, "actual_fingerprint");
        var expectedFingerprint = RequiredString(verification, "expected_fingerprint");
        if (actualFingerprint.Length != 64 || expectedFingerprint.Length != 64 || actualFingerprint != expectedFingerprint)
            throw new InvalidDataException($"Independent reset fingerprint mismatch: reset={actualFingerprint} verifier={expectedFingerprint}.");
        if (RequiredString(reset.GetProperty("material"), "branch") != RequiredString(verification, "branch"))
            throw new InvalidDataException("Independent reset verifier observed a different branch.");

        var resetHash = CanonicalJson.Sha256(resetBytes);
        var verificationHash = CanonicalJson.Sha256(verificationBytes);
        await using var source = NpgsqlDataSource.Create(ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        string commitment;
        string sealedPayloadRef;
        string publicFixtureRevision;
        await using (var seed = new NpgsqlCommand("""
            SELECT commitment_sha256,sealed_payload_ref,public_fixture_revision FROM eval001.seed_commitment
            WHERE seed_id=@seed AND phase='acquisition' AND configuration_block_id=@block;
            """, connection))
        {
            seed.Parameters.AddWithValue("seed", seedId);
            seed.Parameters.AddWithValue("block", configurationBlockId);
            await using var reader = await seed.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("Acquisition reset has no prospective seed commitment for its configuration block.");
            commitment = reader.GetString(0);
            sealedPayloadRef = reader.GetString(1);
            publicFixtureRevision = reader.GetString(2);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("Acquisition reset has non-unique prospective seed state.");
        }
        var hiddenFile = Path.GetFullPath(sealedPayloadRef);
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (hiddenFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(hiddenFile))
            throw new InvalidDataException("Sealed evaluator payload is missing or leaked into the repository tree.");
        var hiddenInput = Deserialize<Campaign2AcquisitionBlockRegistrationInput>(
            await File.ReadAllBytesAsync(hiddenFile, cancellationToken).ConfigureAwait(false));
        var hiddenHash = CanonicalJson.HashJson(hiddenInput.HiddenConfiguration);
        if (hiddenInput.CampaignId != CampaignId || hiddenInput.Phase != "acquisition" ||
            hiddenInput.ConfigurationBlockId != configurationBlockId || hiddenInput.SeedId != seedId || hiddenHash != commitment)
            throw new InvalidDataException("Sealed evaluator payload no longer matches the prospective seed commitment.");
        ValidateHiddenResetAgainstObserved(hiddenInput.HiddenConfiguration, reset, verification);
        if (RequiredString(reset.GetProperty("material"), "base_head") != publicFixtureRevision)
            throw new InvalidDataException("Fixture main revision changed after the prospective seed commitment.");

        string? existingHiddenJson = null;
        string? existingHiddenFingerprint = null;
        var hiddenCount = 0;
        await using (var selectHidden = new NpgsqlCommand("""
            SELECT configuration::text,expected_reset_fingerprint FROM eval001.hidden_configuration
            WHERE seed_id=@seed ORDER BY recorded_at;
            """, connection))
        {
            selectHidden.Parameters.AddWithValue("seed", seedId);
            await using var reader = await selectHidden.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                hiddenCount++;
                existingHiddenJson = reader.GetString(0);
                existingHiddenFingerprint = reader.GetString(1);
            }
        }
        (Guid Generation, string Actual, string Expected, string Hashes, bool Passed, DateTimeOffset RecordedAt)? existingReset = null;
        var resetCount = 0;
        await using (var selectReset = new NpgsqlCommand("""
            SELECT generation_id,actual_fingerprint,expected_fingerprint,provider_evidence_hashes::text,passed,recorded_at
            FROM eval001.reset_verification WHERE seed_id=@seed AND arm='acquisition' ORDER BY recorded_at;
            """, connection))
        {
            selectReset.Parameters.AddWithValue("seed", seedId);
            await using var reader = await selectReset.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                resetCount++;
                existingReset = (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), ReadDbTimestamp(reader, 5));
            }
        }
        if (hiddenCount > 1 || resetCount > 1 || hiddenCount != resetCount)
            throw new InvalidDataException("Campaign 2 reset registration is partially duplicated or inconsistent.");
        DateTimeOffset registeredAt;
        if (hiddenCount == 1)
        {
            using var existingHidden = JsonDocument.Parse(existingHiddenJson!);
            using var existingHashes = JsonDocument.Parse(existingReset!.Value.Hashes);
            var hashSet = existingHashes.RootElement.EnumerateArray().Select(value => value.GetString()).ToHashSet(StringComparer.Ordinal);
            var row = existingReset.Value;
            if (CanonicalJson.HashJson(existingHidden.RootElement) != hiddenHash || existingHiddenFingerprint != expectedFingerprint ||
                row.Generation != generationId || row.Actual != actualFingerprint || row.Expected != expectedFingerprint || !row.Passed ||
                !hashSet.SetEquals(new[] { resetHash, verificationHash }))
                throw new InvalidDataException("Existing Campaign 2 reset registration differs from the verified retry input.");
            registeredAt = row.RecordedAt;
        }
        else
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var hidden = new NpgsqlCommand("""
                INSERT INTO eval001.hidden_configuration(
                  hidden_configuration_id,seed_id,regime_label,configuration,expected_reset_fingerprint,answer_key_version)
                VALUES (@id,@seed,'acquisition-action-slot',@configuration,@fingerprint,'campaign2-acquisition-schedule-v2');
                """, connection, transaction))
            {
                hidden.Parameters.AddWithValue("id", Guid.NewGuid());
                hidden.Parameters.AddWithValue("seed", seedId);
                KernelDb.AddJson(hidden, "configuration", hiddenInput.HiddenConfiguration);
                hidden.Parameters.AddWithValue("fingerprint", expectedFingerprint);
                await hidden.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO eval001.reset_verification(
                  reset_verification_id,seed_id,arm,generation_id,actual_fingerprint,expected_fingerprint,provider_evidence_hashes,passed)
                VALUES (@id,@seed,'acquisition',@generation,@actual,@expected,@hashes,true) RETURNING recorded_at;
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue("id", Guid.NewGuid());
                insert.Parameters.AddWithValue("seed", seedId);
                insert.Parameters.AddWithValue("generation", generationId);
                insert.Parameters.AddWithValue("actual", actualFingerprint);
                insert.Parameters.AddWithValue("expected", expectedFingerprint);
                KernelDb.AddJson(insert, "hashes", JsonSerializer.SerializeToElement(new[] { resetHash, verificationHash }, JsonDefaults.Options));
                registeredAt = ReadDbTimestamp(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new DataException("Reset registration did not return a record time."));
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        var record = new Campaign2ResetRegistrationRecord(
            ResetRegistrationRecordSchema, CampaignId, configurationBlockId, seedId, generationId,
            actualFingerprint, expectedFingerprint, resetHash, verificationHash, true, registeredAt);
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
            Path.Combine(root, "artifacts", "campaign-2r", "preflight", "preflight-gates.json"),
            "acquisition");
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2r");
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
        var recoveredBegin = await TryRecoverExistingBeginAsync(
            root, secretFile, input, resetBytes, reset, preBytes, pre, requestBytes, subjectBytes, prediction, cancellationToken).ConfigureAwait(false);
        if (recoveredBegin is not null)
        {
            await WriteNewAsync(outputFile, CanonicalJson.Serialize(recoveredBegin), cancellationToken).ConfigureAwait(false);
            return recoveredBegin;
        }

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
        preEvidence = await EnsureEvidenceAsync(database, preEvidence, cancellationToken).ConfigureAwait(false);
        requestEvidence = await EnsureEvidenceAsync(database, requestEvidence, cancellationToken).ConfigureAwait(false);
        subjectEvidence = await EnsureEvidenceAsync(database, subjectEvidence, cancellationToken).ConfigureAwait(false);

        var generationId = RequiredString(reset, "generation_id");
        var fingerprint = RequiredString(reset, "actual_fingerprint");
        var seed = RequiredString(reset, "seed_commitment_sha256");
        var localId = await EnsureLocalManifestationAsync(database, input, generationId, fingerprint, cancellationToken).ConfigureAwait(false);
        var remoteId = await EnsureRemoteManifestationAsync(database, cancellationToken).ConfigureAwait(false);
        var preRemoteLineage = await EnsureRemoteRefLineageAsync(database, store, remoteId, pre, cancellationToken).ConfigureAwait(false);

        var preObservationId = Guid.NewGuid();
        var preObservation = new ObservationRecord(
            preObservationId,
            localId,
            "campaign2-state-observer",
            "campaign2-state-observation-v1",
            "git/native",
            pre.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, local = true }, JsonDefaults.Options),
            pre.LocalHead,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "native-git-local" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(pre, JsonDefaults.Options),
            [preEvidence.EvidenceId]);
        preObservation = await EnsureObservationAsync(database, preObservation, cancellationToken).ConfigureAwait(false);
        preObservationId = preObservation.ObservationId;
        var preClaims = await InsertStateClaimsAsync(
            database, localId, remoteId, preObservationId, preEvidence.EvidenceId, pre,
            preRemoteLineage.Observation.ObservationId, preRemoteLineage.Evidence.EvidenceId,
            null, null, null, cancellationToken).ConfigureAwait(false);

        var correspondenceId = await InsertCorrespondenceAsync(
            database, localId, remoteId,
            [preObservationId, preRemoteLineage.Observation.ObservationId],
            [preEvidence.EvidenceId, preRemoteLineage.Evidence.EvidenceId], preClaims.Values,
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
            database, actionId, predictionId,
            [preObservationId, preRemoteLineage.Observation.ObservationId],
            [preEvidence.EvidenceId, preRemoteLineage.Evidence.EvidenceId, requestEvidence.EvidenceId, subjectEvidence.EvidenceId],
            cancellationToken).ConfigureAwait(false);

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
            return ReadDbTimestamp(value ?? throw new DataException("Dispatch phase timestamp is absent."));
        }, cancellationToken).ConfigureAwait(false);

        var record = new Campaign2BeginRecord(
            BeginRecordSchema,
            CampaignId,
            input.Phase,
            input.TrialId,
            input.ConfigurationBlockId,
            input.EvaluatorSeedId,
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
            preRemoteLineage.Observation.ObservationId,
            preRemoteLineage.Evidence.EvidenceId,
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
        var campaignRoot = Path.Combine(root, "experiments", "build001", "campaign-2r");
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
        var recoveredClose = await TryRecoverExistingCloseAsync(
            root, secretFile, begin, before, receiptBytes, postBytes, providerBytes, post, provider, resolved, prediction, score,
            outputFile, cancellationToken).ConfigureAwait(false);
        if (recoveredClose is not null)
        {
            await WriteNewAsync(outputFile, CanonicalJson.Serialize(recoveredClose), cancellationToken).ConfigureAwait(false);
            return recoveredClose;
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
        receiptEvidence = await EnsureEvidenceAsync(database, receiptEvidence, cancellationToken).ConfigureAwait(false);
        postEvidence = await EnsureEvidenceAsync(database, postEvidence, cancellationToken).ConfigureAwait(false);
        providerEvidence = await EnsureEvidenceAsync(database, providerEvidence, cancellationToken).ConfigureAwait(false);
        var postRemoteLineage = await EnsureRemoteRefLineageAsync(database, store, begin.RemoteManifestationId, post, cancellationToken).ConfigureAwait(false);
        var postObservationId = Guid.NewGuid();
        var postObservation = new ObservationRecord(
            postObservationId,
            begin.LocalManifestationId,
            "campaign2-state-observer",
            "campaign2-state-observation-v1",
            "git/native",
            post.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, local = true, locked_horizons = true }, JsonDefaults.Options),
            post.LocalHead,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "native-git-local" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(post, JsonDefaults.Options),
            [postEvidence.EvidenceId]);
        postObservation = await EnsureObservationAsync(database, postObservation, cancellationToken).ConfigureAwait(false);
        postObservationId = postObservation.ObservationId;
        var providerObservationId = Guid.NewGuid();
        var providerObservation = new ObservationRecord(
            providerObservationId,
            begin.RemoteManifestationId,
            "campaign2-provider-outcome-observer",
            "campaign2-provider-outcome-v1",
            "github/provider",
            provider.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, provider_native = true, presentation_or_check_only = true }, JsonDefaults.Options),
            null,
            null,
            JsonSerializer.SerializeToElement(new { dependency_group = "github-provider-outcome" }, JsonDefaults.Options),
            JsonSerializer.SerializeToElement(provider, JsonDefaults.Options),
            [providerEvidence.EvidenceId]);
        providerObservation = await EnsureObservationAsync(database, providerObservation, cancellationToken).ConfigureAwait(false);
        providerObservationId = providerObservation.ObservationId;
        var postClaims = await InsertStateClaimsAsync(
            database, begin.LocalManifestationId, begin.RemoteManifestationId, postObservationId, postEvidence.EvidenceId, post,
            postRemoteLineage.Observation.ObservationId, postRemoteLineage.Evidence.EvidenceId,
            provider, providerObservationId, providerEvidence.EvidenceId, cancellationToken).ConfigureAwait(false);
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
            await InsertOutcomeLinksAsync(connection, transaction, outcomeId,
                [postObservationId, postRemoteLineage.Observation.ObservationId, providerObservationId],
                [postEvidence.EvidenceId, postRemoteLineage.Evidence.EvidenceId, providerEvidence.EvidenceId, receiptEvidence.EvidenceId],
                token).ConfigureAwait(false);

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
                ) VALUES (@id,@trial,@block,@arm,@action,@prediction,@scope,@fingerprint,@versions,@closed);
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
                episode.Parameters.AddWithValue("closed", closedAt);
                await episode.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var link in new[]
                     {
                         ("episode_correspondence", "correspondence_id", begin.CorrespondenceId),
                         ("episode_pre_observation", "observation_id", begin.PreObservationId),
                         ("episode_pre_observation", "observation_id", begin.PreRemoteObservationId),
                         ("episode_post_observation", "observation_id", postObservationId),
                         ("episode_post_observation", "observation_id", postRemoteLineage.Observation.ObservationId),
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
        var preRemoteRefSha256 = CanonicalJson.Sha256(CanonicalJson.Serialize(BuildRemoteRefProviderPayload(before)));
        var localManifestationRef = $"git:working-copy:{begin.TrialId}";
        var publicClaims = BuildPublicClaimExports(
                begin.PreClaimIds, before, null, localManifestationRef, preObservationSha256, preRemoteRefSha256, null, "historical_pre_reobservation")
            .Concat(BuildPublicClaimExports(
                postClaims, post, provider, localManifestationRef, postEvidence.ContentHash, postRemoteLineage.Evidence.ContentHash, providerEvidence.ContentHash, "supported_at_episode_close"))
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
            [preObservationSha256, preRemoteRefSha256]);
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
            new[] { preObservationSha256, preRemoteRefSha256, receiptEvidence.ContentHash, postEvidence.ContentHash, postRemoteLineage.Evidence.ContentHash, providerEvidence.ContentHash }
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
        var output = EnsureUnder(Path.Combine(root, "experiments", "build001", "campaign-2r"), outputPath);
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

    private static async Task<Campaign2CloseRecord?> TryRecoverExistingCloseAsync(
        string root,
        string secretFile,
        Campaign2BeginRecord begin,
        Campaign2StateObservation before,
        byte[] receiptBytes,
        byte[] postBytes,
        byte[] providerBytes,
        Campaign2StateObservation post,
        Campaign2ProviderOutcome provider,
        Campaign2ResolvedOutcome resolved,
        IReadOnlyDictionary<string, double?> prediction,
        PredictionScore score,
        string outputFile,
        CancellationToken cancellationToken)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            ExistingCloseState? state = null;
            await using (var command = new NpgsqlCommand("""
                SELECT e.episode_id,eo.outcome_id,ee.evaluation_id,pe.eligibility_status,pe.mean_brier_loss,
                       pe.brier_components::text,o.actual_propositions::text,e.closed_at
                FROM wk.transition_episode e
                JOIN wk.episode_outcome eo ON eo.episode_id=e.episode_id
                JOIN wk.outcome o ON o.outcome_id=eo.outcome_id
                JOIN wk.episode_evaluation ee ON ee.episode_id=e.episode_id
                JOIN wk.prediction_evaluation pe ON pe.evaluation_id=ee.evaluation_id
                WHERE e.action_id=@action;
                """, connection))
            {
                command.Parameters.AddWithValue("action", begin.ActionId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    state = new ExistingCloseState(
                        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDouble(4),
                        reader.GetString(5), reader.GetString(6), ReadDbTimestamp(reader, 7),
                        new Dictionary<string, Guid>(StringComparer.Ordinal));
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                        throw new InvalidDataException("Campaign 2 Action has duplicate closed TransitionEpisodes.");
                }
            }
            if (state is null) return null;
            var postClaims = new Dictionary<string, Guid>(StringComparer.Ordinal);
            await using (var command = new NpgsqlCommand("""
                SELECT c.predicate_namespace || ':' || c.predicate,c.claim_id
                FROM wk.claim c
                WHERE c.primary_observation_id IN (
                  SELECT observation_id FROM wk.episode_post_observation WHERE episode_id=@episode)
                ORDER BY c.recorded_at;
                """, connection))
            {
                command.Parameters.AddWithValue("episode", state.EpisodeId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false)) postClaims[reader.GetString(0)] = reader.GetGuid(1);
            }
            return state with { PostClaimIds = postClaims };
        }, cancellationToken).ConfigureAwait(false);
        if (existing is null) return null;

        using (var actualDocument = JsonDocument.Parse(existing.ActualPropositionsJson))
        using (var brierDocument = JsonDocument.Parse(existing.BrierComponentsJson))
        {
            if (existing.EligibilityStatus != score.EligibilityStatus ||
                Math.Abs(existing.MeanBrierLoss - score.MeanBrierLoss!.Value) > 1e-12 ||
                CanonicalJson.HashJson(actualDocument.RootElement) != CanonicalJson.HashJson(JsonSerializer.SerializeToElement(resolved.ActualPropositions, JsonDefaults.Options)) ||
                CanonicalJson.HashJson(brierDocument.RootElement) != CanonicalJson.HashJson(JsonSerializer.SerializeToElement(score.BrierComponents, JsonDefaults.Options)))
                throw new InvalidDataException("Existing closed Campaign 2 episode differs from fresh independently resolved evidence.");
        }
        if (existing.PostClaimIds.Count == 0)
            throw new InvalidDataException("Existing closed Campaign 2 episode is missing typed post-action Claims.");

        var preHash = CanonicalJson.Sha256(await File.ReadAllBytesAsync(begin.PreObservationPath, cancellationToken).ConfigureAwait(false));
        var receiptHash = CanonicalJson.Sha256(receiptBytes);
        var postHash = CanonicalJson.Sha256(postBytes);
        var providerHash = CanonicalJson.Sha256(providerBytes);
        var preRemoteRefHash = CanonicalJson.Sha256(CanonicalJson.Serialize(BuildRemoteRefProviderPayload(before)));
        var postRemoteRefHash = CanonicalJson.Sha256(CanonicalJson.Serialize(BuildRemoteRefProviderPayload(post)));
        await RecordEvaluatorGroundTruthAsync(
            secretFile, begin, resolved, postHash, providerHash, provider.ObservedAt, cancellationToken).ConfigureAwait(false);

        var localManifestationRef = $"git:working-copy:{begin.TrialId}";
        var publicClaims = BuildPublicClaimExports(
                begin.PreClaimIds, before, null, localManifestationRef, preHash, preRemoteRefHash, null, "historical_pre_reobservation")
            .Concat(BuildPublicClaimExports(
                existing.PostClaimIds, post, provider, localManifestationRef, postHash, postRemoteRefHash, providerHash, "supported_at_episode_close"))
            .ToArray();
        var publicCorrespondence = new PublicCorrespondenceExport(
            begin.CorrespondenceId, localManifestationRef, "git:working_copy_of", FixtureManifestationRef, "candidate", 1.0,
            before.ObservedAt, before.ObservedAt, [preHash, preRemoteRefHash]);
        var publicEpisode = new EpisodeExport(
            existing.EpisodeId,
            begin.SemanticAction,
            FixtureManifestationRef,
            before.PublicTopologyClass,
            existing.ClosedAt,
            BuildPublicObservedFacts(before),
            prediction.Where(value => value.Value is not null).ToDictionary(value => value.Key, value => value.Value!.Value, StringComparer.Ordinal),
            resolved.ActualPropositions,
            score.BrierComponents,
            score.MeanBrierLoss,
            resolved.ActualDeltas,
            score.InvariantViolations,
            "verified",
            publicClaims,
            [publicCorrespondence],
            new[] { preHash, preRemoteRefHash, receiptHash, postHash, postRemoteRefHash, providerHash }.Distinct(StringComparer.Ordinal).ToArray(),
            ProviderVersionFingerprint);
        var publicEpisodePath = Path.Combine(Path.GetDirectoryName(outputFile)!, "episode-public.json");
        var expectedBytes = CanonicalJson.Serialize(publicEpisode);
        var expectedHash = CanonicalJson.Sha256(expectedBytes);
        if (File.Exists(publicEpisodePath))
        {
            var actualBytes = await File.ReadAllBytesAsync(publicEpisodePath, cancellationToken).ConfigureAwait(false);
            if (CanonicalJson.Sha256(actualBytes) != expectedHash)
                throw new InvalidDataException("Existing public Campaign 2 episode differs from recovered durable state.");
        }
        else
        {
            await WriteNewAsync(publicEpisodePath, expectedBytes, cancellationToken).ConfigureAwait(false);
        }
        return new Campaign2CloseRecord(
            CloseRecordSchema, CampaignId, begin.TrialId, begin.ConfigurationBlockId, begin.SemanticAction,
            begin.ActionId, begin.PredictionId, existing.OutcomeId, existing.EvaluationId, existing.EpisodeId,
            existing.EligibilityStatus, existing.MeanBrierLoss, resolved.ActualPropositions, score.BrierComponents, resolved.ActualDeltas,
            receiptHash, postHash, providerHash, publicEpisodePath, expectedHash, existing.ClosedAt);
    }
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
        var rows = new List<(string Actual, string Deltas, string Invariants, string Hashes, string Resolver)>();
        await using (var select = new NpgsqlCommand("""
            SELECT actual_propositions::text,actual_deltas::text,actual_invariants::text,provider_evidence_hashes::text,resolver_version
            FROM eval001.ground_truth
            WHERE action_id=@action AND configuration_block_id=@block AND horizon_id='locked'
            ORDER BY recorded_at;
            """, connection))
        {
            select.Parameters.AddWithValue("action", begin.ActionId);
            select.Parameters.AddWithValue("block", begin.ConfigurationBlockId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }
        var actualElement = JsonSerializer.SerializeToElement(resolved.ActualPropositions, JsonDefaults.Options);
        var deltaElement = JsonSerializer.SerializeToElement(resolved.ActualDeltas.ToDictionary(value => value, _ => true, StringComparer.Ordinal), JsonDefaults.Options);
        var invariantElement = JsonSerializer.SerializeToElement(resolved.ViolatedInvariants.ToDictionary(value => value, _ => false, StringComparer.Ordinal), JsonDefaults.Options);
        var hashElement = JsonSerializer.SerializeToElement(new[] { postObservationSha256, providerOutcomeSha256 }, JsonDefaults.Options);
        if (rows.Count > 1) throw new InvalidDataException("Evaluator ground truth is duplicated for one Campaign 2 Action.");
        if (rows.Count == 1)
        {
            using var actual = JsonDocument.Parse(rows[0].Actual);
            using var deltas = JsonDocument.Parse(rows[0].Deltas);
            using var invariants = JsonDocument.Parse(rows[0].Invariants);
            using var hashes = JsonDocument.Parse(rows[0].Hashes);
            if (rows[0].Resolver != "campaign2-outcome-resolver-v1" ||
                CanonicalJson.HashJson(actual.RootElement) != CanonicalJson.HashJson(actualElement) ||
                CanonicalJson.HashJson(deltas.RootElement) != CanonicalJson.HashJson(deltaElement) ||
                CanonicalJson.HashJson(invariants.RootElement) != CanonicalJson.HashJson(invariantElement) ||
                CanonicalJson.HashJson(hashes.RootElement) != CanonicalJson.HashJson(hashElement))
                throw new InvalidDataException("Existing evaluator ground truth differs from fresh provider reobservation.");
            return;
        }
        await using var command = new NpgsqlCommand("""
            INSERT INTO eval001.ground_truth(
              ground_truth_id,action_id,configuration_block_id,horizon_id,actual_propositions,actual_deltas,
              actual_invariants,provider_evidence_hashes,resolver_version,resolved_at)
            VALUES (@id,@action,@block,'locked',@actual,@deltas,@invariants,@hashes,'campaign2-outcome-resolver-v1',@resolved);
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("action", begin.ActionId);
        command.Parameters.AddWithValue("block", begin.ConfigurationBlockId);
        KernelDb.AddJson(command, "actual", actualElement);
        KernelDb.AddJson(command, "deltas", deltaElement);
        KernelDb.AddJson(command, "invariants", invariantElement);
        KernelDb.AddJson(command, "hashes", hashElement);
        command.Parameters.AddWithValue("resolved", resolvedAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
    private static async Task<Campaign2BeginRecord?> TryRecoverExistingBeginAsync(
        string root,
        string secretFile,
        Campaign2BeginInput input,
        byte[] resetBytes,
        JsonElement reset,
        byte[] preBytes,
        Campaign2StateObservation pre,
        byte[] requestBytes,
        byte[] subjectBytes,
        IReadOnlyDictionary<string, double?> prediction,
        CancellationToken cancellationToken)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        return await database.WithConnectionAsync(async (connection, token) =>
        {
            var actions = new List<(Guid ActionId, string Targets, string ParametersHash, string SemanticAction)>();
            await using (var command = new NpgsqlCommand("""
                SELECT action_id,target_manifestations::text,parameters_hash,
                       semantic_action_namespace || ':' || semantic_action_type
                FROM wk.action_attempt
                WHERE trial_id=@trial AND configuration_block_id=@block AND arm='acquisition'
                ORDER BY recorded_at;
                """, connection))
            {
                command.Parameters.AddWithValue("trial", input.TrialId);
                command.Parameters.AddWithValue("block", input.ConfigurationBlockId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    actions.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
            if (actions.Count == 0) return null;
            if (actions.Count != 1) throw new InvalidDataException("Campaign 2 begin recovery found duplicate durable Actions for one trial.");
            var action = actions[0];
            if (action.SemanticAction != input.SemanticAction || action.ParametersHash != CanonicalJson.HashJson(input.Parameters))
                throw new InvalidDataException("Existing Campaign 2 Action differs from the sealed retry input.");
            using var targetsDocument = JsonDocument.Parse(action.Targets);
            var targets = targetsDocument.RootElement.EnumerateArray().Select(value => value.GetGuid()).ToArray();
            if (targets.Length != 2) throw new InvalidDataException("Existing Campaign 2 Action has an unexpected target set.");
            var localId = targets[0];
            var remoteId = targets[1];

            async Task<Guid> FindEvidenceAsync(string hash, string observer, string method)
            {
                await using var command = new NpgsqlCommand("""
                    SELECT evidence_id FROM wk.evidence
                    WHERE content_hash=@hash AND observer_name=@observer AND acquisition_method=@method
                    ORDER BY recorded_at LIMIT 1;
                    """, connection);
                command.Parameters.AddWithValue("hash", hash);
                command.Parameters.AddWithValue("observer", observer);
                command.Parameters.AddWithValue("method", method);
                return await command.ExecuteScalarAsync(token).ConfigureAwait(false) is Guid id
                    ? id : throw new InvalidDataException($"Campaign 2 recovery is missing durable {observer}/{method} evidence.");
            }
            var preEvidenceId = await FindEvidenceAsync(CanonicalJson.Sha256(preBytes), "campaign2-state-observer", "fresh-pre-dispatch-observation").ConfigureAwait(false);
            var preRemoteEvidenceId = await FindEvidenceAsync(CanonicalJson.Sha256(CanonicalJson.Serialize(BuildRemoteRefProviderPayload(pre))), "campaign2-github-ref-observer", "git-ls-remote-exact-hosted-ref").ConfigureAwait(false);
            var requestEvidenceId = await FindEvidenceAsync(CanonicalJson.Sha256(requestBytes), "campaign2-request-builder", "locked-subject-request").ConfigureAwait(false);
            var subjectEvidenceId = await FindEvidenceAsync(CanonicalJson.Sha256(subjectBytes), "campaign2-subject-adapter", "fresh-temporary-chat").ConfigureAwait(false);

            Guid preObservationId;
            await using (var command = new NpgsqlCommand("""
                SELECT observation_id FROM wk.observation
                WHERE target_manifestation_id=@target AND observer_name='campaign2-state-observer' AND observed_at=@observed
                ORDER BY recorded_at LIMIT 1;
                """, connection))
            {
                command.Parameters.AddWithValue("target", localId);
                command.Parameters.AddWithValue("observed", pre.ObservedAt);
                preObservationId = await command.ExecuteScalarAsync(token).ConfigureAwait(false) is Guid id
                    ? id : throw new InvalidDataException("Campaign 2 recovery is missing the pre-dispatch Observation.");
            }

            Guid preRemoteObservationId;
            await using (var command = new NpgsqlCommand("""
                SELECT observation_id FROM wk.observation
                WHERE target_manifestation_id=@target AND observer_name='campaign2-github-ref-observer' AND observed_at=@observed
                ORDER BY recorded_at LIMIT 1;
                """, connection))
            {
                command.Parameters.AddWithValue("target", remoteId);
                command.Parameters.AddWithValue("observed", pre.ObservedAt);
                preRemoteObservationId = await command.ExecuteScalarAsync(token).ConfigureAwait(false) is Guid id
                    ? id : throw new InvalidDataException("Campaign 2R recovery is missing the hosted-ref pre-dispatch Observation.");
            }

            var preClaims = new Dictionary<string, Guid>(StringComparer.Ordinal);
            await using (var command = new NpgsqlCommand("""
                SELECT predicate_namespace || ':' || predicate, claim_id
                FROM wk.claim WHERE primary_observation_id = ANY(@observations) ORDER BY recorded_at;
                """, connection))
            {
                command.Parameters.AddWithValue("observations", new[] { preObservationId, preRemoteObservationId });
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false)) preClaims[reader.GetString(0)] = reader.GetGuid(1);
            }
            if (preClaims.Count == 0) throw new InvalidDataException("Campaign 2 recovery is missing typed pre-action Claims.");

            var resetManifestSha256 = CanonicalJson.Sha256(resetBytes);
            Guid correspondenceId;
            await using (var command = new NpgsqlCommand("""
                SELECT correspondence_id FROM wk.correspondence_claim
                WHERE left_manifestation_id=@left AND right_manifestation_id=@right
                  AND relation_namespace='git' AND relation_type='working_copy_of' AND basis_fingerprint=@basis
                ORDER BY recorded_at LIMIT 1;
                """, connection))
            {
                command.Parameters.AddWithValue("left", localId);
                command.Parameters.AddWithValue("right", remoteId);
                command.Parameters.AddWithValue("basis", resetManifestSha256);
                correspondenceId = await command.ExecuteScalarAsync(token).ConfigureAwait(false) is Guid id
                    ? id : throw new InvalidDataException("Campaign 2 recovery is missing the conservative correspondence record.");
            }

            var normalized = Build001Contract.NormalizePrediction(input.SemanticAction, prediction, out var defects);
            if (defects.Count != 0) throw new InvalidDataException("Recovery prediction no longer satisfies the locked vector.");
            Guid predictionId;
            string? storedPrediction = null;
            await using (var command = new NpgsqlCommand("SELECT prediction_id,outcome_probabilities::text FROM wk.prediction WHERE action_id=@action;", connection))
            {
                command.Parameters.AddWithValue("action", action.ActionId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    predictionId = reader.GetGuid(0);
                    storedPrediction = reader.GetString(1);
                    if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("Campaign 2 Action has duplicate Predictions.");
                }
                else predictionId = Guid.Empty;
            }
            var producer = JsonSerializer.SerializeToElement(new
            {
                product = "ChatGPT web",
                selected_model = "5.6 Sol",
                reasoning_selection = "Extra High",
                temporary_chat = true,
                subject_result_sha256 = CanonicalJson.Sha256(subjectBytes),
                request_sha256 = CanonicalJson.Sha256(requestBytes)
            }, JsonDefaults.Options);
            if (predictionId == Guid.Empty)
            {
                predictionId = Guid.NewGuid();
                var declaration = new PredictionDeclaration(
                    predictionId, action.ActionId, input.SemanticAction,
                    normalized.ToDictionary(value => value.Key, value => (double?)value.Value, StringComparer.Ordinal),
                    JsonDefaults.EmptyArray, JsonDefaults.EmptyArray, Build001Contract.DefaultHorizons(),
                    "fresh-campaign2-temporary-chat", Campaign2Attestation.FreshInvocationMethodVersion, producer);
                var predictionDefects = await database.CommitPredictionAsync(declaration, token).ConfigureAwait(false);
                if (predictionDefects.Count != 0) throw new InvalidDataException("Recovered Prediction failed the locked vector.");
            }
            else
            {
                using var storedDocument = JsonDocument.Parse(storedPrediction!);
                var expectedHash = CanonicalJson.HashJson(JsonSerializer.SerializeToElement(normalized, JsonDefaults.Options));
                if (CanonicalJson.HashJson(storedDocument.RootElement) != expectedHash)
                    throw new InvalidDataException("Existing durable Prediction differs from the sealed subject output.");
            }
            await InsertPredictionLineageAsync(database, action.ActionId, predictionId,
                [preObservationId, preRemoteObservationId],
                [preEvidenceId, preRemoteEvidenceId, requestEvidenceId, subjectEvidenceId], token).ConfigureAwait(false);

            Guid dispatchPhaseId = Guid.Empty;
            DateTimeOffset dispatchedAt = default;
            var dispatchCount = 0;
            await using (var command = new NpgsqlCommand("SELECT action_phase_id,recorded_at FROM wk.action_phase WHERE action_id=@action AND phase='dispatched' ORDER BY recorded_at;", connection))
            {
                command.Parameters.AddWithValue("action", action.ActionId);
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    dispatchCount++;
                    dispatchPhaseId = reader.GetGuid(0);
                    dispatchedAt = ReadDbTimestamp(reader, 1);
                }
            }
            if (dispatchCount > 1) throw new InvalidDataException("Campaign 2 Action has duplicate dispatch seals.");
            if (dispatchCount == 0)
            {
                dispatchPhaseId = await database.SealDispatchAsync(
                    action.ActionId, input.Parameters,
                    JsonSerializer.SerializeToElement(new
                    {
                        campaign_id = CampaignId,
                        trial_id = input.TrialId,
                        subject_result_sha256 = CanonicalJson.Sha256(subjectBytes),
                        request_sha256 = CanonicalJson.Sha256(requestBytes),
                        prediction_recorded_before_dispatch = true,
                        recovered_begin = true
                    }, JsonDefaults.Options), token).ConfigureAwait(false);
                await using var command = new NpgsqlCommand("SELECT recorded_at FROM wk.action_phase WHERE action_phase_id=@id;", connection);
                command.Parameters.AddWithValue("id", dispatchPhaseId);
                dispatchedAt = ReadDbTimestamp(await command.ExecuteScalarAsync(token).ConfigureAwait(false)
                    ?? throw new DataException("Recovered dispatch phase timestamp is absent."));
            }
            return new Campaign2BeginRecord(
                BeginRecordSchema, CampaignId, input.Phase, input.TrialId, input.ConfigurationBlockId, input.EvaluatorSeedId,
                input.Arm, input.SemanticAction, input.Target, input.Parameters, input.WorkingCopy, input.ResetBranch, input.Branch,
                RequiredString(reset, "actual_fingerprint"), RequiredString(reset, "seed_commitment_sha256"), GetFreezeManifestSha256(root),
                localId, remoteId, correspondenceId, preObservationId, preEvidenceId, preRemoteObservationId, preRemoteEvidenceId, preClaims, resetManifestSha256,
                requestEvidenceId, subjectEvidenceId, action.ActionId, predictionId, dispatchPhaseId, dispatchedAt,
                input.ResetManifestPath, input.PreObservationPath, input.SubjectRequestPath, input.SubjectResultPath,
                CanonicalJson.Sha256(subjectBytes), dispatchedAt);
        }, cancellationToken).ConfigureAwait(false);
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
        string localRef, string stateEvidenceHash, string remoteRefEvidenceHash, string? providerEvidenceHash, string disposition)
    {
        var result = new List<PublicClaimExport>();
        foreach (var pair in ids.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            var remoteRefSpecific = pair.Key == "github:remote_ref_head";
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
            var evidenceHash = remoteRefSpecific ? remoteRefEvidenceHash : providerSpecific ? providerEvidenceHash! : stateEvidenceHash;
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

    private static async Task<EvidenceRecord> EnsureEvidenceAsync(
        KernelDb database,
        EvidenceRecord candidate,
        CancellationToken cancellationToken)
    {
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            var ids = new List<Guid>();
            await using var command = new NpgsqlCommand("""
                SELECT evidence_id FROM wk.evidence
                WHERE content_hash=@hash AND provider_namespace=@provider AND observer_name=@observer
                  AND acquisition_method=@method AND captured_at=@captured
                ORDER BY recorded_at;
                """, connection);
            command.Parameters.AddWithValue("hash", candidate.ContentHash);
            command.Parameters.AddWithValue("provider", candidate.ProviderNamespace);
            command.Parameters.AddWithValue("observer", candidate.ObserverName);
            command.Parameters.AddWithValue("method", candidate.AcquisitionMethod);
            command.Parameters.AddWithValue("captured", candidate.CapturedAt);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) ids.Add(reader.GetGuid(0));
            return ids;
        }, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 1) throw new InvalidDataException("Campaign 2 evidence is duplicated for identical content/provenance.");
        if (existing.Count == 1) return candidate with { EvidenceId = existing[0] };
        await database.InsertEvidenceAsync(candidate, cancellationToken).ConfigureAwait(false);
        return candidate;
    }

    private static async Task<Guid> EnsureLocalManifestationAsync(
        KernelDb database,
        Campaign2BeginInput input,
        string generationId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var incarnation = $"campaign2:{input.TrialId}:{generationId}";
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            var ids = new List<Guid>();
            await using var command = new NpgsqlCommand("""
                SELECT manifestation_id FROM wk.manifestation
                WHERE provider_namespace='codeeye/git-local' AND incarnation_key=@incarnation
                ORDER BY recorded_at;
                """, connection);
            command.Parameters.AddWithValue("incarnation", incarnation);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) ids.Add(reader.GetGuid(0));
            return ids;
        }, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 1) throw new InvalidDataException("Campaign 2 local manifestation incarnation is duplicated.");
        if (existing.Count == 1) return existing[0];
        var id = Guid.NewGuid();
        await database.InsertManifestationAsync(new ManifestationRecord(
            id,
            "codeeye/git-local",
            "git-working-copy",
            JsonSerializer.SerializeToElement(new
            {
                fixture_repository = FixtureRepository,
                working_copy = Path.GetFullPath(input.WorkingCopy),
                generation_id = generationId,
                environment_fingerprint = fingerprint
            }, JsonDefaults.Options),
            incarnation,
            null,
            JsonSerializer.SerializeToElement(new { reset_generation_id = generationId }, JsonDefaults.Options),
            input.WorkingCopy), cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static async Task<ObservationRecord> EnsureObservationAsync(
        KernelDb database,
        ObservationRecord candidate,
        CancellationToken cancellationToken)
    {
        var rows = await database.WithConnectionAsync(async (connection, token) =>
        {
            var result = new List<(Guid Id, string Payload)>();
            await using var command = new NpgsqlCommand("""
                SELECT observation_id,raw_normalized_payload::text FROM wk.observation
                WHERE target_manifestation_id=@target AND observer_name=@observer AND observed_at=@observed
                ORDER BY recorded_at;
                """, connection);
            command.Parameters.AddWithValue("target", candidate.TargetManifestationId);
            command.Parameters.AddWithValue("observer", candidate.ObserverName);
            command.Parameters.AddWithValue("observed", candidate.ObservedAt);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) result.Add((reader.GetGuid(0), reader.GetString(1)));
            return result;
        }, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1) throw new InvalidDataException("Campaign 2 Observation is duplicated for one target/time/observer.");
        if (rows.Count == 1)
        {
            using var payload = JsonDocument.Parse(rows[0].Payload);
            if (candidate.RawNormalizedPayload is not JsonElement rawPayload ||
                CanonicalJson.HashJson(payload.RootElement) != CanonicalJson.HashJson(rawPayload))
                throw new InvalidDataException("Existing Campaign 2 Observation differs from the retry artifact.");
            return candidate with { ObservationId = rows[0].Id };
        }
        await database.InsertObservationAsync(candidate, cancellationToken).ConfigureAwait(false);
        return candidate;
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
        Campaign2StateObservation state, Guid remoteRefObservationId, Guid remoteRefEvidenceId,
        Campaign2ProviderOutcome? provider, Guid? providerObservationId, Guid? providerEvidenceId, CancellationToken cancellationToken)
    {
        var claims = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var expectedKeys = new HashSet<string>(new[]
        {
            "git:local_head", "git:current_branch", "git:worktree_clean", "git:remote_tracking_head",
            "git:remote_url", "github:remote_ref_head", "git:public_topology_class"
        }, StringComparer.Ordinal);
        if (provider is not null)
        {
            expectedKeys.Add("github:check_started");
            expectedKeys.Add("github:check_terminal_success");
            expectedKeys.Add("github:browser_presented_head");
        }
        var observationIds = providerObservationId is Guid providerObservation
            ? new[] { localObservationId, remoteRefObservationId, providerObservation }
            : new[] { localObservationId, remoteRefObservationId };
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
            await using var command = new NpgsqlCommand("""
                SELECT predicate_namespace || ':' || predicate,claim_id
                FROM wk.claim WHERE primary_observation_id = ANY(@observations) ORDER BY recorded_at;
                """, connection);
            command.Parameters.AddWithValue("observations", observationIds);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) result[reader.GetString(0)] = reader.GetGuid(1);
            return result;
        }, cancellationToken).ConfigureAwait(false);
        if (existing.Count != 0)
        {
            if (!existing.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedKeys))
                throw new InvalidDataException("Existing Campaign 2 typed Claims are incomplete or differ from the retry observation.");
            return existing;
        }
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
            await AddAsync("github:remote_ref_head", remoteId, "github", "remote_ref_head", JsonSerializer.SerializeToElement(state.RemoteHead), "observed", "provider", state.ObservedAt, remoteRefObservationId, remoteRefEvidenceId).ConfigureAwait(false);
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
        IReadOnlyList<Guid> observationIds,
        IReadOnlyList<Guid> evidenceIds,
        IEnumerable<Guid> basisClaimIds,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var existing = await database.WithConnectionAsync(async (connection, token) =>
        {
            var ids = new List<Guid>();
            await using var command = new NpgsqlCommand("""
                SELECT correspondence_id FROM wk.correspondence_claim
                WHERE left_manifestation_id=@left AND right_manifestation_id=@right
                  AND relation_namespace='git' AND relation_type='working_copy_of' AND basis_fingerprint=@fingerprint
                ORDER BY recorded_at;
                """, connection);
            command.Parameters.AddWithValue("left", localId);
            command.Parameters.AddWithValue("right", remoteId);
            command.Parameters.AddWithValue("fingerprint", fingerprint);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) ids.Add(reader.GetGuid(0));
            return ids;
        }, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 1) throw new InvalidDataException("Campaign 2 correspondence is duplicated for one reset incarnation.");
        if (existing.Count == 1) return existing[0];
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
            foreach (var observationId in observationIds.Distinct())
            {
                await using var observation = new NpgsqlCommand(
                    "INSERT INTO wk.correspondence_observation(correspondence_id,observation_id) VALUES (@id,@value);", connection, transaction);
                observation.Parameters.AddWithValue("id", id);
                observation.Parameters.AddWithValue("value", observationId);
                await observation.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var evidenceId in evidenceIds.Distinct())
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
        IReadOnlyList<Guid> observationIds,
        IReadOnlyList<Guid> evidenceIds,
        CancellationToken cancellationToken)
    {
        await database.WithConnectionAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            foreach (var observationId in observationIds.Distinct())
            {
                await using var precondition = new NpgsqlCommand(
                    "INSERT INTO wk.action_precondition_observation(action_id,observation_id) VALUES (@action,@observation) ON CONFLICT DO NOTHING;", connection, transaction);
                precondition.Parameters.AddWithValue("action", actionId);
                precondition.Parameters.AddWithValue("observation", observationId);
                await precondition.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            foreach (var evidenceId in evidenceIds)
            {
                await using var evidence = new NpgsqlCommand(
                    "INSERT INTO wk.prediction_basis_evidence(prediction_id,evidence_id) VALUES (@prediction,@evidence) ON CONFLICT DO NOTHING;", connection, transaction);
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
        var path = Path.Combine(root, "experiments", "build001", "campaign-2r", "preregistration-freeze-manifest.json");
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
        var path = Path.Combine(root, "experiments", "build001", "campaign-2r", "preregistration-freeze-manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var value = document.RootElement;
        var initialProspectiveFreeze = value.TryGetProperty("frozen_before_acquisition", out var initialFreezeValue) &&
                                       initialFreezeValue.ValueKind == JsonValueKind.True;
        var preSubjectRepairFreeze = value.TryGetProperty("repair_after_seed_registration", out var repairValue) &&
                                     repairValue.ValueKind == JsonValueKind.True &&
                                     value.TryGetProperty("frozen_before_first_subject_invocation", out var beforeSubjectValue) &&
                                     beforeSubjectValue.ValueKind == JsonValueKind.True &&
                                     value.TryGetProperty("scientific_outcomes_observed", out var outcomesValue) &&
                                     outcomesValue.ValueKind == JsonValueKind.False;
        if (RequiredString(value, "schema") != FreezeManifestSchema || RequiredString(value, "campaign_id") != CampaignId ||
            !RequiredBoolean(value, "valid") || (!initialProspectiveFreeze && !preSubjectRepairFreeze))
            throw new InvalidDataException("Campaign 2 execution freeze manifest is not valid and prospective.");
        if (preSubjectRepairFreeze)
        {
            var supersededFreeze = RequiredString(value, "supersedes_execution_freeze_commit");
            if (supersededFreeze.Length != 40 || supersededFreeze.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("Campaign 2 repair freeze does not identify the superseded prospective freeze commit.");
        }
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
            string.IsNullOrWhiteSpace(input.ConfigurationBlockId) || string.IsNullOrWhiteSpace(input.EvaluatorSeedId) ||
            string.IsNullOrWhiteSpace(input.Target))
        {
            throw new InvalidDataException("Campaign 2 begin input is incomplete.");
        }
    }

    private static void ValidateHiddenResetAgainstObserved(
        JsonElement hidden,
        JsonElement reset,
        JsonElement verification)
    {
        if (RequiredString(hidden, "schedule_version") != "campaign2-acquisition-action-slot-v2")
            throw new InvalidDataException("Hidden acquisition schedule version is invalid.");
        var resetBlockId = RequiredString(hidden, "reset_block_id");
        var seedId = RequiredString(hidden, "seed_id");
        var branch = RequiredString(hidden, "branch");
        var browserFreshness = RequiredString(hidden, "browser_freshness");
        if (RequiredString(reset, "block_id") != resetBlockId ||
            RequiredString(reset.GetProperty("material"), "branch") != branch ||
            RequiredString(verification, "branch") != branch ||
            RequiredString(reset.GetProperty("material"), "browser_freshness_setup") != browserFreshness ||
            RequiredString(verification, "browser_freshness") != browserFreshness)
            throw new InvalidDataException("Reset material differs from the sealed action-slot schedule.");
        var expectedSeedHash = CanonicalJson.Sha256Utf8($"acquisition|{resetBlockId}|{seedId}");
        if (RequiredString(reset, "seed_commitment_sha256") != expectedSeedHash)
            throw new InvalidDataException("Fixture reset seed commitment does not match the sealed action-slot seed.");
        var material = reset.GetProperty("material");
        if (RequiredString(material, "repository") != FixtureRepository ||
            material.GetProperty("provider_native_repository_id").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture) != FixtureNativeId)
            throw new InvalidDataException("Reset escaped the frozen fixture provider identity.");
        var push = material.GetProperty("push_policy");
        switch (RequiredString(hidden, "push_regime"))
        {
            case "accepted":
                if (RequiredBoolean(push, "branch_protected") || RequiredInt32(push, "required_approving_reviews") != 0 || RequiredBoolean(push, "admins_enforced"))
                    throw new InvalidDataException("Accepted push reset did not materialize an unprotected fixture branch.");
                break;
            case "rejected_by_provider_policy":
                if (!RequiredBoolean(push, "branch_protected") || RequiredInt32(push, "required_approving_reviews") < 1 || !RequiredBoolean(push, "admins_enforced"))
                    throw new InvalidDataException("Rejected push reset did not materialize real provider protection.");
                break;
            default:
                throw new InvalidDataException("Unknown sealed push regime.");
        }
        var check = material.GetProperty("check_provider");
        switch (RequiredString(hidden, "check_regime"))
        {
            case "no_check":
                if (string.Equals(RequiredString(check, "workflow_state"), "active", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("No-check reset left the fixture workflow active.");
                break;
            case "success":
            case "failure":
                if (!string.Equals(RequiredString(check, "workflow_state"), "active", StringComparison.OrdinalIgnoreCase) ||
                    !RequiredBoolean(check, "encrypted_check_secret_present"))
                    throw new InvalidDataException("Check-enabled reset did not materialize the provider workflow/secret state.");
                break;
            default:
                throw new InvalidDataException("Unknown sealed check regime.");
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
            SELECT s.seed_id, r.actual_fingerprint, r.expected_fingerprint, r.passed, h.expected_reset_fingerprint
            FROM eval001.seed_commitment s
            JOIN eval001.hidden_configuration h ON h.seed_id=s.seed_id
            JOIN eval001.reset_verification r ON r.seed_id=s.seed_id
            WHERE s.phase='acquisition' AND s.configuration_block_id=@block AND s.seed_id=@seed
              AND r.arm='acquisition' AND r.generation_id=@generation;
            """, connection);
        command.Parameters.AddWithValue("block", input.ConfigurationBlockId);
        command.Parameters.AddWithValue("seed", input.EvaluatorSeedId);
        command.Parameters.AddWithValue("generation", generationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.GetString(0) != input.EvaluatorSeedId || reader.GetString(1) != fingerprint ||
            reader.GetString(2) != fingerprint || !reader.GetBoolean(3) || reader.GetString(4) != fingerprint ||
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

    private static DateTimeOffset ReadDbTimestamp(NpgsqlDataReader reader, int ordinal) =>
        ReadDbTimestamp(reader.GetValue(ordinal));

    private static DateTimeOffset ReadDbTimestamp(object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dateTime when dateTime.Kind == DateTimeKind.Utc => new DateTimeOffset(dateTime),
        DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
        _ => throw new InvalidCastException($"Unsupported PostgreSQL timestamp CLR type: {value.GetType().FullName}.")
    };
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
