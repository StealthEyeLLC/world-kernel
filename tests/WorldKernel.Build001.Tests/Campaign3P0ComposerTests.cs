using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class Campaign3P0ComposerTests
{
    public static Task ExactContractAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "wk-c3-p0-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var inspectPath = Path.Combine(root, "inspect.json");
            var personalizationPath = Path.Combine(root, "personalization.json");
            var basePromptPath = Path.Combine(root, "base.txt");
            var toolPath = Path.Combine(root, "tool.json");
            var outputPath = Path.Combine(root, "output.json");
            File.WriteAllText(basePromptPath, "base\n");
            File.WriteAllText(toolPath, "{}\n");
            File.WriteAllText(outputPath, "{}\n");

            File.WriteAllText(inspectPath, JsonSerializer.Serialize(new
            {
                schema = "world-kernel-build001-campaign3-subject-adapter-result-v1",
                passed = true,
                mode = "inspect",
                ui_evidence = new
                {
                    application = "Google Chrome",
                    application_version = "fixture",
                    before = new
                    {
                        signed_in = true,
                        login_control_present = false,
                        temporary_chat = true,
                        chat_surface_selected = true,
                        message_marker_count = 0,
                        attachment_marker_count = 0,
                        project_context_present = false,
                        file_library_context_present = false
                    },
                    model_before = new { selected_model = "5.6 Sol", reasoning_selection = "Extra High" }
                }
            }));

            var fields = new[] { "Custom instructions", "Nickname", "Occupation", "More about you" }
                .Select(name => new { name, value_length = 0, value_sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" })
                .ToArray();
            File.WriteAllText(personalizationPath, JsonSerializer.Serialize(new
            {
                schema = Campaign3P0Composer.PersonalizationObservationSchema,
                passed = true,
                workspace_type = "Business",
                base_style_and_tone = "Default",
                fast_answers_enabled = false,
                account_memory_enabled = true,
                record_history_reference_enabled = true,
                style_characteristics = new { warm = "Default", enthusiastic = "Default", headers_and_lists = "Default", emoji = "Default" },
                custom_instruction_fields = fields
            }));

            using var composed = JsonDocument.Parse(Campaign3P0Composer.Compose(inspectPath, personalizationPath, basePromptPath, toolPath, outputPath));
            AssertEx.True(Campaign3Attestation.PassesP0(composed.RootElement), "Exact composed Campaign 3 P0 contract must pass.");

            File.WriteAllText(personalizationPath, JsonSerializer.Serialize(new
            {
                schema = Campaign3P0Composer.PersonalizationObservationSchema,
                passed = true,
                workspace_type = "Business",
                base_style_and_tone = "Default",
                fast_answers_enabled = false,
                account_memory_enabled = true,
                record_history_reference_enabled = true,
                style_characteristics = new { warm = "Default", enthusiastic = "Default", headers_and_lists = "Default", emoji = "Default" },
                custom_instruction_fields = fields.Take(3).ToArray()
            }));
            var rejected = false;
            try { Campaign3P0Composer.Compose(inspectPath, personalizationPath, basePromptPath, toolPath, outputPath); }
            catch (InvalidDataException) { rejected = true; }
            AssertEx.True(rejected, "Campaign 3 P0 composer must reject an altered personalization field set.");
            return Task.CompletedTask;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}