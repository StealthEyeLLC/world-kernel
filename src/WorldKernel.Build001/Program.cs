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
                case "git-observe":
                    await ObserveGitAsync(options).ConfigureAwait(false);
                    return 0;
                case "git-action":
                    await RunGitActionAsync(options).ConfigureAwait(false);
                    return 0;
                case "codeeye-observe":
                    await ObserveCodeEyeAsync(options).ConfigureAwait(false);
                    return 0;
                case "eyebrowse-preflight":
                    await PreflightEyeBrowseAsync(options).ConfigureAwait(false);
                    return 0;
                case "eyebrowse-remote-commit":
                    await CommitThroughEyeBrowseAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-compose-p0":
                    await ComposeCampaign3P0Async(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-register-block":
                    await RegisterCampaign2BlockAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-register-reset":
                    await RegisterCampaign2ResetAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-lineage-harness":
                    await RunCampaign2LineageHarnessAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-begin":
                    await BeginCampaign2EpisodeAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-close":
                    await CloseCampaign2EpisodeAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign2-coverage":
                    await WriteCampaign2CoverageAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-register-block":
                    await RegisterCampaign3BlockAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-register-reset":
                    await RegisterCampaign3ResetAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-lineage-harness":
                    await RunCampaign3LineageHarnessAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-begin":
                    await BeginCampaign3EpisodeAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-close":
                    await CloseCampaign3EpisodeAsync(options).ConfigureAwait(false);
                    return 0;
                case "campaign3-coverage":
                    await WriteCampaign3CoverageAsync(options).ConfigureAwait(false);
                    return 0;
                case "freeze-preregistration":
                    await FreezePreregistrationAsync(options).ConfigureAwait(false);
                    return 0;
                case "preflight-evaluate":
                    await EvaluatePreflightAsync(options).ConfigureAwait(false);
                    return 0;
                case "phase-authorize":
                    AuthorizePhase(options);
                    return 0;
                case "overhead-measure":
                    await MeasureOverheadAsync(options).ConfigureAwait(false);
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

    private static NativeGitFacet CreateGitFacet(IReadOnlyDictionary<string, string> options) => new(
        Required(options, "git-executable"),
        Required(options, "fixture-root"));

    private static async Task ObserveGitAsync(IReadOnlyDictionary<string, string> options)
    {
        var observation = await CreateGitFacet(options)
            .ObserveAsync(Required(options, "working-copy"))
            .ConfigureAwait(false);
        var evidence = CanonicalJson.Canonicalize(observation);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            evidence_sha256 = CanonicalJson.Sha256(evidence),
            observation
        }, JsonDefaults.Options));
    }

    private static async Task RunGitActionAsync(IReadOnlyDictionary<string, string> options)
    {
        var facet = CreateGitFacet(options);
        var workingCopy = Required(options, "working-copy");
        var semanticAction = Required(options, "semantic-action");
        ProviderOperationResult result = semanticAction switch
        {
            "git:create_local_commit" => await facet.CreateLocalCommitAsync(
                workingCopy,
                Required(options, "relative-path"),
                Required(options, "message"),
                DateTimeOffset.Parse(Required(options, "timestamp"), System.Globalization.CultureInfo.InvariantCulture),
                default).ConfigureAwait(false),
            "git:create_branch" => await facet.CreateBranchAsync(
                workingCopy,
                Required(options, "branch"),
                default).ConfigureAwait(false),
            "git:push_ref" => await facet.PushRefAsync(
                workingCopy,
                Required(options, "branch"),
                default).ConfigureAwait(false),
            "git:fetch_remote" => await facet.FetchRemoteAsync(workingCopy, default).ConfigureAwait(false),
            "git:integrate_fast_forward" => await facet.IntegrateFastForwardAsync(
                workingCopy,
                Required(options, "branch"),
                default).ConfigureAwait(false),
            _ => throw new ArgumentException($"'{semanticAction}' is not owned by the experiment-native Git facet.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            semantic_action = result.SemanticAction,
            receipt_accepted = result.ReceiptAccepted,
            result.ExitCode,
            result.StartedAt,
            result.CompletedAt,
            evidence_sha256 = CanonicalJson.Sha256(result.EvidenceBytes),
            receipt = result.TypedReceipt
        }, JsonDefaults.Options));
    }

    private static ProgramHostAdapter CreateProgramHost(IReadOnlyDictionary<string, string> options) => new(
        Required(options, "node-executable"),
        Required(options, "scripts-root"));

    private static async Task ObserveCodeEyeAsync(IReadOnlyDictionary<string, string> options)
    {
        var result = await CreateProgramHost(options).ObserveCodeEyeAsync(
            Required(options, "sdk-path"),
            Required(options, "solution-path"),
            options.GetValueOrDefault("pipe", "codeeye-dev")).ConfigureAwait(false);
        WriteProgramHostResult(result);
    }

    private static async Task PreflightEyeBrowseAsync(IReadOnlyDictionary<string, string> options)
    {
        var result = await CreateProgramHost(options).PreflightEyeBrowseGitHubAsync(
            Required(options, "sdk-path"),
            Required(options, "repository-url")).ConfigureAwait(false);
        WriteProgramHostResult(result);
    }

    private static async Task CommitThroughEyeBrowseAsync(IReadOnlyDictionary<string, string> options)
    {
        var result = await CreateProgramHost(options).CreateRemoteCommitAsync(
            Required(options, "sdk-path"),
            Required(options, "branch"),
            Required(options, "file"),
            Required(options, "text"),
            Required(options, "message")).ConfigureAwait(false);
        WriteProgramHostResult(result);
    }

    private static void WriteProgramHostResult(ProgramHostResult result) => Console.WriteLine(JsonSerializer.Serialize(new
    {
        ok = true,
        result.ScriptName,
        result.ExitCode,
        result.StartedAt,
        result.CompletedAt,
        evidence_sha256 = CanonicalJson.Sha256(result.EvidenceBytes),
        result.Payload
    }, JsonDefaults.Options));

    private static async Task ComposeCampaign3P0Async(IReadOnlyDictionary<string, string> options)
    {
        var output = Required(options, "output");
        var bytes = Campaign3P0Composer.Compose(
            Required(options, "inspect-result"),
            Required(options, "personalization-observation"),
            Required(options, "base-prompt"),
            Required(options, "tool-contract"),
            Required(options, "trial-output-contract"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, bytes).ConfigureAwait(false);
        using var document = JsonDocument.Parse(bytes);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            output,
            output_sha256 = CanonicalJson.Sha256(bytes),
            observable_configuration_fingerprint_sha256 = document.RootElement.GetProperty("observable_configuration_fingerprint_sha256").GetString()
        }, JsonDefaults.Options));
    }

    private static async Task RegisterCampaign2BlockAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign2Execution.RegisterAcquisitionBlockAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "input"),
            Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.ConfigurationBlockId,
            record.SeedId,
            record.CommitmentSha256,
            output = Required(options, "output")
        }, JsonDefaults.Options));
    }

    private static async Task RegisterCampaign2ResetAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign2Execution.RegisterAcquisitionResetAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "configuration-block"),
            Required(options, "seed-id"),
            Required(options, "reset-manifest"),
            Required(options, "verification"),
            Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.ConfigurationBlockId,
            record.SeedId,
            record.GenerationId,
            record.ActualFingerprint,
            output = Required(options, "output")
        }, JsonDefaults.Options));
    }
    private static async Task RunCampaign2LineageHarnessAsync(IReadOnlyDictionary<string, string> options)
    {
        var result = await Campaign2Execution.RunPrefreezeLineageHarnessAsync(
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "working-copy"),
            Required(options, "state-observation"),
            Required(options, "semantic-action")).ConfigureAwait(false);
        var output = Required(options, "output");
        var bytes = CanonicalJson.Serialize(result);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, bytes).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, result.SemanticAction, result.TrialId, result.ActionId, result.PredictionId, result.DispatchSealPhaseId, result.PredictionBeforeDispatch, result.RemoteClaimSubjectMatched, result.RemoteClaimProviderMatched, result.LocalClaimsSubjectMatched, result.OutcomeCount, result.PredictionEvaluationCount, result.TransitionEpisodeCount, result.MaterialActionDispatched, output, output_sha256 = CanonicalJson.Sha256(bytes) }, JsonDefaults.Options));
    }

    private static async Task BeginCampaign2EpisodeAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign2Execution.BeginAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "input"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            phase = record.Phase,
            record.TrialId,
            record.ActionId,
            record.PredictionId,
            record.DispatchPhaseId,
            record.DispatchedAt,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }

    private static async Task CloseCampaign2EpisodeAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign2Execution.CloseAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "begin"),
            Required(options, "receipt"),
            Required(options, "post-observation"),
            Required(options, "provider-outcome"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.TrialId,
            record.EpisodeId,
            record.EligibilityStatus,
            record.MeanBrierLoss,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }

    private static async Task WriteCampaign2CoverageAsync(IReadOnlyDictionary<string, string> options)
    {
        var coverage = await Campaign2Execution.WriteCoverageAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            coverage.ConfigurationBlocks,
            coverage.StopRuleSatisfied,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }


    private static async Task RegisterCampaign3BlockAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign3Execution.RegisterAcquisitionBlockAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "input"),
            Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.ConfigurationBlockId,
            record.SeedId,
            record.CommitmentSha256,
            output = Required(options, "output")
        }, JsonDefaults.Options));
    }

    private static async Task RegisterCampaign3ResetAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign3Execution.RegisterAcquisitionResetAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "configuration-block"),
            Required(options, "seed-id"),
            Required(options, "reset-manifest"),
            Required(options, "verification"),
            Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.ConfigurationBlockId,
            record.SeedId,
            record.GenerationId,
            record.ActualFingerprint,
            output = Required(options, "output")
        }, JsonDefaults.Options));
    }
    private static async Task RunCampaign3LineageHarnessAsync(IReadOnlyDictionary<string, string> options)
    {
        var result = await Campaign3Execution.RunPrefreezeLineageHarnessAsync(
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "working-copy"),
            Required(options, "state-observation"),
            Required(options, "semantic-action")).ConfigureAwait(false);
        var output = Required(options, "output");
        var bytes = CanonicalJson.Serialize(result);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, bytes).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, result.SemanticAction, result.TrialId, result.ActionId, result.PredictionId, result.DispatchSealPhaseId, result.PredictionBeforeDispatch, result.RemoteClaimSubjectMatched, result.RemoteClaimProviderMatched, result.LocalClaimsSubjectMatched, result.OutcomeCount, result.PredictionEvaluationCount, result.TransitionEpisodeCount, result.MaterialActionDispatched, output, output_sha256 = CanonicalJson.Sha256(bytes) }, JsonDefaults.Options));
    }

    private static async Task BeginCampaign3EpisodeAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign3Execution.BeginAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "input"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            phase = record.Phase,
            record.TrialId,
            record.ActionId,
            record.PredictionId,
            record.DispatchPhaseId,
            record.DispatchedAt,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }

    private static async Task CloseCampaign3EpisodeAsync(IReadOnlyDictionary<string, string> options)
    {
        var record = await Campaign3Execution.CloseAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "evidence-root"),
            Required(options, "begin"),
            Required(options, "receipt"),
            Required(options, "post-observation"),
            Required(options, "provider-outcome"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            record.TrialId,
            record.EpisodeId,
            record.EligibilityStatus,
            record.MeanBrierLoss,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }

    private static async Task WriteCampaign3CoverageAsync(IReadOnlyDictionary<string, string> options)
    {
        var coverage = await Campaign3Execution.WriteCoverageAsync(
            Required(options, "repo-root"),
            Required(options, "secret-file"),
            Required(options, "output")).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(Required(options, "output")).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            coverage.ConfigurationBlocks,
            coverage.StopRuleSatisfied,
            output = Required(options, "output"),
            output_sha256 = CanonicalJson.Sha256(bytes)
        }, JsonDefaults.Options));
    }

    private static async Task FreezePreregistrationAsync(IReadOnlyDictionary<string, string> options)
    {
        var repositoryRoot = Required(options, "repo-root");
        var connection = ConnectionSecrets.ReadConnectionString(Required(options, "secret-file"), "evaluator_connection");
        var output = Required(options, "output");
        var frozen = await EvaluatorBoundary.FreezeOriginalAsync(repositoryRoot, connection, output).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(output).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            output,
            output_sha256 = CanonicalJson.Sha256(bytes),
            contract = frozen
        }, JsonDefaults.Options));
    }

    private static async Task EvaluatePreflightAsync(IReadOnlyDictionary<string, string> options)
    {
        var output = Required(options, "output");
        var manifest = PreflightGateEvaluator.Evaluate(Required(options, "artifact-directory"));
        var bytes = CanonicalJson.Serialize(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, bytes).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            output,
            output_sha256 = CanonicalJson.Sha256(bytes),
            manifest.AllPreflightGatesPassed,
            failed_gates = manifest.Gates.Where(gate => !gate.Passed).Select(gate => gate.Id).ToArray()
        }, JsonDefaults.Options));
    }

    private static void AuthorizePhase(IReadOnlyDictionary<string, string> options)
    {
        var phase = Required(options, "phase");
        PreflightGateEvaluator.EnsurePhaseAuthorized(Required(options, "preflight-manifest"), phase);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, phase, authorized = true }, JsonDefaults.Options));
    }

    private static async Task MeasureOverheadAsync(IReadOnlyDictionary<string, string> options)
    {
        var secretFile = Required(options, "secret-file");
        var result = await OverheadBenchmark.MeasureAsync(
            ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"),
            ConnectionSecrets.ReadConnectionString(secretFile, "evaluator_connection"),
            Required(options, "evidence-root"),
            Required(options, "artifact-root")).ConfigureAwait(false);
        var bytes = CanonicalJson.Serialize(result);
        var output = Required(options, "output");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllBytesAsync(output, bytes).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, output, output_sha256 = CanonicalJson.Sha256(bytes) }, JsonDefaults.Options));
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
          git-observe --git-executable PATH --fixture-root PATH --working-copy PATH
          git-action --semantic-action ACTION --git-executable PATH --fixture-root PATH --working-copy PATH [action options]
          codeeye-observe --node-executable PATH --scripts-root PATH --sdk-path PATH --solution-path PATH
          eyebrowse-preflight --node-executable PATH --scripts-root PATH --sdk-path PATH --repository-url URL
          eyebrowse-remote-commit --node-executable PATH --scripts-root PATH --sdk-path PATH --branch NAME --file PATH --text TEXT --message TEXT
          campaign3-compose-p0 --inspect-result PATH --personalization-observation PATH --base-prompt PATH --tool-contract PATH --trial-output-contract PATH --output PATH
          campaign2-register-block --repo-root PATH --secret-file PATH --input PATH --output PATH
          campaign2-register-reset --repo-root PATH --secret-file PATH --configuration-block ID --seed-id ID --reset-manifest PATH --verification PATH --output PATH
          campaign2-begin --repo-root PATH --secret-file PATH --evidence-root PATH --input PATH --output PATH
          campaign2-close --repo-root PATH --secret-file PATH --evidence-root PATH --begin PATH --receipt PATH --post-observation PATH --provider-outcome PATH --output PATH
          campaign2-coverage --repo-root PATH --secret-file PATH --output PATH
          campaign3-register-block --repo-root PATH --secret-file PATH --input PATH --output PATH
          campaign3-register-reset --repo-root PATH --secret-file PATH --configuration-block ID --seed-id ID --reset-manifest PATH --verification PATH --output PATH
          campaign3-begin --repo-root PATH --secret-file PATH --evidence-root PATH --input PATH --output PATH
          campaign3-close --repo-root PATH --secret-file PATH --evidence-root PATH --begin PATH --receipt PATH --post-observation PATH --provider-outcome PATH --output PATH
          campaign3-coverage --repo-root PATH --secret-file PATH --output PATH
          freeze-preregistration --repo-root PATH --secret-file PATH --output PATH
          preflight-evaluate --artifact-directory PATH --output PATH
          phase-authorize --preflight-manifest PATH --phase acquisition|pilot|confirmatory|drift
          overhead-measure --secret-file PATH --evidence-root PATH --artifact-root PATH --output PATH
        """);
}
