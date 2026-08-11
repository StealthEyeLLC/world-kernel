using System.Text.Json;
using Npgsql;

namespace StealthEye.WorldKernel.Build001;

public sealed record PreflightEvidence(string Path, string Sha256);

public sealed record PreflightGate(
    string Id,
    string Name,
    bool Passed,
    IReadOnlyList<string> Findings,
    IReadOnlyList<PreflightEvidence> Evidence);

public sealed record PreflightManifest(
    string Schema,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PreflightGate> Gates,
    bool AllPreflightGatesPassed,
    bool AcquisitionAuthorized,
    bool PilotAuthorized,
    bool ConfirmatoryAuthorized,
    bool DriftAuthorized,
    bool FirstConfirmatoryBlockStarted,
    string Decision);

public static class PreflightGateEvaluator
{
    public const string ManifestSchema = "world-kernel-build001-preflight-gates-v2-campaign2";

    public static PreflightManifest Evaluate(string artifactDirectory)
    {
        var p0 = Load(artifactDirectory, "p0-baseline.json");
        var p1 = Load(artifactDirectory, "p1-codeeye-live.json");
        var p2 = Load(artifactDirectory, "p2-eyebrowse-live.json");
        var p2Action = Load(artifactDirectory, "p2-eyebrowse-remote-commit.json", required: false);
        var p2Blocker = Load(artifactDirectory, "p2-eyebrowse-remote-commit-blocker.json", required: false);
        var p3Observation = Load(artifactDirectory, "p3-native-git-observation.json");
        var p3Branch = Load(artifactDirectory, "p3-create-branch.json");
        var p3Commit = Load(artifactDirectory, "p3-create-local-commit.json");
        var p3Push = Load(artifactDirectory, "p3-push-accepted.json");
        var p3Rejection = Load(artifactDirectory, "native-git-policy-rejection.json");
        var p3Fetch = Load(artifactDirectory, "p3-fetch.json");
        var p3Integrate = Load(artifactDirectory, "p3-integrate-fast-forward.json");
        var p4Projection = Load(artifactDirectory, "p4-projection-rebuild.json");
        var p4Tests = Load(artifactDirectory, "implementation-test-results.json");
        var p4Recovery = Load(artifactDirectory, "recovery-test.json");
        var p4Runtime = Load(artifactDirectory, "postgres-runtime-manifest.json");
        var p5 = Load(artifactDirectory, "p5-fresh-invocation.json", required: false) ??\n                 Load(artifactDirectory, "p5-fresh-invocation-blocker.json");
        var p6 = Load(artifactDirectory, "p6-deterministic-reset.json");

        var gates = new List<PreflightGate>
        {
            Gate(
                "P0",
                "Baseline capture",
                Campaign2Attestation.PassesP0(p0.Root),
                [
                    Campaign2Attestation.PassesP0(p0.Root)
                        ? "Observable ChatGPT product controls and their canonical fingerprint are attested."
                        : "Campaign 2 observable product/configuration attestation is absent or invalid.",
                    "No private OpenAI serving deployment identifier is claimed or hashed."
                ],
                [p0]),
            Gate(
                "P1",
                "Live CODEeye",
                Number(p1, "exit_code") == 0 &&
                String(p1, "payload", "observer") == "CODEeye.ProgramHost" &&
                Contains(p1, "\"repository_status\"") && Contains(p1, "\"world_sync\"") &&
                Contains(p1, "\"git_diff\"") && Contains(p1, "p3_local_change=1"),
                [
                    "Named-pipe Program Host probe returned current repository HEAD/branch.",
                    "Controlled local change and live world.sync provider incarnation were observed."
                ],
                [p1]),
            Gate(
                "P2",
                "Live authenticated eyeBROWSE",
                Number(p2, "exit_code") == 0 && IsTrue(p2, "payload", "signed_in") &&
                IsNonEmptyString(p2, "payload", "location", "user_login") &&
                ArrayLength(p2, "payload", "observation", "semantic_controls") > 0 &&
                p2Action is not null && Number(p2Action, "exit_code") == 0 &&
                Campaign2Attestation.PassesP2Action(p2Action.Root),
                [
                    IsTrue(p2, "payload", "signed_in")
                        ? "Authenticated browser identity observed."
                        : "Browser/kernel and semantic observation are live, but the fixture profile is anonymous.",
                    p2Action is not null && Number(p2Action, "exit_code") == 0
                        ? "Disposable browser-side remote commit completed."
                        : "No authenticated browser-side remote commit completed.",
                    p2Action is not null && IsTrue(p2Action, "stale_then_fresh_distinguished")
                        ? "Stale and fresh hosted state were distinguished."
                        : "The required authenticated stale/fresh action sequence could not run."
                ],
                Compact(p2, p2Action, p2Blocker)),
            Gate(
                "P3",
                "Experiment-owned native Git facet",
                ArrayLength(p3Observation, "observation", "commands") >= 5 &&
                Contains(p3Observation, "https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git") &&
                Contains(p3Observation, "refs/remotes/origin") &&
                IsAccepted(p3Branch) && IsAccepted(p3Commit) && IsAccepted(p3Push) &&
                !IsAccepted(p3Rejection) && Contains(p3Rejection, "GH006") &&
                IsAccepted(p3Fetch) && IsAccepted(p3Integrate),
                [
                    "Exact remote/ref/reachability observation, branch, commit, accepted push, fetch, and ff-only integration completed.",
                    "A real protected-branch GH006 rejection was captured as an unverified provider receipt."
                ],
                [p3Observation, p3Branch, p3Commit, p3Push, p3Rejection, p3Fetch, p3Integrate]),
            Gate(
                "P4",
                "PostgreSQL 18 runtime and recovery",
                String(p4Runtime, "postgres_version")?.Contains("18.4", StringComparison.Ordinal) == true &&
                String(p4Runtime, "listen_addresses") == "127.0.0.1" &&
                String(p4Runtime, "topology")?.Contains("no Windows SCM service", StringComparison.Ordinal) == true &&
                Number(p4Tests, "failed") == 0 && Number(p4Tests, "passed") >= 10 &&
                IsTrue(p4Recovery, "passed") && IsTrue(p4Projection, "identical") &&
                !IsTrue(p4Projection, "authoritative_current_state_table"),
                [
                    "Portable PostgreSQL 18.4 ran loopback-only without a Windows SCM service.",
                    "Schema/integrity tests, crash recovery, durable counts, and projection rebuild passed."
                ],
                [p4Runtime, p4Tests, p4Recovery, p4Projection]),
            Gate(
                "P5",
                "Fresh isolated cognitive invocation",
                Campaign2Attestation.PassesP5(p5.Root, p0.Root),
                [
                    Campaign2Attestation.PassesP5(p5.Root, p0.Root)
                        ? "Fresh Temporary Chat invocation and matching observable configuration passed."
                        : "No valid Campaign 2 fresh-invocation attestation matches the observable P0 configuration."
                ],
                [p5]),
            Gate(
                "P6",
                "Deterministic provider reset",
                IsTrue(p6, "passed") && IsTrue(p6, "same_seed_repeated_material_state") &&
                IsTrue(p6, "different_regimes_have_different_fingerprints") &&
                IsTrue(p6, "accepted_regime", "passed") && IsTrue(p6, "rejected_regime", "passed"),
                [
                    "Repeated accepted/rejected seed resets reproduced exact Git SHA and provider fingerprint.",
                    "Different hidden regimes produced different fingerprints."
                ],
                [p6])
        };

        var allPassed = gates.All(gate => gate.Passed);
        return new PreflightManifest(
            ManifestSchema,
            DateTimeOffset.UtcNow,
            gates,
            allPassed,
            allPassed,
            allPassed,
            allPassed,
            allPassed,
            false,
            allPassed
                ? "P0-P6 pass; phase execution may proceed subject to the frozen boundary contract."
                : "P0-P6 do not all pass; acquisition, pilot, confirmatory, and drift execution are prohibited.");
    }

