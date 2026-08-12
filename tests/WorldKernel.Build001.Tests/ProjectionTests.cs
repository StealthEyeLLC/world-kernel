using Npgsql;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static partial class DatabaseTests
{
    public static async Task ProjectionNotRawEvidenceAsync(string secretFile)
    {
        await using var database = new KernelDb(ConnectionSecrets.ReadConnectionString(secretFile, "owner_connection"));
        var remote = Manifestation("github/provider", "github-repository", "projection-hostile-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        await database.InsertManifestationAsync(remote).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var projectionEvidence = FakeEvidence("world-kernel/projection", now) with
        {
            ObserverName = "world-kernel-projection",
            AcquisitionMethod = "rebuildable-current-projection"
        };
        await database.InsertEvidenceAsync(projectionEvidence).ConfigureAwait(false);
        var projectedObservation = Observation(remote.ManifestationId, projectionEvidence.EvidenceId, now) with
        {
            ProviderNamespace = "world-kernel/projection",
            ObserverName = "world-kernel-projection",
            ObserverVersion = "v1"
        };

        await AssertEx.ThrowsAsync<PostgresException>(
            () => database.InsertObservationAsync(projectedObservation),
            exception => exception.SqlState == "23514").ConfigureAwait(false);
    }
}