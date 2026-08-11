using System.Text.Json;
using StealthEye.WorldKernel.Build001;

namespace StealthEye.WorldKernel.Build001.Tests;

internal static class Campaign2ExecutionTests
{
    public static Task OutcomeVectorsAsync()
    {
        var emptyProvider = Provider();

        var commitBefore = State(localHead: Sha('a'), remoteHead: Sha('a'), worktree: Sha('1'), clean: false);
        var commitAfter = State(localHead: Sha('b'), remoteHead: Sha('a'), worktree: Sha('2'), clean: true);
        AssertVector("git:create_local_commit", commitBefore, commitAfter, true, emptyProvider,
            ("provider_accepts_action", true),
            ("local_head_changes", true),
            ("remote_target_ref_changes_before_push", false),
            ("local_worktree_clean_after", true),
            ("new_commit_reachable_remotely_before_push", false));

        var branchBefore = State(localHead: Sha('a'), remoteHead: null, currentBranch: "wk-b001-old");
        var branchAfter = State(localHead: Sha('a'), remoteHead: null, currentBranch: "wk-b001-new",
            localBranches: ["wk-b001-old", "wk-b001-new"]);
        AssertVector("git:create_branch", branchBefore, branchAfter, true, emptyProvider,
            ("provider_accepts_action", true),
            ("new_local_branch_exists", true),
            ("current_branch_is_new_branch", true),
            ("local_head_sha_changes", false),
            ("remote_branch_exists_before_push", false));

        var pushBefore = State(localHead: Sha('b'), remoteHead: Sha('a'));
        var pushAfter = State(localHead: Sha('b'), remoteHead: Sha('b'));
        var pushProvider = Provider(checkStarted: true, checkSuccess: true, presentedHead: Sha('b'));
        AssertVector("git:push_ref", pushBefore, pushAfter, true, pushProvider,
            ("provider_accepts_push", true),
            ("remote_ref_equals_local_head_at_H1", true),
            ("local_head_changes_because_of_push", false),
            ("remote_check_starts_by_H2", true),
            ("remote_check_terminal_success_by_H3", true),
            ("browser_presentation_reflects_new_remote_head_by_H1", true));

        var remoteBefore = State(localHead: Sha('a'), remoteHead: Sha('a'), trackingHead: Sha('a'));
        var remoteAfter = State(localHead: Sha('a'), remoteHead: Sha('c'), trackingHead: Sha('a'),
            remoteReachable: false);
        var remoteProvider = Provider(presentedHead: Sha('c'));
        AssertVector("github:create_remote_commit", remoteBefore, remoteAfter, true, remoteProvider,
            ("provider_accepts_action", true),
            ("remote_head_changes", true),
            ("local_head_changes_before_fetch", false),
            ("local_remote_tracking_ref_changes_before_fetch", false),
            ("new_remote_commit_reachable_locally_before_fetch", false),
            ("browser_presentation_reflects_new_remote_commit_by_H1", true));

        var fetchBefore = State(localHead: Sha('a'), remoteHead: Sha('c'), trackingHead: Sha('a'), remoteReachable: false);
        var fetchAfter = State(localHead: Sha('a'), remoteHead: Sha('c'), trackingHead: Sha('c'), remoteReachable: true);
        AssertVector("git:fetch_remote", fetchBefore, fetchAfter, true, emptyProvider,
            ("provider_accepts_action", true),
            ("local_head_changes", false),
            ("remote_tracking_ref_equals_remote_head_at_H1", true),
            ("remote_head_changes_because_of_fetch", false),
            ("remote_commit_reachable_locally_after_fetch", true),
            ("checked_out_branch_content_changes", false));

        var integrateBefore = State(localHead: Sha('a'), remoteHead: Sha('c'), trackingHead: Sha('c'), worktree: Sha('1'));
        var integrateAfter = State(localHead: Sha('c'), remoteHead: Sha('c'), trackingHead: Sha('c'), worktree: Sha('3'));
        AssertVector("git:integrate_fast_forward", integrateBefore, integrateAfter, true, emptyProvider,
            ("fast_forward_is_accepted", true),
            ("local_head_equals_remote_target_after_H1", true),
            ("local_head_changes", true),
            ("local_worktree_content_changes", true),
            ("remote_head_changes_because_of_integration", false),
            ("merge_commit_created", false));

        var rejectedAfter = State(localHead: Sha('b'), remoteHead: Sha('c'), trackingHead: Sha('c'), worktree: Sha('2'));
        AssertVector("git:integrate_fast_forward", rejectedAfter, rejectedAfter, false, emptyProvider,
            ("fast_forward_is_accepted", false),
            ("local_head_equals_remote_target_after_H1", false),
            ("local_head_changes", false),
            ("merge_commit_created", false));
        return Task.CompletedTask;
    }

    private static void AssertVector(
        string action,
        Campaign2StateObservation before,
        Campaign2StateObservation after,
        bool accepted,
        Campaign2ProviderOutcome provider,
        params (string Key, bool Expected)[] assertions)
    {
        var result = Campaign2OutcomeResolver.Resolve(action, before, after, accepted, provider);
        AssertEx.Equal(Build001Contract.ForAction(action).Count, result.ActualPropositions.Count);
        AssertEx.True(result.ActualPropositions.Values.All(value => value is not null));
        foreach (var assertion in assertions)
        {
            AssertEx.Equal(assertion.Expected, result.ActualPropositions[assertion.Key]!.Value, $"{action}:{assertion.Key}");
        }
    }

    private static Campaign2StateObservation State(
        string? localHead = null,
        string? remoteHead = null,
        string? trackingHead = null,
        string? worktree = null,
        bool clean = true,
        string currentBranch = "wk-b001-fixture",
        IReadOnlyList<string>? localBranches = null,
        bool remoteReachable = true,
        int parents = 1) => new(
        Campaign2OutcomeResolver.StateSchema,
        DateTimeOffset.UtcNow,
        "wk-b001-fixture",
        localHead ?? Sha('a'),
        currentBranch,
        Sha('d'),
        worktree ?? Sha('1'),
        clean,
        localBranches ?? [currentBranch],
        remoteHead,
        trackingHead,
        remoteReachable,
        parents,
        "https://github.com/StealthEyeLLC/world-kernel-build-001-fixture.git",
        "test",
        JsonDefaults.EmptyArray);

    private static Campaign2ProviderOutcome Provider(
        bool checkStarted = false,
        bool checkSuccess = false,
        string? presentedHead = null) => new(
        Campaign2OutcomeResolver.ProviderSchema,
        DateTimeOffset.UtcNow,
        new Campaign2CheckOutcome(true, checkStarted, checkSuccess, checkSuccess ? "success" : null, JsonDefaults.EmptyArray),
        new Campaign2BrowserOutcome(presentedHead is not null, presentedHead, null, JsonDefaults.EmptyObject));

    private static string Sha(char value) => new(value, 40);
}
