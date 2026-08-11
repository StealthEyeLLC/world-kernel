using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public sealed record CorrespondenceInput(
    Guid LocalManifestationId,
    Guid RemoteManifestationId,
    string ConfiguredRemoteUrl,
    string ProviderCanonicalFullName,
    long ProviderNativeRepositoryId,
    long ExpectedProviderNativeRepositoryId,
    IReadOnlyCollection<string> ProviderAcceptedLocatorFullNames,
    IReadOnlyCollection<string> LocalReachableCommits,
    IReadOnlyCollection<string> RemoteReachableCommits,
    IReadOnlyCollection<string> EvidenceDependencyGroups,
    bool ProviderIdentityFresh,
    bool LocalRemoteObservationFresh,
    bool HistoryObservationFresh);

public sealed record CorrespondenceDecision(
    string Relation,
    string Strength,
    double Confidence,
    IReadOnlyList<string> SatisfiedBasis,
    IReadOnlyList<string> Ambiguities,
    string BasisFingerprint);

public static class CorrespondenceResolver
{
    public static CorrespondenceDecision ResolveWorkingCopyOf(CorrespondenceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var satisfied = new List<string>();
        var ambiguity = new List<string>();
        var remoteName = TryNormalizeGitHubFullName(input.ConfiguredRemoteUrl);
        var acceptedNames = input.ProviderAcceptedLocatorFullNames
            .Append(input.ProviderCanonicalFullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (remoteName is not null && acceptedNames.Contains(remoteName))
        {
            satisfied.Add("configured_remote_url");
        }
        else
        {
            ambiguity.Add("configured remote does not resolve to the provider manifestation's accepted locators");
        }

        if (input.ProviderNativeRepositoryId == input.ExpectedProviderNativeRepositoryId && input.ProviderIdentityFresh)
        {
            satisfied.Add("hosted_provider_native_id");
        }
        else
        {
            ambiguity.Add("hosted provider-native identity is mismatched or stale");
        }

        var shared = input.LocalReachableCommits
            .Intersect(input.RemoteReachableCommits, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (shared.Length > 0 && input.HistoryObservationFresh)
        {
            satisfied.Add("shared_exact_commit");
        }
        else
        {
            ambiguity.Add("no fresh compatible exact Git history evidence");
        }

        if (!input.LocalRemoteObservationFresh)
        {
            ambiguity.Add("configured remote observation is stale");
        }
        var independentGroups = input.EvidenceDependencyGroups.Distinct(StringComparer.Ordinal).Count();
        if (independentGroups < 2)
        {
            ambiguity.Add("correlated sensor evidence does not count as independent identity support");
        }

        var hard = satisfied.Count == 3 && ambiguity.Count == 0 && independentGroups >= 2;
        var basis = new
        {
            relation = "git:working_copy_of",
            local = input.LocalManifestationId,
            remote = input.RemoteManifestationId,
            normalized_remote = remoteName,
            provider_id = input.ProviderNativeRepositoryId,
            expected_provider_id = input.ExpectedProviderNativeRepositoryId,
            shared_exact_commits = shared,
            evidence_dependency_groups = input.EvidenceDependencyGroups.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            satisfied,
            ambiguity
        };
        return new CorrespondenceDecision(
            "git:working_copy_of",
            hard ? "hard" : "candidate",
            hard ? 1.0 : Math.Min(0.74, satisfied.Count / 4.0),
            satisfied,
            ambiguity,
            CanonicalJson.Sha256(CanonicalJson.Serialize(basis)));
    }

    public static string? TryNormalizeGitHubFullName(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }
        var trimmed = remoteUrl.Trim();
        const string scpPrefix = "git@github.com:";
        string path;
        if (trimmed.StartsWith(scpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = trimmed[scpPrefix.Length..];
        }
        else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                 string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                 uri.Scheme is "https" or "ssh" or "git")
        {
            path = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            return null;
        }

        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 ? $"{segments[0]}/{segments[1]}" : null;
    }
}
