using System.Text;
using System.Text.Json;
using StealthEye.WorldKernel.Build001;
using StealthEye.WorldKernel.Campaign2Runner;

return await RunnerCli.RunAsync(args).ConfigureAwait(false);

internal static class RunnerCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h") { Help(); return 0; }
            var options = Parse(args.Skip(1));
            if (args[0] == "hashes") { await HashesAsync(options).ConfigureAwait(false); return 0; }
            var secret = Required(options, "secret-file");
            var evidence = Required(options, "evidence-root");
            await using var ledger = new Campaign2Ledger(secret, evidence);
            switch (args[0])
            {
                case "boundary":
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, state = await ledger.BoundaryAsync().ConfigureAwait(false) }, JsonDefaults.Options));
                    break;
                case "seed":
                    await ledger.RecordSeedCommitmentAsync(await LoadAsync<SeedCommitInput>(Required(options, "input")).ConfigureAwait(false)).ConfigureAwait(false);
                    Console.WriteLine("{\"ok\":true}");
                    break;
                case "hidden":
                    await ledger.RecordHiddenConfigurationAsync(await LoadAsync<HiddenConfigurationInput>(Required(options, "input")).ConfigureAwait(false)).ConfigureAwait(false);
                    Console.WriteLine("{\"ok\":true}");
                    break;
                case "reset":
                    await ledger.RecordResetVerificationAsync(await LoadAsync<ResetVerificationInput>(Required(options, "input")).ConfigureAwait(false)).ConfigureAwait(false);
                    Console.WriteLine("{\"ok\":true}");
                    break;
                case "randomize":
                    await ledger.RecordArmRandomizationAsync(await LoadAsync<ArmRandomizationInput>(Required(options, "input")).ConfigureAwait(false)).ConfigureAwait(false);
                    Console.WriteLine("{\"ok\":true}");
                    break;
                case "declare":
                {
                    var state = await ledger.DeclareTrialAsync(await LoadAsync<TrialDeclareInput>(Required(options, "input")).ConfigureAwait(false)).ConfigureAwait(false);
                    await SaveAsync(Required(options, "output"), state).ConfigureAwait(false);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, state_path = Required(options, "output"), state.ActionId, state.PredictionId }, JsonDefaults.Options));
                    break;
                }
                case "seal":
                {
                    var state = await LoadAsync<TrialLedgerState>(Required(options, "state")).ConfigureAwait(false);
                    var payload = await LoadElementAsync(Required(options, "input")).ConfigureAwait(false);
                    var phase = await ledger.SealDispatchAsync(state, payload).ConfigureAwait(false);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, action_phase_id = phase }, JsonDefaults.Options));
                    break;
                }
                case "close":
                {
                    var state = await LoadAsync<TrialLedgerState>(Required(options, "state")).ConfigureAwait(false);
                    var input = await LoadAsync<TrialCloseInput>(Required(options, "input")).ConfigureAwait(false);
                    var result = await ledger.CloseTrialAsync(state, input, Required(options, "episode-output")).ConfigureAwait(false);
                    if (options.TryGetValue("output", out var output)) await SaveAsync(output, result).ConfigureAwait(false);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, result }, JsonDefaults.Options));
                    break;
                }
                default: throw new ArgumentException("Unknown campaign2 runner command: " + args[0]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { ok = false, error_type = ex.GetType().FullName, error = ex.Message }, JsonDefaults.Options));
            return 1;
        }
    }

    private static async Task HashesAsync(IReadOnlyDictionary<string, string> options)
    {
        var root = Path.GetFullPath(Required(options, "repo-root"));
        static string NormalizedFileHash(string path)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            return CanonicalJson.Sha256(Encoding.UTF8.GetBytes(text));
        }
        var result = new
        {
            runner_version = "campaign2-runner-v1",
            memory_serializer_version = PackageBuilder.MemorySerializerVersion,
            memory_serializer_hash = PackageBuilder.MemorySerializerHash,
            structured_serializer_version = PackageBuilder.StructuredSerializerVersion,
            structured_serializer_hash = PackageBuilder.StructuredSerializerHash,
            scorer_version = Build001Contract.ScorerVersion,
            scorer_source_sha256 = NormalizedFileHash(Path.Combine(root, "src", "WorldKernel.Build001", "Scoring.cs")),
            evaluation_spec_version = Build001Contract.EvaluationSpecVersion,
            evaluation_spec_sha256 = Build001Contract.EvaluationSpecHash,
            runner_program_sha256 = NormalizedFileHash(Path.Combine(root, "tools", "WorldKernel.Campaign2Runner", "Program.cs")),
            runner_ledger_sha256 = NormalizedFileHash(Path.Combine(root, "tools", "WorldKernel.Campaign2Runner", "Ledger.cs")),
            runner_models_sha256 = NormalizedFileHash(Path.Combine(root, "tools", "WorldKernel.Campaign2Runner", "Models.cs"))
        };
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, hashes = result }, JsonDefaults.Options));
        await Task.CompletedTask;
    }

    private static async Task<T> LoadAsync<T>(string path) => JsonSerializer.Deserialize<T>(await File.ReadAllBytesAsync(path).ConfigureAwait(false), JsonDefaults.Options)
        ?? throw new InvalidDataException("Unable to deserialize " + path);
    private static async Task<JsonElement> LoadElementAsync(string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(path).ConfigureAwait(false));
        return doc.RootElement.Clone();
    }
    private static async Task SaveAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllBytesAsync(path, CanonicalJson.Serialize(value)).ConfigureAwait(false);
    }
    private static Dictionary<string, string> Parse(IEnumerable<string> values)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var e = values.GetEnumerator();
        while (e.MoveNext()) { var key = e.Current; if (!key.StartsWith("--", StringComparison.Ordinal) || !e.MoveNext()) throw new ArgumentException("Malformed option list."); result.Add(key[2..], e.Current); }
        return result;
    }
    private static string Required(IReadOnlyDictionary<string, string> options, string name) => options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing --" + name);
    private static void Help() => Console.WriteLine("campaign2 runner: hashes | boundary | seed | hidden | reset | randomize | declare | seal | close");
}
