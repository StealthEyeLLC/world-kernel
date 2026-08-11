using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace StealthEye.WorldKernel.Build001;

public sealed record LatencySummary(double MeanMs, double P50Ms, double P95Ms, double MinMs, double MaxMs, int Samples);

public static class OverheadBenchmark
{
    public static async Task<object> MeasureAsync(
        string kernelConnection,
        string evaluatorConnection,
        string evidenceRoot,
        string artifactRoot,
        CancellationToken cancellationToken = default)
    {
        const int samples = 20;
        var blobWrites = new List<double>();
        var databaseWrites = new List<double>();
        var databaseReads = new List<double>();
        var blobReads = new List<double>();
        var store = new EvidenceStore(evidenceRoot);
        await using var database = new KernelDb(kernelConnection);
        await using var readConnection = new NpgsqlConnection(kernelConnection);
        await readConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < samples; index++)
        {
            var bytes = Encoding.UTF8.GetBytes($"build001-overhead-probe-v1|{index:00}|{Guid.NewGuid():N}\n");
            var stopwatch = Stopwatch.StartNew();
            var evidence = await store.PutAsync(
                bytes,
                "build001/overhead-probe",
                "world-kernel-overhead-v1",
                "text/plain",
                "implementation-overhead-probe",
                DateTimeOffset.UtcNow,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            blobWrites.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            await database.InsertEvidenceAsync(evidence, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            databaseWrites.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            await using (var query = new NpgsqlCommand(
                             "SELECT content_hash,byte_length FROM wk.evidence WHERE evidence_id=@id;",
                             readConnection))
            {
                query.Parameters.AddWithValue("id", evidence.EvidenceId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
                    reader.GetString(0) != evidence.ContentHash || reader.GetInt64(1) != evidence.ByteLength)
                {
                    throw new InvalidOperationException("Overhead benchmark database read did not reproduce written Evidence.");
                }
            }
            stopwatch.Stop();
            databaseReads.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            var read = await store.ReadVerifiedAsync(evidence, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            if (!read.SequenceEqual(bytes)) throw new InvalidOperationException("Overhead benchmark blob verification failed.");
            blobReads.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var packageTotals = new List<double>();
        var memorySerialization = new List<double>();
        var structuredSerialization = new List<double>();
        var packageBytes = new List<(int Memory, int Structured)>();
        var episodes = Enumerable.Range(0, 4).Select(SyntheticEpisode).ToArray();
        for (var index = 0; index < samples; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            var pair = PackageBuilder.BuildFairPair(
                episodes,
                "git:push_ref",
                "fixture:1330898503",
                "local_ahead",
                "git-2.55|github|eyebrowse-2e27f44e");
            stopwatch.Stop();
            packageTotals.Add(stopwatch.Elapsed.TotalMilliseconds);
            memorySerialization.Add(pair.Memory.GenerationLatency.TotalMilliseconds);
            structuredSerialization.Add(pair.Structured.GenerationLatency.TotalMilliseconds);
            packageBytes.Add((pair.Memory.ByteLength, pair.Structured.ByteLength));
        }

        var kernelSize = await DatabaseSizeAsync(readConnection, cancellationToken).ConfigureAwait(false);
        await using var evaluator = new NpgsqlConnection(evaluatorConnection);
        await evaluator.OpenAsync(cancellationToken).ConfigureAwait(false);
        var evaluatorSize = await DatabaseSizeAsync(evaluator, cancellationToken).ConfigureAwait(false);
        var evidenceBytes = DirectorySize(evidenceRoot);
        var artifactBytes = DirectorySize(artifactRoot);

        var providerTimings = new List<object>();
        foreach (var file in new[]
                 {
                     "p1-codeeye-live.json", "p2-eyebrowse-live.json", "p3-create-local-commit.json",
                     "p3-push-accepted.json", "p3-fetch.json", "p3-integrate-fast-forward.json"
                 })
        {
            var path = Path.Combine(artifactRoot, "preflight", file);
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            if (!TryDate(root, "started_at", out var started) || !TryDate(root, "completed_at", out var completed)) continue;
            providerTimings.Add(new { artifact = file, wall_ms = (completed - started).TotalMilliseconds });
        }

        return new
        {
            schema = "world-kernel-build001-overhead-v1",
            scope = "implementation/preflight only; no pilot or confirmatory trial",
            evidence_probe_count = samples,
            content_addressed_blob_write = Summarize(blobWrites),
            postgresql_evidence_insert = Summarize(databaseWrites),
            postgresql_exact_evidence_read = Summarize(databaseReads),
            verified_blob_read = Summarize(blobReads),
            deterministic_package_pair_total = Summarize(packageTotals),
            memory_serializer = Summarize(memorySerialization),
            structured_serializer = Summarize(structuredSerialization),
            package_fixture = new
            {
                synthetic_implementation_benchmark = true,
                source_episode_count = episodes.Length,
                memory_bytes = packageBytes[0].Memory,
                structured_bytes = packageBytes[0].Structured,
                extra_llm_calls = 0
            },
            storage = new
            {
                kernel_postgresql_bytes = kernelSize,
                evaluator_postgresql_bytes = evaluatorSize,
                evidence_blob_bytes = evidenceBytes,
                committed_and_uncommitted_artifact_bytes = artifactBytes
            },
            provider_wait_wall_times = providerTimings,
            experimental_model_calls = 0,
            experimental_model_input_tokens = 0,
            experimental_model_output_tokens = 0,
            implementation_operator = new
            {
                invocations = 1,
                surface = "ChatGPT Work / Codex ongoing implementation conversation",
                exact_token_telemetry_available = false,
                excluded_from_arm_statistics = true
            },
            measured_at = DateTimeOffset.UtcNow
        };
    }

    private static LatencySummary Summarize(IReadOnlyCollection<double> values)
    {
        var sorted = values.Order().ToArray();
        return new LatencySummary(
            sorted.Average(),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            sorted[0],
            sorted[^1],
            sorted.Length);
    }

    private static double Percentile(IReadOnlyList<double> values, double probability)
    {
        var position = probability * (values.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? values[lower]
            : values[lower] + (values[upper] - values[lower]) * (position - lower);
    }

    private static async Task<long> DatabaseSizeAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_database_size(current_database());", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long DirectorySize(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length)
        : 0;

    private static bool TryDate(JsonElement root, string property, out DateTimeOffset value)
    {
        value = default;
        return root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(element.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal, out value);
    }

    private static EpisodeExport SyntheticEpisode(int index)
    {
        const string action = "git:push_ref";
        var probabilities = Build001Contract.ForAction(action).ToDictionary(key => key, _ => 0.75, StringComparer.Ordinal);
        var actual = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (bool?)true, StringComparer.Ordinal);
        var components = Build001Contract.ForAction(action).ToDictionary(key => key, _ => (double?)0.0625, StringComparer.Ordinal);
        return new EpisodeExport(
            Guid.Parse($"10000000-0000-0000-0000-{index + 1:000000000000}"),
            action,
            "fixture:1330898503",
            "local_ahead",
            DateTimeOffset.Parse($"2026-08-01T00:{index:00}:00Z", System.Globalization.CultureInfo.InvariantCulture),
            ["synthetic implementation benchmark; not experimental history"],
            probabilities,
            actual,
            components,
            0.0625,
            ["remote_ref_changed"],
            [],
            "verified",
            [],
            [],
            [new string('a', 64)],
            "git-2.55|github|eyebrowse-2e27f44e");
    }
}
