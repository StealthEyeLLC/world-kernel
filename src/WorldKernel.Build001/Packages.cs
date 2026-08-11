using System.Globalization;
using System.Text;
using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public sealed record PublicClaimExport(
    Guid ClaimId,
    string SubjectRef,
    string Predicate,
    JsonElement Value,
    string AuthorityClass,
    string ProductionMethod,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    DateTimeOffset KnownAt,
    string Freshness,
    string Standing,
    IReadOnlyList<string> EvidenceHashes);

public sealed record PublicCorrespondenceExport(
    Guid CorrespondenceId,
    string LeftManifestationRef,
    string Relation,
    string RightManifestationRef,
    string Standing,
    double Confidence,
    DateTimeOffset ValidFrom,
    DateTimeOffset KnownAt,
    IReadOnlyList<string> BasisEvidenceHashes);

public sealed record EpisodeExport(
    Guid EpisodeId,
    string SemanticAction,
    string FixtureManifestationRef,
    string PublicTopologyClass,
    DateTimeOffset ClosedAt,
    IReadOnlyList<string> PreObservedFacts,
    IReadOnlyDictionary<string, double> PredictionProbabilities,
    IReadOnlyDictionary<string, bool?> ActualPropositions,
    IReadOnlyDictionary<string, double?> BrierComponents,
    double? MeanBrierLoss,
    IReadOnlyList<string> MaterialDeltas,
    IReadOnlyList<string> InvariantViolations,
    string OutcomeStatus,
    IReadOnlyList<PublicClaimExport> Claims,
    IReadOnlyList<PublicCorrespondenceExport> Correspondences,
    IReadOnlyList<string> EvidenceHashes,
    string ProviderVersionFingerprint);

public sealed record ContextPackage(
    string Arm,
    string SerializerVersion,
    string SerializerHash,
    IReadOnlyList<Guid> SourceEpisodeIds,
    byte[] Utf8Bytes,
    string ContentHash,
    int EstimatedTokens,
    TimeSpan GenerationLatency)
{
    public int ByteLength => Utf8Bytes.Length;
    public string Text => Encoding.UTF8.GetString(Utf8Bytes);
}

public sealed record PackagePair(ContextPackage Memory, ContextPackage Structured, string LineageHash);

public static class PackageBuilder
{
    public const string MemorySerializerVersion = "build001-memory-v1";
    public const string StructuredSerializerVersion = "build001-structured-v1";

    public static readonly string MemorySerializerHash = CanonicalJson.Sha256Utf8(
        "build001-memory-v1|chronological-complete-public-episode-facts|no-extra-llm|utf8-lf");

    public static readonly string StructuredSerializerHash = CanonicalJson.Sha256Utf8(
        "build001-structured-v1|typed-bitemporal-correspondence-episodes|no-extra-llm|canonical-json");