    public static void EnsurePhaseAuthorized(string manifestPath, string phase)
    {
        if (phase is not ("acquisition" or "pilot" or "confirmatory" or "drift"))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a preregistered cognitive experiment phase.");
        }
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        if (!ReadBoolean(document.RootElement, "all_preflight_gates_passed"))
        {
            var failed = document.RootElement.GetProperty("gates").EnumerateArray()
                .Where(gate => !gate.GetProperty("passed").GetBoolean())
                .Select(gate => gate.GetProperty("id").GetString())
                .ToArray();
            throw new InvalidOperationException(
                $"Build 001 {phase} is blocked by frozen preflight gate(s): {string.Join(", ", failed)}.");
        }
    }

    private static PreflightGate Gate(
        string id,
        string name,
        bool passed,
        IReadOnlyList<string> findings,
        IReadOnlyList<Artifact> evidence) => new(
            id,
            name,
            passed,
            findings,
            evidence.Select(item => new PreflightEvidence(item.FileName, item.Sha256)).ToArray());

    private static IReadOnlyList<Artifact> Compact(params Artifact?[] artifacts) =>
        artifacts.Where(item => item is not null).Cast<Artifact>().ToArray();

    private static bool IsAccepted(Artifact artifact) =>
        IsTrue(artifact, "receipt_accepted") && Number(artifact, "exit_code") == 0;

    private static Artifact Load(string directory, string fileName, bool required = true)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            if (!required) return null!;
            throw new FileNotFoundException($"Required preflight artifact is absent: {fileName}", path);
        }
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        return new Artifact(fileName, CanonicalJson.Sha256(bytes), document.RootElement.Clone(), bytes);
    }

    private static JsonElement? At(Artifact artifact, params string[] path)
    {
        var current = artifact.Root;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current)) return null;
        }
        return current;
    }

    private static bool IsTrue(Artifact artifact, params string[] path) =>
        At(artifact, path) is { ValueKind: JsonValueKind.True };

    private static bool ReadBoolean(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? String(Artifact artifact, params string[] path) =>
        At(artifact, path) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool IsNonEmptyString(Artifact artifact, params string[] path) =>
        !string.IsNullOrWhiteSpace(String(artifact, path));

    private static bool IsSha256(Artifact artifact, params string[] path) =>
        String(artifact, path) is { Length: 64 } value && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static long Number(Artifact artifact, params string[] path) =>
        At(artifact, path) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number)
            ? number
            : long.MinValue;

    private static int ArrayLength(Artifact artifact, params string[] path) =>
        At(artifact, path) is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : -1;

    private static bool HasObject(Artifact artifact, params string[] path) =>
        At(artifact, path) is { ValueKind: JsonValueKind.Object };

    private static bool Contains(Artifact artifact, string value) =>
        System.Text.Encoding.UTF8.GetString(artifact.Bytes).Contains(value, StringComparison.Ordinal);

    private sealed record Artifact(string FileName, string Sha256, JsonElement Root, byte[] Bytes);
}

