using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class Campaign2Tests
{
    public static Task ObservableAttestationsAsync()
    {
        var hash = new string('a', 64);
        var configuration = JsonSerializer.SerializeToElement(new
        {
            product_surface = "ChatGPT web",
            documented_model_family = "GPT-5.6 Sol",
            selected_model = "5.6 Sol",
            reasoning_selection = "Extra High",
            conversation_type = "Temporary Chat",
            memory_state = "not_used_or_created_by_temporary_chat",
            custom_instructions = new { state = "disabled", sha256 = (string?)null },
            project_context_enabled = false,
            file_library_context_enabled = false,
            prior_trial_attachments_present = false,
            base_prompt_sha256 = hash,
            tool_contract_sha256 = new string('b', 64),
            application = "Google Chrome",
            application_version = "151.0.7922.109"
        });
        var fingerprint = CanonicalJson.HashJson(configuration);
        var p0 = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign2Attestation.ProductSchema,
            passed = true,
            attestation_method_version = Campaign2Attestation.ProductMethodVersion,
            captured_at = "2026-08-11T12:00:00Z",
            observable_configuration = configuration,
            observable_configuration_fingerprint_sha256 = fingerprint,
            private_deployment_identifier = new { exposed = false, value = (string?)null, equality_claimed = false }
        });
        AssertEx.True(Campaign2Attestation.PassesP0(p0), "Observable P0 fields and their canonical fingerprint should pass.");

        var p0WithUnknown = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign2Attestation.ProductSchema,
            passed = true,
            attestation_method_version = Campaign2Attestation.ProductMethodVersion,
            captured_at = "2026-08-11T12:00:00Z",
            observable_configuration = new { product_surface = "unknown" },
            observable_configuration_fingerprint_sha256 = CanonicalJson.HashJson(JsonSerializer.SerializeToElement(new { product_surface = "unknown" })),
            private_deployment_identifier = new { exposed = false, value = (string?)null, equality_claimed = false }
        });
        AssertEx.False(Campaign2Attestation.PassesP0(p0WithUnknown), "Hashing an unknown label must never satisfy P0.");

        var p5 = JsonSerializer.SerializeToElement(new
        {
            schema = Campaign2Attestation.FreshInvocationSchema,
            passed = true,
            attestation_method_version = Campaign2Attestation.FreshInvocationMethodVersion,
            trial_invalidation_rules_version = Campaign2Attestation.InvalidationRulesVersion,
            captured_at = "2026-08-11T12:01:00Z",
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
            trial_output_contract_sha256 = new string('e', 64),
            ui_evidence_sha256 = new string('f', 64),
            base_prompt_sha256 = hash,
            tool_contract_sha256 = new string('b', 64)
        });
        AssertEx.True(Campaign2Attestation.PassesP5(p5, p0), "A fresh Temporary Chat attestation matching P0 should pass.");
        AssertEx.False(Campaign2Attestation.PassesP5(
            JsonSerializer.SerializeToElement(new { schema = Campaign2Attestation.FreshInvocationSchema, passed = true }), p0),
            "An unsubstantiated fresh-context claim must fail.");

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
        AssertEx.True(Campaign2Attestation.PassesP2Action(p2));
        return Task.CompletedTask;
    }
}
