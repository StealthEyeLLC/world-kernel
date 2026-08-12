using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public static class Campaign3Boundary
{
    public const string ScienceAuthorizationSchema = "world-kernel-build001-campaign3-science-authorization-v1";
    public const string ProviderVerificationSchema = "world-kernel-build001-campaign3-provider-freeze-verification-v1";

    public static bool PassesScienceAuthorization(JsonElement authorization, JsonElement freeze, string freezeManifestSha256)
    {
        if (!IsSha256(freezeManifestSha256) ||
            String(freeze, "schema") != Campaign3Execution.FreezeManifestSchema ||
            String(freeze, "campaign_id") != Campaign3Execution.CampaignId ||
            !IsTrue(freeze, "valid") || !IsTrue(freeze, "single_prospective_freeze")) return false;

        var implementation = At(freeze, "implementation");
        if (implementation is not { ValueKind: JsonValueKind.Object } impl) return false;
        var frozenCommit = String(impl, "commit");
        var frozenTree = String(impl, "tree");
        var frozenFingerprint = String(freeze, "subject_configuration_fingerprint_sha256");

        return String(authorization, "schema") == ScienceAuthorizationSchema &&
               String(authorization, "campaign_id") == Campaign3Execution.CampaignId &&
               IsTrue(authorization, "authorized") &&
               String(authorization, "branch") == "build001-campaign-3" &&
               HasTimestamp(authorization, "verified_at") &&
               String(authorization, "provider_observer") == "GitHub administration connector" &&
               IsFalse(authorization, "post_freeze_source_change") &&
               String(authorization, "local_commit") == frozenCommit &&
               String(authorization, "provider_head") == frozenCommit &&
               String(authorization, "local_tree") == frozenTree &&
               String(authorization, "provider_tree") == frozenTree &&
               String(authorization, "freeze_manifest_sha256") == freezeManifestSha256 &&
               String(authorization, "provider_manifest_sha256") == freezeManifestSha256 &&
               String(authorization, "subject_configuration_fingerprint_sha256") == frozenFingerprint &&
               IsSha256(String(authorization, "provider_verification_sha256")) &&
               IsSha256(String(authorization, "preflight_manifest_sha256")) &&
               IsSha256(String(authorization, "freeze_zero_state_sha256")) &&
               IsSha256(String(authorization, "pre_science_zero_state_sha256")) &&
               IsTrue(authorization, "p0_p6_all_passed") &&
               IsTrue(authorization, "scientific_state_zero") &&
               IsTrue(authorization, "external_eye_heads_verified");
    }

    private static JsonElement? At(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current)) return null;
        }
        return current;
    }

    private static string? String(JsonElement root, params string[] path) =>
        At(root, path) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool IsTrue(JsonElement root, params string[] path) => At(root, path) is { ValueKind: JsonValueKind.True };
    private static bool IsFalse(JsonElement root, params string[] path) => At(root, path) is { ValueKind: JsonValueKind.False };
    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool HasTimestamp(JsonElement root, params string[] path) =>
        DateTimeOffset.TryParse(String(root, path), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _);
}