public sealed record FrozenPreregistration(
    string Schema,
    string ContractVersion,
    string MachinePreregistrationSha256,
    string HumanSpecSha256,
    string EvaluationSpecSha256,
    string PredictionScorerVersion,
    string AnalysisVersion,
    string MemorySerializerVersion,
    string MemorySerializerSha256,
    string StructuredSerializerVersion,
    string StructuredSerializerSha256,
    string ContractBlobRef,
    DateTimeOffset FrozenAt,
    bool ConfirmatoryStarted);

public static class EvaluatorBoundary
{
    public const string ContractVersion = "v1-original";
    public const string HumanSpecHash = "63804abcc376c4e2c27f242bee16e10678458b1befd5d1e4903ff54fa2d87696";
    public const string MachinePreregistrationHash = "cf4de0ea97cd394ec8ae9373617d253b9b220be11a6584d30380b0051a617b10";
    public const string AnalysisVersion = "build001-primary-analysis-v1";
    public const string FreezeCommit = "22f9f8e459d71f8df271078575c920db9c6469b2";

    public static async Task<FrozenPreregistration> FreezeOriginalAsync(
        string repositoryRoot,
        string evaluatorConnection,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var originalRoot = Path.Combine(repositoryRoot, "docs", "preregistration", "original");
        var humanPath = Path.Combine(originalRoot, "StealthEye_World_Kernel_Build_001_Spec_and_Preregistration.md");
        var machinePath = Path.Combine(originalRoot, "StealthEye_World_Kernel_Build_001_Preregistration.json");
        var evaluationPath = Path.Combine(repositoryRoot, "schemas", "evaluation-spec-v1.json");
        RequireHash(humanPath, HumanSpecHash);
        RequireHash(machinePath, MachinePreregistrationHash);
        RequireHash(evaluationPath, Build001Contract.EvaluationSpecHash);

        FrozenPreregistration frozen;
        if (File.Exists(outputPath))
        {
            frozen = JsonSerializer.Deserialize<FrozenPreregistration>(
                         File.ReadAllBytes(outputPath), JsonDefaults.Options)
                     ?? throw new InvalidDataException("Frozen preregistration artifact was empty.");
            ValidateFrozen(frozen);
        }
        else
        {
            frozen = new FrozenPreregistration(
                "world-kernel-build001-frozen-preregistration-v1",
                ContractVersion,
                MachinePreregistrationHash,
                HumanSpecHash,
                Build001Contract.EvaluationSpecHash,
                Build001Contract.ScorerVersion,
                AnalysisVersion,
                PackageBuilder.MemorySerializerVersion,
                PackageBuilder.MemorySerializerHash,
                PackageBuilder.StructuredSerializerVersion,
                PackageBuilder.StructuredSerializerHash,
                $"git:StealthEyeLLC/world-kernel@{FreezeCommit}:docs/preregistration/original",
                DateTimeOffset.UtcNow,
                false);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            await File.WriteAllBytesAsync(outputPath, CanonicalJson.Serialize(frozen), cancellationToken).ConfigureAwait(false);
        }

        var artifactHash = CanonicalJson.Sha256(await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false));
        await using var connection = new NpgsqlConnection(evaluatorConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var serializerJson = JsonSerializer.SerializeToElement(new
        {
            memory = new { version = frozen.MemorySerializerVersion, sha256 = frozen.MemorySerializerSha256 },
            structured = new { version = frozen.StructuredSerializerVersion, sha256 = frozen.StructuredSerializerSha256 },
            analysis_version = frozen.AnalysisVersion
        }, JsonDefaults.Options);

        await using (var insertContract = new NpgsqlCommand("""
            INSERT INTO eval001.preregistration_contract(
              contract_version,machine_preregistration_sha256,human_spec_sha256,
              evaluation_spec_sha256,scorer_version,serializer_versions,contract_blob_ref)
            VALUES (@version,@machine,@human,@evaluation,@scorer,@serializers,@blob)
            ON CONFLICT (contract_version) DO NOTHING;
            """, connection, transaction))
        {
            insertContract.Parameters.AddWithValue("version", frozen.ContractVersion);
            insertContract.Parameters.AddWithValue("machine", frozen.MachinePreregistrationSha256);
            insertContract.Parameters.AddWithValue("human", frozen.HumanSpecSha256);
            insertContract.Parameters.AddWithValue("evaluation", frozen.EvaluationSpecSha256);
            insertContract.Parameters.AddWithValue("scorer", $"{frozen.PredictionScorerVersion};{frozen.AnalysisVersion}");
            KernelDb.AddJson(insertContract, "serializers", serializerJson);
            insertContract.Parameters.AddWithValue("blob", frozen.ContractBlobRef);
            await insertContract.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var verify = new NpgsqlCommand("""
            SELECT machine_preregistration_sha256,human_spec_sha256,evaluation_spec_sha256,contract_blob_ref
            FROM eval001.preregistration_contract WHERE contract_version=@version;
            """, connection, transaction))
        {
            verify.Parameters.AddWithValue("version", frozen.ContractVersion);
            await using var reader = await verify.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
                reader.GetString(0) != frozen.MachinePreregistrationSha256 ||
                reader.GetString(1) != frozen.HumanSpecSha256 ||
                reader.GetString(2) != frozen.EvaluationSpecSha256 ||
                reader.GetString(3) != frozen.ContractBlobRef)
            {
                throw new InvalidOperationException("Evaluator preregistration contract differs from the immutable original.");
            }
        }

