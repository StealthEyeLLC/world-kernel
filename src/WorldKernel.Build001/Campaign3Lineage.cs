using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public static partial class Campaign3Execution
{
    private sealed record RemoteRefLineage(ObservationRecord Observation, EvidenceRecord Evidence, byte[] Bytes);

    private static JsonElement BuildRemoteRefProviderPayload(Campaign3StateObservation state)
    {
        if (state.Commands.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Campaign 3 state observation does not retain the provider command transcript.");

        var expectedRef = $"refs/heads/{state.Branch}";
        var expectedArguments = new[] { "ls-remote", "--heads", "origin", expectedRef };
        JsonElement? matched = null;
        foreach (var command in state.Commands.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object ||
                !command.TryGetProperty("arguments", out var arguments) ||
                arguments.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var values = arguments.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!)
                .ToArray();
            if (values.SequenceEqual(expectedArguments, StringComparer.Ordinal))
            {
                if (matched is not null)
                    throw new InvalidDataException("Campaign 3 state observation contains duplicate hosted-ref provider queries.");
                matched = command.Clone();
            }
        }

        if (matched is not JsonElement providerCommand)
            throw new InvalidDataException("Campaign 3 state observation lacks the exact hosted-ref provider query.");
        if (!providerCommand.TryGetProperty("exit_code", out var exitCodeElement) ||
            exitCodeElement.ValueKind != JsonValueKind.Number || !exitCodeElement.TryGetInt32(out var exitCode) || exitCode != 0)
            throw new InvalidDataException("Campaign 3 hosted-ref provider query did not succeed.");
        if (!providerCommand.TryGetProperty("output", out var outputElement) || outputElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Campaign 3 hosted-ref provider query lacks retained output.");

        var output = outputElement.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (output.Length > 1)
            throw new InvalidDataException("Campaign 3 hosted-ref provider query returned more than one exact ref.");

        string? providerHead = null;
        if (output.Length == 1)
        {
            var parts = output[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length != 40 || parts[0].Any(character => !Uri.IsHexDigit(character)) ||
                !string.Equals(parts[1], expectedRef, StringComparison.Ordinal))
                throw new InvalidDataException("Campaign 3 hosted-ref provider query returned an invalid exact-ref row.");
            providerHead = parts[0].ToLowerInvariant();
        }

        if (!string.Equals(providerHead, state.RemoteHead, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Campaign 3 retained remote_head differs from the hosted-ref provider query.");

        return JsonSerializer.SerializeToElement(new
        {
            schema = "world-kernel-build001-campaign3-hosted-ref-observation-v1",
            repository = FixtureRepository,
            branch = state.Branch,
            remote_url = state.RemoteUrl,
            acquisition_method = "git-ls-remote-exact-hosted-ref",
            observed_at = state.ObservedAt,
            remote_head = providerHead,
            command = new
            {
                arguments = expectedArguments,
                exit_code = exitCode,
                output
            }
        }, JsonDefaults.Options);
    }

    private static async Task<RemoteRefLineage> EnsureRemoteRefLineageAsync(
        KernelDb database,
        EvidenceStore store,
        Guid remoteManifestationId,
        Campaign3StateObservation state,
        CancellationToken cancellationToken)
    {
        var payload = BuildRemoteRefProviderPayload(state);
        var bytes = CanonicalJson.Serialize(payload);
        var evidence = await store.PutAsync(
            bytes,
            "github/provider",
            "campaign3-github-ref-observer",
            "application/json",
            "git-ls-remote-exact-hosted-ref",
            state.ObservedAt,
            providerRevision: state.RemoteHead,
            encoding: "utf-8",
            metadata: JsonSerializer.SerializeToElement(new
            {
                provider_endpoint = "github-git-transport",
                subject_semantics = "hosted-ref",
                local_state_authority = false,
                campaign_id = CampaignId
            }, JsonDefaults.Options),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        evidence = await EnsureEvidenceAsync(database, evidence, cancellationToken).ConfigureAwait(false);

        var observation = new ObservationRecord(
            Guid.NewGuid(),
            remoteManifestationId,
            "campaign3-github-ref-observer",
            "campaign3-github-ref-observation-v1",
            "github/provider",
            state.ObservedAt,
            "succeeded",
            JsonSerializer.SerializeToElement(new { complete = true, exact_ref = true, provider_native = true }, JsonDefaults.Options),
            state.RemoteHead,
            null,
            JsonSerializer.SerializeToElement(new
            {
                dependency_group = "github-git-transport",
                correspondence_not_authority = true
            }, JsonDefaults.Options),
            payload,
            [evidence.EvidenceId]);
        observation = await EnsureObservationAsync(database, observation, cancellationToken).ConfigureAwait(false);
        return new RemoteRefLineage(observation, evidence, bytes);
    }
}