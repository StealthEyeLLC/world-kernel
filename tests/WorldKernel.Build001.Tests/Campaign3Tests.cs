using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class Campaign3Tests
{
    public static Task ObservableAttestationsAsync()
    {
        var basePromptHash = new string('a', 64);
        var toolHash = new string('b', 64);
        var trialOutputHash = new string('e', 64);

        JsonElement Configuration(
            string workspaceType = "Business",
            bool accountMemoryEnabled = true,
            bool recordHistoryEnabled = true,
            int personalizationFieldCount = 4,
            string baseStyle = "Default",
            bool fastAnswersEnabled = false)
        {
            return JsonSerializer.SerializeToElement(new
            {
                product_surface = "ChatGPT web",
                documented_model_family = "GPT-5.6 Sol",
                selected_model = "5.6 Sol",
                reasoning_selection = "Extra High",
                conversation_type = "Temporary Chat",
                subject_mode = "Chat",
                workspace_type = workspaceType,
                memory_state = "not_used_or_created_by_temporary_chat",
                account_memory_enabled = accountMemoryEnabled,
                record_history_reference_enabled = recordHistoryEnabled,
                custom_instructions = new
                {
                    state = "identically_frozen",
                    sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    field_count = personalizationFieldCount
                },
                base_style_and_tone = baseStyle,
                style_characteristics = new
                {
                    warm = "Default",
                    enthusiastic = "Default",
                    headers_and_lists = "Default",
                    emoji = "Default"
                },
                fast_answers_enabled = fastAnswersEnabled,
                project_context_enabled = false,
                file_library_context_enabled = false,
                prior_trial_attachments_present = false,
                base_prompt_sha256 = basePromptHash,
                tool_contract_sha256 = toolHash,
                trial_output_contract_sha256 = trialOutputHash,
                application = "Google Chrome",
                application_version = "151.0.7922.109"
            });
        }

        JsonElement P0(JsonElement configuration)
        {
            return JsonSerializer.SerializeToElement(new
            {
                schema = Campaign3Attestation.ProductSchema,
                passed = true,
                attestation_method_version = Campaign3Attestation.ProductMethodVersion,
                captured_at = "2026-08-12T08:00:00Z",
                observable_configuration = configuration,
                observable_configuration_fingerprint_sha256 = CanonicalJson.HashJson(configuration),
                private_deployment_identifier = new { exposed = false, value = (string?)null, equality_claimed = false }
            });
        }

        var configuration = Configuration();
        var fingerprint = CanonicalJson.HashJson(configuration);
        var p0 = P0(configuration);
        AssertEx.True(Campaign3Attestation.PassesP0(p0), "The exact Campaign 3 observable subject configuration should pass.");

        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(workspaceType: "Personal"))), "A non-Business workspace must fail P0.");
        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(accountMemoryEnabled: false))), "Memory off must fail P0.");
        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(recordHistoryEnabled: false))), "Record History off must fail P0.");
        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(personalizationFieldCount: 3))), "Anything other than four frozen empty personalization fields must fail P0.");
        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(baseStyle: "Friendly"))), "A non-Default base style must fail P0.");
        AssertEx.False(Campaign3Attestation.PassesP0(P0(Configuration(fastAnswersEnabled: true))), "Fast Answers enabled must fail P0.");

        var p0WithUnknown = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign3Attestation.ProductSchema,
            passed = true,
            attestation_method_version = Campaign3Attestation.ProductMethodVersion,
            captured_at = "2026-08-12T08:00:00Z",
            observable_configuration = new { product_surface = "unknown" },
            observable_configuration_fingerprint_sha256 = CanonicalJson.HashJson(JsonSerializer.SerializeToElement(new { product_surface = "unknown" })),
            private_deployment_identifier = new { exposed = false, value = (string?)null, equality_claimed = false }
        });
        AssertEx.False(Campaign3Attestation.PassesP0(p0WithUnknown), "Hashing an unknown label must never satisfy P0.");

        var p5 = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign3Attestation.FreshInvocationSchema,
            passed = true,
            attestation_method_version = Campaign3Attestation.FreshInvocationMethodVersion,
            trial_invalidation_rules_version = Campaign3Attestation.InvalidationRulesVersion,
            captured_at = "2026-08-12T08:01:00Z",
            observable_configuration_fingerprint_sha256 = fingerprint,
            selected_model = "5.6 Sol",
            reasoning_selection = "Extra High",
            new_conversation = true,
            temporary_chat = true,
            prior_message_count = 0,
            no_prior_trial_transcript = true,
            no_cross_arm_memory = true,
            no_hidden_evaluator_state = true,
            same_base_instructions = true,
            same_tool_contract = true,
            project_context_present = false,
            file_library_context_present = false,
            prior_trial_attachments_present = false,
            observable_product_fallback = false,
            machine_readable_response_parsed = true,
            arm_package_sha256 = new string('c', 64),
            invocation_adapter_sha256 = new string('d', 64),
            trial_output_contract_sha256 = trialOutputHash,
            ui_evidence_sha256 = new string('f', 64),
            base_prompt_sha256 = basePromptHash,
            tool_contract_sha256 = toolHash
        });
        AssertEx.True(Campaign3Attestation.PassesP5(p5, p0), "A fresh Temporary Chat attestation matching the exact P0 fingerprint should pass.");

        var wrongOutputContractP5 = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign3Attestation.FreshInvocationSchema,
            passed = true,
            attestation_method_version = Campaign3Attestation.FreshInvocationMethodVersion,
            trial_invalidation_rules_version = Campaign3Attestation.InvalidationRulesVersion,
            captured_at = "2026-08-12T08:01:00Z",
            observable_configuration_fingerprint_sha256 = fingerprint,
            selected_model = "5.6 Sol",
            reasoning_selection = "Extra High",
            new_conversation = true,
            temporary_chat = true,
            prior_message_count = 0,
            no_prior_trial_transcript = true,
            no_cross_arm_memory = true,
            no_hidden_evaluator_state = true,
            same_base_instructions = true,
            same_tool_contract = true,
            project_context_present = false,
            file_library_context_present = false,
            prior_trial_attachments_present = false,
            observable_product_fallback = false,
            machine_readable_response_parsed = true,
            arm_package_sha256 = new string('c', 64),
            invocation_adapter_sha256 = new string('d', 64),
            trial_output_contract_sha256 = new string('9', 64),
            ui_evidence_sha256 = new string('f', 64),
            base_prompt_sha256 = basePromptHash,
            tool_contract_sha256 = toolHash
        });
        AssertEx.False(Campaign3Attestation.PassesP5(wrongOutputContractP5, p0), "A trial output contract mismatch against P0 must fail P5.");

        var p2 = JsonSerializer.SerializeToElement(new
        {
            write_occurred = true,
            provider_state_independently_verified = true,
            stale_then_fresh_distinguished = true,
            mutation_confined_to_fixture = true,
            repository = "StealthEyeLLC/world-kernel-build-001-fixture",
            provider_commit_sha = new string('1', 40),
            browser_receipt_sha256 = new string('2', 64),
            fresh_provider_observation_sha256 = new string('3', 64)
        });
        AssertEx.True(Campaign3Attestation.PassesP2Action(p2));
        return Task.CompletedTask;
    }
}