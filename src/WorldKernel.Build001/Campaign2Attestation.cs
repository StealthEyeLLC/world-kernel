using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

/// <summary>
/// Validates Campaign 2 attestations from observable product controls. It deliberately
/// does not treat an unavailable private OpenAI deployment identifier as evidence.
/// </summary>
public static class Campaign2Attestation
{
    public const string ProductSchema = "world-kernel-build001-campaign2-product-attestation-v1";
    public const string FreshInvocationSchema = "world-kernel-build001-campaign2-fresh-invocation-v1";
    public const string ProductMethodVersion = "campaign2-chatgpt-observable-controls-v1";
    public const string FreshInvocationMethodVersion = "campaign2-temporary-chat-isolation-v1";
    public const string InvalidationRulesVersion = "campaign2-trial-invalidation-v1";

    public static bool PassesP0(JsonElement root)
    {
        var configuration = At(root, "observable_configuration");
        return String(root, "schema") == ProductSchema &&
               IsTrue(root, "passed") &&
               String(root, "attestation_method_version") == ProductMethodVersion &&
               HasTimestamp(root, "captured_at") &&
               configuration is { ValueKind: JsonValueKind.Object } value &&
               String(value, "product_surface") == "ChatGPT web" &&
               String(value, "documented_model_family") == "GPT-5.6 Sol" &&
               String(value, "selected_model") == "5.6 Sol" &&
               String(value, "reasoning_selection") == "Extra High" &&
               String(value, "conversation_type") == "Temporary Chat" &&
               String(value, "memory_state") == "not_used_or_created_by_temporary_chat" &&
               CustomInstructionsAreControlled(value) &&
               IsFalse(value, "project_context_enabled") &&
               IsFalse(value, "file_library_context_enabled") &&
               IsFalse(value, "prior_trial_attachments_present") &&
               IsSha256(value, "base_prompt_sha256") &&
               IsSha256(value, "tool_contract_sha256") &&
               !ContainsUnknown(value) &&
               CanonicalHashMatches(root, value, "observable_configuration_fingerprint_sha256") &&
               IsFalse(root, "private_deployment_identifier", "exposed") &&
               IsFalse(root, "private_deployment_identifier", "equality_claimed") &&
               At(root, "private_deployment_identifier", "value") is { ValueKind: JsonValueKind.Null };
    }

    public static bool PassesP5(JsonElement root, JsonElement p0)
    {
        var p0Configuration = At(p0, "observable_configuration");
        var p0Fingerprint = String(p0, "observable_configuration_fingerprint_sha256");
        return PassesP0(p0) &&
               String(root, "schema") == FreshInvocationSchema &&
               IsTrue(root, "passed") &&
               String(root, "attestation_method_version") == FreshInvocationMethodVersion &&
               String(root, "trial_invalidation_rules_version") == InvalidationRulesVersion &&
               HasTimestamp(root, "captured_at") &&
               String(root, "observable_configuration_fingerprint_sha256") == p0Fingerprint &&
               String(root, "selected_model") == "5.6 Sol" &&
               String(root, "reasoning_selection") == "Extra High" &&
               IsTrue(root, "new_conversation") &&
               IsTrue(root, "temporary_chat") &&
               Number(root, "prior_message_count") == 0 &&
               IsTrue(root, "no_prior_trial_transcript") &&
               IsTrue(root, "no_cross_arm_memory") &&
               IsTrue(root, "no_hidden_evaluator_state") &&
               IsTrue(root, "same_base_instructions") &&
               IsTrue(root, "same_tool_contract") &&
               IsFalse(root, "project_context_present") &&
               IsFalse(root, "file_library_context_present") &&
               IsFalse(root, "prior_trial_attachments_present") &&
               IsFalse(root, "observable_product_fallback") &&
               IsTrue(root, "machine_readable_response_parsed") &&
               IsSha256(root, "arm_package_sha256") &&
               IsSha256(root, "invocation_adapter_sha256") &&
               IsSha256(root, "trial_output_contract_sha256") &&
               IsSha256(root, "ui_evidence_sha256") &&
               p0Configuration is { ValueKind: JsonValueKind.Object } configuration &&
               String(root, "base_prompt_sha256") == String(configuration, "base_prompt_sha256") &&
               String(root, "tool_contract_sha256") == String(configuration, "tool_contract_sha256");
    }

    public static bool PassesP2Action(JsonElement root) =>
        IsTrue(root, "write_occurred") &&
        IsTrue(root, "provider_state_independently_verified") &&
        IsTrue(root, "stale_then_fresh_distinguished") &&
        IsTrue(root, "mutation_confined_to_fixture") &&
        String(root, "repository") == "StealthEyeLLC/world-kernel-build-001-fixture" &&
        IsSha1(root, "provider_commit_sha") &&
        IsSha256(root, "browser_receipt_sha256") &&
        IsSha256(root, "fresh_provider_observation_sha256");

    private static bool CustomInstructionsAreControlled(JsonElement configuration)
    {
        var state = String(configuration, "custom_instructions", "state");
        return state switch
        {
            "disabled" => At(configuration, "custom_instructions", "sha256") is { ValueKind: JsonValueKind.Null },
            "identically_frozen" => IsSha256(configuration, "custom_instructions", "sha256"),
            _ => false
        };
    }

    private static bool CanonicalHashMatches(JsonElement root, JsonElement value, string hashProperty) =>
        String(root, hashProperty) is { } expected &&
        expected == CanonicalJson.HashJson(value);

    private static bool ContainsUnknown(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => string.Equals(value.GetString(), "unknown", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(value.GetString(), "unavailable", StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Array => value.EnumerateArray().Any(ContainsUnknown),
        JsonValueKind.Object => value.EnumerateObject().Any(property => ContainsUnknown(property.Value)),
        _ => false
    };

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

    private static bool IsTrue(JsonElement root, params string[] path) =>
        At(root, path) is { ValueKind: JsonValueKind.True };

    private static bool IsFalse(JsonElement root, params string[] path) =>
        At(root, path) is { ValueKind: JsonValueKind.False };

    private static long Number(JsonElement root, params string[] path) =>
        At(root, path) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number)
            ? number
            : long.MinValue;

    private static bool IsSha256(JsonElement root, params string[] path) => IsLowerHex(String(root, path), 64);

    private static bool IsSha1(JsonElement root, params string[] path) => IsLowerHex(String(root, path), 40);

    private static bool IsLowerHex(string? value, int length) =>
        value is not null && value.Length == length && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool HasTimestamp(JsonElement root, params string[] path) =>
        DateTimeOffset.TryParse(String(root, path), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _);
}
