using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class Campaign3BoundaryTests
{
    public static Task ScienceAuthorizationAsync()
    {
        var commit = new string('1', 40);
        var tree = new string('2', 40);
        var hash = new string('a', 64);
        var fingerprint = new string('b', 64);
        var freeze = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign3Execution.FreezeManifestSchema,
            campaign_id = Campaign3Execution.CampaignId,
            valid = true,
            single_prospective_freeze = true,
            implementation = new { commit, tree },
            subject_configuration_fingerprint_sha256 = fingerprint
        });
        JsonElement Authorization(string providerHead = "", bool postFreezeSourceChange = false)
        {
            if (string.IsNullOrEmpty(providerHead)) providerHead = commit;
            return JsonSerializer.SerializeToElement(new
            {
                schema = Campaign3Boundary.ScienceAuthorizationSchema,
                campaign_id = Campaign3Execution.CampaignId,
                authorized = true,
                branch = "build001-campaign-3",
                verified_at = "2026-08-12T08:10:00Z",
                provider_observer = "GitHub administration connector",
                post_freeze_source_change = postFreezeSourceChange,
                local_commit = commit,
                provider_head = providerHead,
                local_tree = tree,
                provider_tree = tree,
                freeze_manifest_sha256 = hash,
                provider_manifest_sha256 = hash,
                subject_configuration_fingerprint_sha256 = fingerprint,
                provider_verification_sha256 = new string('c', 64),
                preflight_manifest_sha256 = new string('d', 64),
                freeze_zero_state_sha256 = new string('e', 64),
                pre_science_zero_state_sha256 = new string('f', 64),
                p0_p6_all_passed = true,
                scientific_state_zero = true,
                external_eye_heads_verified = true
            });
        }
        AssertEx.True(Campaign3Boundary.PassesScienceAuthorization(Authorization(), freeze, hash), "Exact provider/local/freeze identity should authorize science.");
        AssertEx.False(Campaign3Boundary.PassesScienceAuthorization(Authorization(new string('9', 40)), freeze, hash), "Provider HEAD mismatch must fail science authorization.");
        AssertEx.False(Campaign3Boundary.PassesScienceAuthorization(Authorization(postFreezeSourceChange: true), freeze, hash), "Post-freeze source change must fail science authorization.");
        AssertEx.False(Campaign3Boundary.PassesScienceAuthorization(Authorization(), freeze, new string('0', 64)), "Freeze hash mismatch must fail science authorization.");
        return Task.CompletedTask;
    }
}