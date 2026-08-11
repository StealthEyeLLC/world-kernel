using System.Text;
using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class UnitTests
{
    public static Task CompleteVectorsAsync()
    {
        AssertEx.Equal(6, Build001Contract.Propositions.Count);
        AssertEx.Equal(8, Build001Contract.ForAction("git:create_local_commit").Count);
        AssertEx.Equal(6, Build001Contract.ForAction("git:create_branch").Count);
        AssertEx.Equal(8, Build001Contract.ForAction("git:push_ref").Count);
        AssertEx.Equal(8, Build001Contract.ForAction("github:create_remote_commit").Count);
        AssertEx.Equal(7, Build001Contract.ForAction("git:fetch_remote").Count);
        AssertEx.Equal(7, Build001Contract.ForAction("git:integrate_fast_forward").Count);
        var normalized = Build001Contract.NormalizePrediction(
            "git:create_branch",
            new Dictionary<string, double?> { ["provider_accepts_action"] = 0.9, ["unexpected"] = 1.0 },
            out var defects);
        AssertEx.Equal(6, normalized.Count);
        AssertEx.Near(0.5, normalized["new_local_branch_exists"], 0);
        AssertEx.True(defects.Count >= 6, "Missing fields and extra proposition must be recorded as format defects.");
        return Task.CompletedTask;
    }

    public static async Task EvidenceStoreAsync(string artifactDirectory)
    {
        var root = Path.Combine(artifactDirectory, "evidence-store-test", Guid.NewGuid().ToString("N"));
        var store = new EvidenceStore(root);
        var bytes = Encoding.UTF8.GetBytes("immutable evidence\n");
        var first = await store.PutAsync(bytes, "test/provider", "test-observer", "text/plain", "unit-test", DateTimeOffset.UtcNow).ConfigureAwait(false);
        var second = await store.PutAsync(bytes, "test/provider", "test-observer", "text/plain", "unit-test-recapture", DateTimeOffset.UtcNow).ConfigureAwait(false);
        AssertEx.Equal(first.ContentHash, second.ContentHash);
        AssertEx.Equal(first.BlobRef, second.BlobRef);
        AssertEx.False(first.EvidenceId == second.EvidenceId, "Recapture events retain distinct Evidence identities while sharing immutable bytes.");
        var read = await store.ReadVerifiedAsync(first).ConfigureAwait(false);
        AssertEx.True(bytes.SequenceEqual(read));
        var path = store.ResolveBlobRef(first.BlobRef);
        await File.WriteAllTextAsync(path, "tampered").ConfigureAwait(false);
        await AssertEx.ThrowsAsync<InvalidDataException>(() => store.ReadVerifiedAsync(first)).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<InvalidDataException>(() => store.PutAsync(bytes, "test/provider", "test-observer", "text/plain", "unit-test", DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    public static Task CorrespondenceAsync()
    {
        var local = Guid.NewGuid();
        var remote = Guid.NewGuid();
        var basis = new CorrespondenceInput(
            local,
            remote,
            "https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git",
            "StealthEyeLLC/world-kernel-build-001-fixture",
            1330898503,
            1330898503,
            ["StealthEyeLLC/world-kernel-build-001-fixture"],
            ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"],
            ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"],
            ["codeeye/git-local", "github/provider-native"],
            true,
            true,
            true);
        var exact = CorrespondenceResolver.ResolveWorkingCopyOf(basis);
        AssertEx.Equal("hard", exact.Strength);
        AssertEx.Equal("git:working_copy_of", exact.Relation);

        var sameNameDecoy = CorrespondenceResolver.ResolveWorkingCopyOf(basis with { ProviderNativeRepositoryId = 999 });
        AssertEx.Equal("candidate", sameNameDecoy.Strength);
        var fork = CorrespondenceResolver.ResolveWorkingCopyOf(basis with
        {
            ConfiguredRemoteUrl = "https://github.com/decoy/world-kernel-build-001-fixture.git",
            ProviderCanonicalFullName = "StealthEyeLLC/world-kernel-build-001-fixture",
            ProviderNativeRepositoryId = 999
        });
        AssertEx.Equal("candidate", fork.Strength);
        var identicalClone = CorrespondenceResolver.ResolveWorkingCopyOf(basis with
        {
            ConfiguredRemoteUrl = "file:///X:/clone",
            ProviderNativeRepositoryId = 999
        });
        AssertEx.Equal("candidate", identicalClone.Strength);
        var changedRemote = CorrespondenceResolver.ResolveWorkingCopyOf(basis with
        {
            ConfiguredRemoteUrl = "https://github.com/StealthEyeLLC/eye.git"
        });
        AssertEx.Equal("candidate", changedRemote.Strength);
        var correlated = CorrespondenceResolver.ResolveWorkingCopyOf(basis with
        {
            EvidenceDependencyGroups = ["eyebrowse-page", "eyebrowse-page"]
        });
        AssertEx.Equal("candidate", correlated.Strength);
        var renamed = CorrespondenceResolver.ResolveWorkingCopyOf(basis with
        {
            ConfiguredRemoteUrl = "https://github.com/StealthEyeLLC/fixture-old-name.git",
            ProviderAcceptedLocatorFullNames = ["StealthEyeLLC/fixture-old-name", "StealthEyeLLC/world-kernel-build-001-fixture"]
        });
        AssertEx.Equal("hard", renamed.Strength);
        AssertEx.Equal("StealthEyeLLC/world-kernel", CorrespondenceResolver.TryNormalizeGitHubFullName("git@github.com:StealthEyeLLC/world-kernel.git"));
        AssertEx.Equal<string?>(null, CorrespondenceResolver.TryNormalizeGitHubFullName("X:\\same-name\\world-kernel"));
        return Task.CompletedTask;
    }

    public static Task PackagesAsync()
    {
        var episodes = Enumerable.Range(0, 4).Select(index => Episode(index)).ToArray();
        var pair1 = PackageBuilder.BuildFairPair(episodes, "git:push_ref", "fixture:1330898503", "local_ahead", "git-2.55|github|eyebrowse-2e27f44e");
        var pair2 = PackageBuilder.BuildFairPair(episodes.Reverse().ToArray(), "git:push_ref", "fixture:1330898503", "local_ahead", "git-2.55|github|eyebrowse-2e27f44e");
        AssertEx.True(pair1.Memory.SourceEpisodeIds.SequenceEqual(pair1.Structured.SourceEpisodeIds));
        AssertEx.Equal(pair1.LineageHash, pair2.LineageHash);
        AssertEx.Equal(pair1.Memory.ContentHash, pair2.Memory.ContentHash);
        AssertEx.Equal(pair1.Structured.ContentHash, pair2.Structured.ContentHash);
        AssertEx.True(pair1.Memory.ByteLength <= Build001Contract.MaxPackageBytes);
        AssertEx.True(pair1.Structured.ByteLength <= Build001Contract.MaxPackageBytes);
        AssertEx.True(pair1.Memory.EstimatedTokens <= Build001Contract.DefaultMaxInheritedTokens);
        AssertEx.True(pair1.Structured.EstimatedTokens <= Build001Contract.DefaultMaxInheritedTokens);
        AssertEx.False(pair1.Memory.Text.Contains("regime_label", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(pair1.Structured.Text.Contains("answer_key", StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public static Task ScoringAsync()
    {
        var action = "git:create_branch";
        var probabilities = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (double?)0.8, StringComparer.Ordinal);
        var actual = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (bool?)true, StringComparer.Ordinal);
        var score = PredictionScorer.Score(action, probabilities, actual, ["branch_created"], ["branch_created"], ["head_unchanged"], []);
        AssertEx.Equal("eligible", score.EligibilityStatus);
        AssertEx.Near(0.04, score.MeanBrierLoss!.Value, 1e-12);
        AssertEx.Near(1.0, score.DeltaF1!.Value, 1e-12);

        var behavioral = BehavioralScorer.Analyze([
            new BehavioralRun("b1", "memory", 3, 1, true, 1),
            new BehavioralRun("b2", "memory", 2, 1, true, 0),
            new BehavioralRun("b1", "structured", 2, 1, true, 0),
            new BehavioralRun("b2", "structured", 1, 1, true, 0)
        ]);
        AssertEx.True(behavioral.PassesGate);
        AssertEx.Near(2.0 / 3.0, behavioral.RelativeReduction!.Value, 1e-12);
        return Task.CompletedTask;
    }

    public static Task StatisticsAsync()
    {
        var blocks = Enumerable.Range(1, 48)
            .Select(index => new PairedBlock($"b{index:000}", 0.25 + (index % 4) * 0.01, 0.15 + (index % 4) * 0.005, 0.30))
            .ToArray();
        var inputHash = CanonicalJson.Sha256(PreregisteredStatistics.SerializeInput(blocks));
        var first = PreregisteredStatistics.Analyze(blocks, inputHash);
        var second = PreregisteredStatistics.Analyze(blocks, inputHash);
        AssertEx.Equal(48, first.BlockCount);
        AssertEx.True(first.RelativeReduction >= 0.20);
        AssertEx.True(first.BootstrapDifferenceLower > 0);
        AssertEx.True(first.RandomizationPValue < 0.05);
        AssertEx.Equal(first, second, "Locked statistics must be deterministic for the same input manifest.");
        AssertEx.Equal(48, PreregisteredStatistics.PilotDerivedBlockCount(0.2, 0.03));
        AssertEx.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(PreregisteredStatistics.PilotDerivedBlockCount(0.04, 0.03))).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    private static EpisodeExport Episode(int index)
    {
        var action = "git:push_ref";
        var probabilities = Build001Contract.ForAction(action).ToDictionary(key => key, _ => 0.75, StringComparer.Ordinal);
        var actual = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (bool?)true, StringComparer.Ordinal);
        var components = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (double?)0.0625, StringComparer.Ordinal);
        return new EpisodeExport(
            Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
            action,
            "fixture:1330898503",
            "local_ahead",
            DateTimeOffset.Parse($"2026-08-10T00:{index:00}:00Z", System.Globalization.CultureInfo.InvariantCulture),
            ["local branch ahead by one exact commit", "remote branch absent"],
            probabilities,
            actual,
            components,
            0.0625,
            ["remote_ref_changed"],
            [],
            "verified",
            [new PublicClaimExport(Guid.NewGuid(), "local:fixture", "git:topology", JsonSerializer.SerializeToElement("local_ahead"), "provider", "observed", DateTimeOffset.Parse("2026-08-10T00:00:00Z"), null, DateTimeOffset.Parse("2026-08-10T00:00:01Z"), "fresh_at_episode", "supported", [new string('a', 64)])],
            [new PublicCorrespondenceExport(Guid.NewGuid(), "local:fixture", "git:working_copy_of", "github:1330898503", "supported", 1.0, DateTimeOffset.Parse("2026-08-10T00:00:00Z"), DateTimeOffset.Parse("2026-08-10T00:00:01Z"), [new string('b', 64)])],
            [new string('a', 64), new string('b', 64)],
            "git-2.55|github|eyebrowse-2e27f44e");
    }
}
