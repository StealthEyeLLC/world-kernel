using System.Text.Json;
using StealthEye.WorldKernel.Build001;

return await CommandLine.RunAsync(args).ConfigureAwait(false);

internal static class CommandLine
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var options = Parse(args.Skip(1));
            switch (args[0])
            {
                case "migrate":
                    await MigrateAsync(options, evaluator: false).ConfigureAwait(false);
                    return 0;
                case "migrate-evaluator":
                    await MigrateAsync(options, evaluator: true).ConfigureAwait(false);
                    return 0;
                case "evidence-put":
                    await PutEvidenceAsync(options).ConfigureAwait(false);
                    return 0;
                case "stats":
                    await AnalyzeAsync(options).ConfigureAwait(false);
                    return 0;
                case "package-pair":
                    await BuildPackagePairAsync(options).ConfigureAwait(false);
                    return 0;
                default:
                    throw new ArgumentException($"Unknown command '{args[0]}'.");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                ok = false,
                error_type = exception.GetType().FullName,
                error = exception.Message
            }, JsonDefaults.Options));
            return 1;
        }
    }

    private static async Task MigrateAsync(IReadOnlyDictionary<string, string> options, bool evaluator)
    {
        var repositoryRoot = Required(options, "repo-root");
        var secretFile = Required(options, "secret-file");
        var secretKey = evaluator ? "evaluator_connection" : "owner_connection";
        var connectionString = ConnectionSecrets.ReadConnectionString(secretFile, secretKey);
        var schemaFile = Path.Combine(repositoryRoot, "schemas", evaluator ? "002-evaluator.sql" : "001-world-kernel.sql");
        await using var database = new KernelDb(connectionString);
        await database.MigrateAsync([schemaFile]).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            database = evaluator ? "evaluator" : "kernel",
            schema_file = schemaFile,
            schema_sha256 = CanonicalJson.Sha256(await File.ReadAllBytesAsync(schemaFile).ConfigureAwait(false)),
            migrated_at = DateTimeOffset.UtcNow
        }, JsonDefaults.Options));
    }

    private static async Task PutEvidenceAsync(IReadOnlyDictionary<string, string> options)
    {
        var source = Required(options, "source");
        var storeRoot = Required(options, "store-root");
        var bytes = await File.ReadAllBytesAsync(source).ConfigureAwait(false);
        var store = new EvidenceStore(storeRoot);
        var record = await store.PutAsync(
            bytes,
            Required(options, "provider"),
            Required(options, "observer"),
            options.GetValueOrDefault("media-type", "application/octet-stream"),
            options.GetValueOrDefault("method", "file-import"),
            DateTimeOffset.UtcNow,
            encoding: options.GetValueOrDefault("encoding")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, evidence = record }, JsonDefaults.Options));
    }

    private static async Task AnalyzeAsync(IReadOnlyDictionary<string, string> options)
    {
        var input = Required(options, "input");
        var output = Required(options, "output");
        var inputBytes = await File.ReadAllBytesAsync(input).ConfigureAwait(false);
        var blocks = JsonSerializer.Deserialize<List<PairedBlock>>(inputBytes, JsonDefaults.Options)
                     ?? throw new InvalidDataException("Statistics input did not contain paired blocks.");
        var canonicalInput = PreregisteredStatistics.SerializeInput(blocks);
        var inputHash = CanonicalJson.Sha256(canonicalInput);
        var statistics = PreregisteredStatistics.Analyze(blocks, inputHash);
        var resultBytes = CanonicalJson.Serialize(new
        {
            analysis_version = "build001-primary-analysis-v1",
            input_manifest_hash = inputHash,
            statistics,
            generated_at = DateTimeOffset.UtcNow
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, resultBytes).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            output,
            output_sha256 = CanonicalJson.Sha256(resultBytes),
            statistics
        }, JsonDefaults.Options));
    }

    private static async Task BuildPackagePairAsync(IReadOnlyDictionary<string, string> options)
    {
        var input = Required(options, "input");
        var outputDirectory = Required(options, "output-directory");
        var episodes = JsonSerializer.Deserialize<List<EpisodeExport>>(
                           await File.ReadAllBytesAsync(input).ConfigureAwait(false),
                           JsonDefaults.Options)
                       ?? throw new InvalidDataException("Package input did not contain public acquisition episodes.");
        var pair = PackageBuilder.BuildFairPair(
            episodes,
            Required(options, "semantic-action"),
            Required(options, "manifestation-ref"),
            options.GetValueOrDefault("topology"),
            options.GetValueOrDefault("provider-version"),
            options.TryGetValue("max-tokens", out var tokens) ? int.Parse(tokens, System.Globalization.CultureInfo.InvariantCulture) : Build001Contract.DefaultMaxInheritedTokens,
            options.TryGetValue("max-bytes", out var bytes) ? int.Parse(bytes, System.Globalization.CultureInfo.InvariantCulture) : Build001Contract.MaxPackageBytes);
        Directory.CreateDirectory(outputDirectory);
        var memoryPath = Path.Combine(outputDirectory, "memory-package.txt");
        var structuredPath = Path.Combine(outputDirectory, "structured-package.json");
        var lineagePath = Path.Combine(outputDirectory, "package-lineage.json");
        await File.WriteAllBytesAsync(memoryPath, pair.Memory.Utf8Bytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(structuredPath, pair.Structured.Utf8Bytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(lineagePath, CanonicalJson.Serialize(new
        {
            lineage_hash = pair.LineageHash,
            source_episode_ids = pair.Memory.SourceEpisodeIds,
            memory = new
            {
                pair.Memory.SerializerVersion, pair.Memory.SerializerHash, pair.Memory.ContentHash,
                pair.Memory.ByteLength, pair.Memory.EstimatedTokens, generation_latency_ms = pair.Memory.GenerationLatency.TotalMilliseconds
            },
            structured = new
            {
                pair.Structured.SerializerVersion, pair.Structured.SerializerHash, pair.Structured.ContentHash,
                pair.Structured.ByteLength, pair.Structured.EstimatedTokens, generation_latency_ms = pair.Structured.GenerationLatency.TotalMilliseconds
            }
        })).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, memoryPath, structuredPath, lineagePath }, JsonDefaults.Options));
    }

    private static Dictionary<string, string> Parse(IEnumerable<string> values)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var enumerator = values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var key = enumerator.Current;
            if (!key.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Expected option, received '{key}'.");
            if (!enumerator.MoveNext()) throw new ArgumentException($"Option '{key}' requires a value.");
            result.Add(key[2..], enumerator.Current);
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required option --{name}.");

    private static void PrintHelp() => Console.WriteLine("""
        world-kernel-build-001 commands:
          migrate --repo-root PATH --secret-file PATH
          migrate-evaluator --repo-root PATH --secret-file PATH
          evidence-put --source PATH --store-root PATH --provider NAME --observer NAME [--media-type TYPE]
          stats --input paired-blocks.json --output result.json
          package-pair --input episodes.json --output-directory PATH --semantic-action ACTION --manifestation-ref REF
        """);
}

