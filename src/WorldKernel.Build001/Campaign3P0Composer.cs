using System.Text;
using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public static class Campaign3P0Composer
{
    public const string PersonalizationObservationSchema = "world-kernel-build001-campaign3-p0-personalization-observation-v1";
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly string[] FrozenFieldNames = ["Custom instructions", "Nickname", "Occupation", "More about you"];

    public static byte[] Compose(
        string inspectResultPath,
        string personalizationObservationPath,
        string basePromptPath,
        string toolContractPath,
        string trialOutputContractPath)
    {
        using var inspectDocument = JsonDocument.Parse(File.ReadAllBytes(inspectResultPath));
        using var personalizationDocument = JsonDocument.Parse(File.ReadAllBytes(personalizationObservationPath));
        var inspect = inspectDocument.RootElement;
        var personalization = personalizationDocument.RootElement;

        RequireString(inspect, "schema", "world-kernel-build001-campaign3-subject-adapter-result-v1");
        RequireTrue(inspect, "passed");
        RequireString(inspect, "mode", "inspect");
        var ui = RequireObject(inspect, "ui_evidence");
        var before = RequireObject(ui, "before");
        var model = RequireObject(ui, "model_before");
        RequireTrue(before, "signed_in");
        RequireFalse(before, "login_control_present");
        RequireTrue(before, "temporary_chat");
        RequireTrue(before, "chat_surface_selected");
        RequireNumber(before, "message_marker_count", 0);
        RequireNumber(before, "attachment_marker_count", 0);
        RequireFalse(before, "project_context_present");
        RequireFalse(before, "file_library_context_present");
        RequireString(model, "selected_model", "5.6 Sol");
        RequireString(model, "reasoning_selection", "Extra High");

        RequireString(personalization, "schema", PersonalizationObservationSchema);
        RequireTrue(personalization, "passed");
        RequireString(personalization, "workspace_type", "Business");
        RequireString(personalization, "base_style_and_tone", "Default");
        RequireFalse(personalization, "fast_answers_enabled");
        RequireTrue(personalization, "account_memory_enabled");
        RequireTrue(personalization, "record_history_reference_enabled");
        var styles = RequireObject(personalization, "style_characteristics");
        foreach (var key in new[] { "warm", "enthusiastic", "headers_and_lists", "emoji" })
            RequireString(styles, key, "Default");

        var fields = RequireArray(personalization, "custom_instruction_fields").EnumerateArray().ToArray();
        if (fields.Length != FrozenFieldNames.Length)
            throw new InvalidDataException($"Campaign 3 P0 requires exactly four personalization text fields; observed {fields.Length}.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var name = RequiredString(field, "name");
            names.Add(name);
            RequireNumber(field, "value_length", 0);
            RequireString(field, "value_sha256", EmptySha256);
        }
        if (!FrozenFieldNames.All(names.Contains) || names.Count != FrozenFieldNames.Length)
            throw new InvalidDataException("Campaign 3 P0 personalization field identity differs from the frozen four-field set.");

        var configuration = JsonSerializer.SerializeToElement(new
        {
            product_surface = "ChatGPT web",
            documented_model_family = "GPT-5.6 Sol",
            selected_model = "5.6 Sol",
            reasoning_selection = "Extra High",
            conversation_type = "Temporary Chat",
            subject_mode = "Chat",
            workspace_type = "Business",
            memory_state = "not_used_or_created_by_temporary_chat",
            account_memory_enabled = true,
            record_history_reference_enabled = true,
            custom_instructions = new
            {
                state = "identically_frozen",
                sha256 = EmptySha256,
                field_count = 4
            },
            base_style_and_tone = "Default",
            style_characteristics = new
            {
                warm = "Default",
                enthusiastic = "Default",
                headers_and_lists = "Default",
                emoji = "Default"
            },
            fast_answers_enabled = false,
            project_context_enabled = false,
            file_library_context_enabled = false,
            prior_trial_attachments_present = false,
            base_prompt_sha256 = NormalizedTextSha256(basePromptPath),
            tool_contract_sha256 = NormalizedTextSha256(toolContractPath),
            trial_output_contract_sha256 = NormalizedTextSha256(trialOutputContractPath),
            application = RequiredString(ui, "application"),
            application_version = RequiredString(ui, "application_version")
        }, JsonDefaults.Options);

        var p0 = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign3Attestation.ProductSchema,
            passed = true,
            attestation_method_version = Campaign3Attestation.ProductMethodVersion,
            captured_at = DateTimeOffset.UtcNow,
            observable_configuration = configuration,
            observable_configuration_fingerprint_sha256 = CanonicalJson.HashJson(configuration),
            source_evidence = new
            {
                temporary_chat_inspection_sha256 = CanonicalJson.Sha256(File.ReadAllBytes(inspectResultPath)),
                personalization_observation_sha256 = CanonicalJson.Sha256(File.ReadAllBytes(personalizationObservationPath))
            },
            private_deployment_identifier = new { exposed = false, value = (string?)null, equality_claimed = false }
        }, JsonDefaults.Options);

        if (!Campaign3Attestation.PassesP0(p0))
            throw new InvalidDataException("Composed Campaign 3 P0 attestation does not satisfy the prospective hard gate.");
        return CanonicalJson.Canonicalize(p0);
    }

    private static string NormalizedTextSha256(string path) =>
        CanonicalJson.Sha256Utf8(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal));

    private static JsonElement RequireObject(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' must be an object.");
        return value;
    }

    private static JsonElement RequireArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' must be an array.");
        return value;
    }

    private static string RequiredString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' is absent or empty.");
        return value.GetString()!;
    }

    private static void RequireString(JsonElement root, string property, string expected)
    {
        var actual = RequiredString(root, property);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' expected '{expected}', observed '{actual}'.");
    }

    private static void RequireTrue(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.True)
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' must be true.");
    }

    private static void RequireFalse(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.False)
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' must be false.");
    }

    private static void RequireNumber(JsonElement root, string property, long expected)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var actual) || actual != expected)
            throw new InvalidDataException($"Campaign 3 P0 field '{property}' must equal {expected}.");
    }
}