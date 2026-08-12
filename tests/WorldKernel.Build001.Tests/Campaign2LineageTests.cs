using System.Text;
using System.Text.Json;
using Npgsql;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static partial class DatabaseTests
{
    public static async Task ClaimLineageRunANegativeAsync(string secretFile)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var local = Manifestation("codeeye/git-local", "git-working-copy", "c2r-negative-local-" + Guid.NewGuid().ToString("N"));
        var remote = Manifestation("github/provider", "github-repository", "c2r-negative-remote-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(local).ConfigureAwait(false);
        await database.InsertManifestationAsync(remote).ConfigureAwait(false);
        var capturedAt = DateTimeOffset.UtcNow;
        var localEvidence = FakeEvidence("git/native", capturedAt);
        await database.InsertEvidenceAsync(localEvidence).ConfigureAwait(false);
        var localObservation = Observation(local.ManifestationId, localEvidence.EvidenceId, capturedAt) with
        {
            ProviderNamespace = "git/native",
            ObserverName = "campaign2-state-observer"
        };
        await database.InsertObservationAsync(localObservation).ConfigureAwait(false);

        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertMaterialClaimAsync(database, Guid.NewGuid(), remote.ManifestationId,
                localObservation.ObservationId, localEvidence.EvidenceId, "github", "remote_ref_head",
                JsonSerializer.SerializeToElement("deadbeef"), capturedAt),
            exception => exception.SqlState == "23514").ConfigureAwait(false);
    }

    public static async Task ClaimLineageProviderMatchedPositiveAsync(string secretFile)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var remote = Manifestation("github/provider", "github-repository", "c2r-positive-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(remote).ConfigureAwait(false);
        var capturedAt = DateTimeOffset.UtcNow;
        var evidence = FakeEvidence("github/provider", capturedAt) with
        {
            ObserverName = "campaign2-github-ref-observer",
            AcquisitionMethod = "git-ls-remote-exact-hosted-ref"
        };
        await database.InsertEvidenceAsync(evidence).ConfigureAwait(false);
        var observation = Observation(remote.ManifestationId, evidence.EvidenceId, capturedAt) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "campaign2-github-ref-observer",
            ObserverVersion = "campaign2r-github-ref-observation-v1"
        };
        await database.InsertObservationAsync(observation).ConfigureAwait(false);
        var claimId = Guid.NewGuid();
        await InsertMaterialClaimAsync(database, claimId, remote.ManifestationId, observation.ObservationId,
            evidence.EvidenceId, "github", "remote_ref_head", JsonSerializer.SerializeToElement("cafebabe"), capturedAt).ConfigureAwait(false);
        var count = await ScalarAsync<long>(database, "SELECT count(*) FROM wk.claim WHERE claim_id=@id;", ("id", claimId)).ConfigureAwait(false);
        AssertEx.Equal(1L, count, "Provider-matched GitHub Claim must be accepted.");
    }

    public static async Task ClaimLineageCrossProviderHostilesAsync(string secretFile)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var local = Manifestation("codeeye/git-local", "git-working-copy", "c2r-hostile-local-" + Guid.NewGuid().ToString("N"));
        var remote = Manifestation("github/provider", "github-repository", "c2r-hostile-remote-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(local).ConfigureAwait(false);
        await database.InsertManifestationAsync(remote).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        var githubEvidence = FakeEvidence("github/provider", now);
        await database.InsertEvidenceAsync(githubEvidence).ConfigureAwait(false);
        var githubAsLocalObservation = Observation(local.ManifestationId, githubEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "github-hostile"
        };
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.InsertObservationAsync(githubAsLocalObservation),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var staleEvidence = FakeEvidence("github/provider", now.AddMinutes(-30)) with
        {
            ObserverName = "eyeBROWSE",
            AcquisitionMethod = "browser-presentation"
        };
        await database.InsertEvidenceAsync(staleEvidence).ConfigureAwait(false);
        var staleObservation = Observation(remote.ManifestationId, staleEvidence.EvidenceId, now.AddMinutes(-30)) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "eyeBROWSE",
            AcquisitionStatus = "stale"
        };
        await database.InsertObservationAsync(staleObservation).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertMaterialClaimAsync(database, Guid.NewGuid(), remote.ManifestationId,
                staleObservation.ObservationId, staleEvidence.EvidenceId, "github", "remote_ref_head",
                JsonSerializer.SerializeToElement("stale"), now),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var replayedFreshObservation = Observation(remote.ManifestationId, staleEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "eyeBROWSE",
            AcquisitionStatus = "succeeded"
        };
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.InsertObservationAsync(replayedFreshObservation),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var inferenceEvidence = FakeEvidence("model/inference", now) with { ObserverName = "model-inference" };
        await database.InsertEvidenceAsync(inferenceEvidence).ConfigureAwait(false);
        var inferenceObservation = Observation(remote.ManifestationId, inferenceEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "model/inference",
            ObserverName = "model-inference"
        };
        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.InsertObservationAsync(inferenceObservation),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var receiptEvidence = FakeEvidence("github/provider", now) with
        {
            ObserverName = "eyeBROWSE",
            AcquisitionMethod = "provider-action-receipt"
        };
        await database.InsertEvidenceAsync(receiptEvidence).ConfigureAwait(false);
        var receiptObservation = Observation(remote.ManifestationId, receiptEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "eyeBROWSE"
        };
        await database.InsertObservationAsync(receiptObservation).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertMaterialClaimAsync(database, Guid.NewGuid(), remote.ManifestationId,
                receiptObservation.ObservationId, receiptEvidence.EvidenceId, "github", "remote_ref_head",
                JsonSerializer.SerializeToElement("receipt-only"), now),
            exception => exception.SqlState == "23514").ConfigureAwait(false);

        var cleanEvidence = FakeEvidence("github/provider", now) with { ObserverName = "github-provider" };
        await database.InsertEvidenceAsync(cleanEvidence).ConfigureAwait(false);
        var cleanObservation = Observation(remote.ManifestationId, cleanEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "github/provider",
            ObserverName = "github-provider"
        };
        await database.InsertObservationAsync(cleanObservation).ConfigureAwait(false);
        var action = await DeclareActionAsync(database, remote.ManifestationId, "github:create_remote_commit", "prediction-lineage").ConfigureAwait(false);
        var prediction = Prediction(action, "github:create_remote_commit");
        await database.CommitPredictionAsync(prediction).ConfigureAwait(false);
        await AssertEx.ThrowsAsync<PostgresException>(
            () => InsertMaterialClaimAsync(database, Guid.NewGuid(), remote.ManifestationId,
                cleanObservation.ObservationId, prediction.PredictionId, "github", "remote_ref_head",
                JsonSerializer.SerializeToElement("prediction-as-evidence"), now),
            exception => exception.SqlState == "23503" || exception.SqlState == "23514").ConfigureAwait(false);
    }
}
