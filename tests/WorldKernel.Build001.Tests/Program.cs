using System.Text.Json;
using StealthEye.WorldKernel.Build001;
using StealthEye.WorldKernel.Build001.Tests;

var options = Parse(args);
var repositoryRoot = Required(options, "repo-root");
var secretFile = Required(options, "secret-file");
var artifactDirectory = Path.Combine(repositoryRoot, "artifacts", "preflight");
Directory.CreateDirectory(artifactDirectory);

var tests = new List<(string Name, Func<Task> Run)>
{
    ("contract.complete_vectors", UnitTests.CompleteVectorsAsync),
    ("evidence.content_addressed_immutable", () => UnitTests.EvidenceStoreAsync(artifactDirectory)),
    ("correspondence.conservative_identity", UnitTests.CorrespondenceAsync),
    ("packages.same_source_deterministic_bounded", UnitTests.PackagesAsync),
    ("scoring.brier_and_behavior", UnitTests.ScoringAsync),
    ("statistics.paired_block_procedure", UnitTests.StatisticsAsync),
    ("experiment.preflight_phase_refusal", () => UnitTests.PreflightRefusalAsync(artifactDirectory)),\n    ("campaign2.observable_attestation_hard_gates", Campaign2Tests.ObservableAttestationsAsync),
    ("postgres.schema_temporal_append_only", () => DatabaseTests.SchemaAndTemporalAsync(secretFile)),
    ("postgres.prediction_dispatch_episode", () => DatabaseTests.ActionLifecycleAsync(secretFile, artifactDirectory)),
    ("postgres.epistemic_laundering_hostiles", () => DatabaseTests.EpistemicHostilesAsync(secretFile, artifactDirectory)),
    ("postgres.arm_isolation", () => DatabaseTests.ArmIsolationAsync(secretFile))
};

var results = new List<object>();
var failures = 0;
foreach (var test in tests)
{
    var started = DateTimeOffset.UtcNow;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        await test.Run().ConfigureAwait(false);
        stopwatch.Stop();
        results.Add(new { name = test.Name, status = "PASS", started_at = started, duration_ms = stopwatch.Elapsed.TotalMilliseconds });
        Console.WriteLine($"PASS {test.Name} ({stopwatch.Elapsed.TotalMilliseconds:0.0} ms)");
    }
    catch (Exception exception)
    {
        stopwatch.Stop();
        failures++;
        results.Add(new
        {
            name = test.Name,
            status = "FAIL",
            started_at = started,
            duration_ms = stopwatch.Elapsed.TotalMilliseconds,
            error_type = exception.GetType().FullName,
            error = exception.Message,
            stack = exception.StackTrace
        });
        Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
    }
}

var output = CanonicalJson.Serialize(new
{
    suite = "world-kernel-build001-preconfirmatory-v1",
    generated_at = DateTimeOffset.UtcNow,
    passed = tests.Count - failures,
    failed = failures,
    tests = results
});
var outputPath = Path.Combine(artifactDirectory, "implementation-test-results.json");
await File.WriteAllBytesAsync(outputPath, output).ConfigureAwait(false);
await File.WriteAllTextAsync(outputPath + ".sha256", CanonicalJson.Sha256(output) + "  implementation-test-results.json\n").ConfigureAwait(false);
Console.WriteLine(JsonSerializer.Serialize(new { output = outputPath, sha256 = CanonicalJson.Sha256(output), failures }, JsonDefaults.Options));
return failures == 0 ? 0 : 1;

static Dictionary<string, string> Parse(IEnumerable<string> values)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    using var enumerator = values.GetEnumerator();
    while (enumerator.MoveNext())
    {
        var key = enumerator.Current;
        if (!key.StartsWith("--", StringComparison.Ordinal) || !enumerator.MoveNext()) throw new ArgumentException("Invalid option list.");
        result[key[2..]] = enumerator.Current;
    }
    return result;
}

static string Required(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing --{key}.");
