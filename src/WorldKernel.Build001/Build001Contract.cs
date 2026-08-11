using System.Collections.ObjectModel;
using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public static class Build001Contract
{
    public const string EvaluationSpecVersion = "build001-evaluation-v1";
    public const string EvaluationSpecHash = "f182f6b0ac91d85436f077f0c78e3db0ec3b35d2f15f82d0675b699598c93ded";
    public const string ScorerVersion = "build001-brier-v1";
    public const int MaxPackageBytes = 32_768;
    public const int DefaultMaxInheritedTokens = 6_000;
    public const int AbsoluteMaxInheritedTokens = 8_000;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PropositionMap =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["git:create_local_commit"] =
                [
                    "provider_accepts_action", "local_head_changes", "local_head_equals_new_commit",
                    "remote_target_ref_changes_before_push", "local_worktree_clean_after",
                    "current_branch_name_changes", "new_commit_reachable_locally",
                    "new_commit_reachable_remotely_before_push"
                ],
                ["git:create_branch"] =
                [
                    "provider_accepts_action", "new_local_branch_exists", "current_branch_is_new_branch",
                    "local_head_sha_changes", "remote_branch_exists_before_push", "worktree_content_changes"
                ],
                ["git:push_ref"] =
                [
                    "provider_accepts_push", "remote_ref_exists_at_H1", "remote_ref_equals_local_head_at_H1",
                    "local_head_changes_because_of_push", "local_worktree_changes_because_of_push",
                    "remote_check_starts_by_H2", "remote_check_terminal_success_by_H3",
                    "browser_presentation_reflects_new_remote_head_by_H1"
                ],
                ["github:create_remote_commit"] =
                [
                    "provider_accepts_action", "remote_head_changes", "remote_head_equals_new_hosted_commit",
                    "local_head_changes_before_fetch", "local_worktree_changes_before_fetch",
                    "local_remote_tracking_ref_changes_before_fetch",
                    "new_remote_commit_reachable_locally_before_fetch",
                    "browser_presentation_reflects_new_remote_commit_by_H1"
                ],
                ["git:fetch_remote"] =
                [
                    "provider_accepts_action", "local_head_changes", "local_worktree_changes",
                    "remote_tracking_ref_equals_remote_head_at_H1", "remote_head_changes_because_of_fetch",
                    "remote_commit_reachable_locally_after_fetch", "checked_out_branch_content_changes"
                ],
                ["git:integrate_fast_forward"] =
                [
                    "fast_forward_is_accepted", "local_head_equals_remote_target_after_H1", "local_head_changes",
                    "local_worktree_content_changes", "local_worktree_clean_after",
                    "remote_head_changes_because_of_integration", "merge_commit_created"
                ]
            });

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Propositions => PropositionMap;

    public static IReadOnlyList<string> ForAction(string semanticAction) =>
        PropositionMap.TryGetValue(semanticAction, out var propositions)
            ? propositions
            : throw new ArgumentOutOfRangeException(nameof(semanticAction), semanticAction, "Not a frozen Build 001 action.");

    public static (string Namespace, string Type) SplitAction(string semanticAction)
    {
        _ = ForAction(semanticAction);
        var index = semanticAction.IndexOf(':', StringComparison.Ordinal);
        return (semanticAction[..index], semanticAction[(index + 1)..]);
    }

    public static IReadOnlyDictionary<string, double> NormalizePrediction(
        string semanticAction,
        IReadOnlyDictionary<string, double?> supplied,
        out IReadOnlyList<string> formatDefects)
    {
        var defects = new List<string>();
        var normalized = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var key in ForAction(semanticAction))
        {
            if (!supplied.TryGetValue(key, out var value) || value is null || double.IsNaN(value.Value) ||
                double.IsInfinity(value.Value) || value.Value < 0 || value.Value > 1)
            {
                normalized[key] = 0.5;
                defects.Add($"{key}: missing or invalid; locked default 0.5 applied");
            }
            else
            {
                normalized[key] = value.Value;
            }
        }

        foreach (var extra in supplied.Keys.Except(ForAction(semanticAction), StringComparer.Ordinal))
        {
            defects.Add($"{extra}: ignored extra proposition");
        }

        formatDefects = defects.AsReadOnly();
        return new ReadOnlyDictionary<string, double>(normalized);
    }

    public static JsonElement DefaultHorizons() => JsonSerializer.SerializeToElement(new
    {
        H1 = "pilot_freeze_required",
        H2 = "pilot_freeze_required",
        H3 = "pilot_freeze_required"
    });
}

public sealed record EvidenceRecord(
    Guid EvidenceId,
    string ProviderNamespace,
    string ObserverName,
    DateTimeOffset CapturedAt,
    string HashAlgorithm,
    string ContentHash,
    string BlobRef,
    string MediaType,
    string AcquisitionMethod,
    long ByteLength,
    string? ProviderRevision,
    DateTimeOffset? ProviderEventAt,
    string? Encoding,
    JsonElement Metadata);

public sealed record ManifestationRecord(
    Guid ManifestationId,
    string ProviderNamespace,
    string ManifestationKind,
    JsonElement IdentityBasis,
    string IncarnationKey,
    string? ProviderNativeId,
    JsonElement ObserverNativeIds,
    string? DisplayLabel);

public sealed record ObservationRecord(
    Guid ObservationId,
    Guid TargetManifestationId,
    string ObserverName,
    string ObserverVersion,
    string ProviderNamespace,
    DateTimeOffset ObservedAt,
    string AcquisitionStatus,
    JsonElement Coverage,
    string? ProviderRevision,
    DateTimeOffset? ProviderEventAt,
    JsonElement SourceDependency,
    JsonElement? RawNormalizedPayload,
    IReadOnlyList<Guid> EvidenceIds);

public sealed record ActionDeclaration(
    Guid ActionId,
    string TrialId,
    string ConfigurationBlockId,
    string Arm,
    IReadOnlyList<Guid> TargetManifestations,
    string OwningEye,
    string CapabilityName,
    string CapabilityVersion,
    string SemanticAction,
    JsonElement Parameters,
    JsonElement ProducerModel,
    string FixtureScopeId);

public sealed record PredictionDeclaration(
    Guid PredictionId,
    Guid ActionId,
    string SemanticAction,
    IReadOnlyDictionary<string, double?> Probabilities,
    JsonElement ExpectedDeltas,
    JsonElement ExpectedInvariants,
    JsonElement Horizons,
    string Mechanism,
    string MechanismVersion,
    JsonElement ProducerModel);

public sealed record ProviderOperationResult(
    string SemanticAction,
    bool ReceiptAccepted,
    int ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    byte[] EvidenceBytes,
    JsonElement TypedReceipt);

