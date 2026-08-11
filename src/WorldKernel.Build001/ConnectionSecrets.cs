using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public static class ConnectionSecrets
{
    public static string ReadConnectionString(string secretFile, string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(secretFile));
        if (!document.RootElement.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Secret file does not contain string field '{key}'.");
        }
        return value.GetString() ?? throw new InvalidDataException($"Secret field '{key}' was null.");
    }
}