    public static PackagePair BuildFairPair(
        IReadOnlyCollection<EpisodeExport> candidateEpisodes,
        string semanticAction,
        string fixtureManifestationRef,
        string? publicTopologyClass,
        string? providerVersionFingerprint,
        int maxTokens = Build001Contract.DefaultMaxInheritedTokens,
        int maxBytes = Build001Contract.MaxPackageBytes)
    {
        if (maxTokens is <= 0 or > Build001Contract.AbsoluteMaxInheritedTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens));
        }
        if (maxBytes is <= 0 or > Build001Contract.MaxPackageBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        var selected = candidateEpisodes
            .Where(episode => episode.SemanticAction == semanticAction)
            .Where(episode => episode.FixtureManifestationRef == fixtureManifestationRef)
            .Where(episode => publicTopologyClass is null || episode.PublicTopologyClass == publicTopologyClass)
            .Where(episode => providerVersionFingerprint is null ||
                              episode.ProviderVersionFingerprint == providerVersionFingerprint)
            .OrderByDescending(episode => episode.ClosedAt)
            .ThenBy(episode => episode.EpisodeId)
            .ToList();

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("The public deterministic selector found no acquisition episodes.");
        }

        while (selected.Count > 0)
        {
            var memory = BuildMemory(selected);
            var structured = BuildStructured(selected);
            if (Fits(memory, maxTokens, maxBytes) && Fits(structured, maxTokens, maxBytes))
            {
                var sourceIds = selected.Select(episode => episode.EpisodeId).ToArray();
                if (!memory.SourceEpisodeIds.SequenceEqual(sourceIds) || !structured.SourceEpisodeIds.SequenceEqual(sourceIds))
                {
                    throw new InvalidOperationException("Memory/Structured source-episode lineage diverged.");
                }
                var lineageHash = CanonicalJson.Sha256(CanonicalJson.Serialize(new
                {
                    source_episode_ids = sourceIds,
                    selection = new
                    {
                        semantic_action = semanticAction,
                        fixture_manifestation_ref = fixtureManifestationRef,
                        public_topology_class = publicTopologyClass,
                        provider_version_fingerprint = providerVersionFingerprint
                    },
                    ceilings = new { max_tokens = maxTokens, max_bytes = maxBytes }
                }));
                return new PackagePair(memory, structured, lineageHash);
            }

            selected.RemoveAt(selected.Count - 1);
        }

        throw new InvalidOperationException("A single complete episode cannot fit both inherited-context ceilings.");
    }

    private static ContextPackage BuildMemory(IReadOnlyList<EpisodeExport> episodes)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var builder = new StringBuilder();
        builder.AppendLine("CONVENTIONAL MEMORY — deterministic prior episode history");
        builder.AppendLine("This is historical experience, not current provider authority. Reobserve before material action.");
        builder.AppendLine();
        foreach (var episode in episodes)
        {
            builder.Append("Episode ").Append(episode.EpisodeId).Append(" at ")
                .AppendLine(episode.ClosedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.Append("Environment: fixture ").Append(episode.FixtureManifestationRef)
                .Append(", observable topology ").Append(episode.PublicTopologyClass)
                .Append(", provider versions ").AppendLine(episode.ProviderVersionFingerprint);
            builder.Append("Pre-action observations: ").AppendLine(string.Join("; ", episode.PreObservedFacts));
            builder.Append("Action taken: ").AppendLine(episode.SemanticAction);
            builder.Append("Predicted probabilities: ").AppendLine(FormatDictionary(episode.PredictionProbabilities));
            builder.Append("Actual propositions: ").AppendLine(FormatDictionary(episode.ActualPropositions));
            builder.Append("Prediction errors: ").AppendLine(FormatDictionary(episode.BrierComponents));
            builder.Append("Mean Brier loss: ")
                .AppendLine(episode.MeanBrierLoss?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "unknown/censored");
            builder.Append("Material deltas: ").AppendLine(string.Join(", ", episode.MaterialDeltas));
            builder.Append("Invariant violations: ").AppendLine(
                episode.InvariantViolations.Count == 0 ? "none" : string.Join(", ", episode.InvariantViolations));
            builder.Append("Outcome: ").AppendLine(episode.OutcomeStatus);
            foreach (var claim in episode.Claims.OrderBy(claim => claim.Predicate, StringComparer.Ordinal))
            {
                builder.Append("Historical fact/standing: ").Append(claim.SubjectRef).Append(' ')
                    .Append(claim.Predicate).Append('=').Append(claim.Value.GetRawText())
                    .Append("; authority ").Append(claim.AuthorityClass)
                    .Append("; observed/learned ").Append(claim.KnownAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                    .Append("; externally valid from ").Append(claim.ValidFrom.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                    .Append("; freshness ").Append(claim.Freshness)
                    .Append("; current historical standing ").AppendLine(claim.Standing);
            }
            foreach (var correspondence in episode.Correspondences.OrderBy(c => c.CorrespondenceId))
            {
                builder.Append("Repository relationship history: ").Append(correspondence.LeftManifestationRef)
                    .Append(' ').Append(correspondence.Relation).Append(' ').Append(correspondence.RightManifestationRef)
                    .Append("; standing ").Append(correspondence.Standing)
                    .Append("; confidence ").AppendLine(correspondence.Confidence.ToString("0.000", CultureInfo.InvariantCulture));
            }
            builder.Append("Evidence hashes: ").AppendLine(string.Join(", ", episode.EvidenceHashes));
            builder.AppendLine();
        }
        started.Stop();
        var bytes = Encoding.UTF8.GetBytes(builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        return CreatePackage("memory", MemorySerializerVersion, MemorySerializerHash, episodes, bytes, started.Elapsed);
    }

    private static ContextPackage BuildStructured(IReadOnlyList<EpisodeExport> episodes)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var payload = new
        {
            package_kind = "world_kernel_build001_structured_history",
            authority_notice = "Derived historical package; native providers remain authoritative and must be freshly observed.",
            selected_episode_ids = episodes.Select(episode => episode.EpisodeId).ToArray(),
            manifestations = episodes.Select(episode => new
            {
                manifestation_ref = episode.FixtureManifestationRef,
                provider_version_fingerprint = episode.ProviderVersionFingerprint
            }).Distinct().OrderBy(value => value.manifestation_ref, StringComparer.Ordinal).ToArray(),
            correspondences = episodes.SelectMany(episode => episode.Correspondences)
                .GroupBy(value => value.CorrespondenceId)
                .Select(group => group.OrderByDescending(value => value.KnownAt).First())
                .OrderBy(value => value.CorrespondenceId)
                .ToArray(),
            claims = episodes.SelectMany(episode => episode.Claims)
                .OrderBy(value => value.KnownAt)
                .ThenBy(value => value.ClaimId)
                .ToArray(),
            transition_episodes = episodes.Select(episode => new
            {
                episode.EpisodeId,
                episode.SemanticAction,
                episode.FixtureManifestationRef,
                episode.PublicTopologyClass,
                episode.ClosedAt,
                episode.PreObservedFacts,
                episode.PredictionProbabilities,
                episode.ActualPropositions,
                episode.BrierComponents,
                episode.MeanBrierLoss,
                episode.MaterialDeltas,
                episode.InvariantViolations,
                episode.OutcomeStatus,
                episode.EvidenceHashes
            }).ToArray()
        };
        var bytes = CanonicalJson.Serialize(payload);
        started.Stop();
        return CreatePackage("structured", StructuredSerializerVersion, StructuredSerializerHash, episodes, bytes, started.Elapsed);
    }

    private static ContextPackage CreatePackage(
        string arm,
        string version,
        string serializerHash,
        IReadOnlyList<EpisodeExport> episodes,
        byte[] bytes,
        TimeSpan latency) => new(
            arm,
            version,
            serializerHash,
            episodes.Select(episode => episode.EpisodeId).ToArray(),
            bytes,
            CanonicalJson.Sha256(bytes),
            CanonicalJson.EstimateTokens(bytes),
            latency);

    private static bool Fits(ContextPackage package, int maxTokens, int maxBytes) =>
        package.EstimatedTokens <= maxTokens && package.ByteLength <= maxBytes;

    private static string FormatDictionary<T>(IReadOnlyDictionary<string, T> values) => string.Join(
        ", ",
        values.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => $"{value.Key}={value.Value}"));
}
