using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public sealed class EvidenceStore
{
    private readonly string _root;

    public EvidenceStore(string root)
    {
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public async Task<EvidenceRecord> PutAsync(
        ReadOnlyMemory<byte> bytes,
        string providerNamespace,
        string observerName,
        string mediaType,
        string acquisitionMethod,
        DateTimeOffset capturedAt,
        string? providerRevision = null,
        DateTimeOffset? providerEventAt = null,
        string? encoding = null,
        JsonElement? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var hash = CanonicalJson.Sha256(bytes.Span);
        var relative = Path.Combine("sha256", hash[..2], hash.Substring(2, 2), hash);
        var destination = Path.Combine(_root, relative);
        var destinationDirectory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destinationDirectory);

        if (File.Exists(destination))
        {
            await VerifyFileAsync(destination, hash, bytes.Length, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var temporary = Path.Combine(destinationDirectory, $".{hash}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(
                                 temporary,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 64 * 1024,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    File.Move(temporary, destination, overwrite: false);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    await VerifyFileAsync(destination, hash, bytes.Length, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        return new EvidenceRecord(
            Guid.NewGuid(),
            providerNamespace,
            observerName,
            capturedAt,
            "sha256",
            hash,
            relative.Replace(Path.DirectorySeparatorChar, '/'),
            mediaType,
            acquisitionMethod,
            bytes.Length,
            providerRevision,
            providerEventAt,
            encoding,
            metadata ?? JsonDefaults.EmptyObject);
    }

    public async Task<byte[]> ReadVerifiedAsync(EvidenceRecord record, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(record.HashAlgorithm, "sha256", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported evidence hash algorithm {record.HashAlgorithm}.");
        }

        var path = ResolveBlobRef(record.BlobRef);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.LongLength != record.ByteLength || !string.Equals(CanonicalJson.Sha256(bytes), record.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Evidence blob {record.BlobRef} failed immutable hash verification.");
        }
        return bytes;
    }

    public string ResolveBlobRef(string blobRef)
    {
        var relative = blobRef.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(_root, relative));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Evidence blob reference escapes the configured content-addressed root.");
        }
        return resolved;
    }

    private static async Task VerifyFileAsync(
        string path,
        string expectedHash,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length != expectedLength)
        {
            throw new InvalidDataException($"Existing evidence blob {path} has the wrong byte length.");
        }
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(CanonicalJson.Sha256(bytes), expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Existing evidence blob {path} failed hash verification.");
        }
    }
}