        await using (var boundary = new NpgsqlCommand("""
            INSERT INTO eval001.boundary_event(boundary_event_id,event_type,contract_version,evidence_hash,details,occurred_at)
            SELECT @id,'original_frozen',@version,@hash,@details,@occurred
            WHERE NOT EXISTS (
              SELECT 1 FROM eval001.boundary_event
              WHERE event_type='original_frozen' AND contract_version=@version
            );
            """, connection, transaction))
        {
            boundary.Parameters.AddWithValue("id", Guid.NewGuid());
            boundary.Parameters.AddWithValue("version", frozen.ContractVersion);
            boundary.Parameters.AddWithValue("hash", artifactHash);
            KernelDb.AddJson(boundary, "details", JsonSerializer.SerializeToElement(new
            {
                source = "immutable governing files",
                confirmatory_outcome_inspected = false,
                pilot_started = false
            }, JsonDefaults.Options));
            boundary.Parameters.AddWithValue("occurred", frozen.FrozenAt);
            await boundary.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var verifyBoundary = new NpgsqlCommand("""
            SELECT evidence_hash FROM eval001.boundary_event
            WHERE event_type='original_frozen' AND contract_version=@version
            ORDER BY recorded_at LIMIT 1;
            """, connection, transaction))
        {
            verifyBoundary.Parameters.AddWithValue("version", frozen.ContractVersion);
            var value = (string?)await verifyBoundary.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value != artifactHash)
            {
                throw new InvalidOperationException("Evaluator original_frozen boundary evidence hash differs.");
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return frozen;
    }

    private static void RequireHash(string path, string expected)
    {
        var actual = CanonicalJson.Sha256(File.ReadAllBytes(path));
        if (actual != expected) throw new InvalidDataException($"Governing file hash mismatch for {path}: {actual}");
    }

    private static void ValidateFrozen(FrozenPreregistration frozen)
    {
        if (frozen.ContractVersion != ContractVersion || frozen.HumanSpecSha256 != HumanSpecHash ||
            frozen.MachinePreregistrationSha256 != MachinePreregistrationHash ||
            frozen.EvaluationSpecSha256 != Build001Contract.EvaluationSpecHash || frozen.ConfirmatoryStarted)
        {
            throw new InvalidDataException("Frozen preregistration artifact conflicts with the immutable original boundary.");
        }
    }
}
