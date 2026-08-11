using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StealthEye.WorldKernel.Build001;

public sealed class NativeGitFacet
{
    private static readonly Regex BranchPattern = new("^wk-b001-[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant);
    private readonly string _gitExecutable;
    private readonly string _fixtureRoot;

    public NativeGitFacet(string gitExecutable, string fixtureRoot)
    {
        _gitExecutable = Path.GetFullPath(gitExecutable);
        _fixtureRoot = Path.GetFullPath(fixtureRoot);
        if (!File.Exists(_gitExecutable))
        {
            throw new FileNotFoundException("Stock git executable not found.", _gitExecutable);
        }
    }

    public async Task<ProviderOperationResult> CreateLocalCommitAsync(
        string workingCopy,
        string fixtureRelativePath,
        string message,
        DateTimeOffset deterministicTimestamp,
        CancellationToken cancellationToken = default)
    {
        var root = GuardWorkingCopy(workingCopy);
        var file = Path.GetFullPath(Path.Combine(root, fixtureRelativePath));
        EnsureUnder(root, file);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("Evaluator-prepared tracked fixture file does not exist.", file);
        }

        var started = DateTimeOffset.UtcNow;
        var commands = new List<GitCommandResult>
        {
            await RunAsync(root, ["add", "--", fixtureRelativePath], null, cancellationToken).ConfigureAwait(false)
        };
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_AUTHOR_NAME"] = "StealthEye Build 001",
            ["GIT_AUTHOR_EMAIL"] = "build001@invalid.local",
            ["GIT_COMMITTER_NAME"] = "StealthEye Build 001",
            ["GIT_COMMITTER_EMAIL"] = "build001@invalid.local",
            ["GIT_AUTHOR_DATE"] = deterministicTimestamp.ToUniversalTime().ToString("O"),
            ["GIT_COMMITTER_DATE"] = deterministicTimestamp.ToUniversalTime().ToString("O")
        };
        commands.Add(await RunAsync(root, ["commit", "--no-gpg-sign", "-m", message], environment, cancellationToken).ConfigureAwait(false));
        commands.Add(await RunAsync(root, ["rev-parse", "HEAD"], null, cancellationToken).ConfigureAwait(false));
        return BuildResult("git:create_local_commit", started, commands);
    }

    public async Task<ProviderOperationResult> CreateBranchAsync(
        string workingCopy,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        if (!BranchPattern.IsMatch(branchName))
        {
            throw new ArgumentException("Branch name is outside the disposable Build 001 namespace.", nameof(branchName));
        }
        var root = GuardWorkingCopy(workingCopy);
        var started = DateTimeOffset.UtcNow;
        var commands = new List<GitCommandResult>
        {
            await RunAsync(root, ["switch", "-c", branchName], null, cancellationToken).ConfigureAwait(false),
            await RunAsync(root, ["branch", "--show-current"], null, cancellationToken).ConfigureAwait(false)
        };
        return BuildResult("git:create_branch", started, commands);
    }

    public async Task<ProviderOperationResult> PushRefAsync(
        string workingCopy,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        if (!BranchPattern.IsMatch(branchName))
        {
            throw new ArgumentException("Remote branch is outside the disposable Build 001 namespace.", nameof(branchName));
        }
        var root = GuardWorkingCopy(workingCopy);
        var started = DateTimeOffset.UtcNow;
        var commands = new List<GitCommandResult>
        {
            await RunAsync(root, ["push", "--porcelain", "origin", $"HEAD:refs/heads/{branchName}"], null, cancellationToken)
                .ConfigureAwait(false)
        };
        return BuildResult("git:push_ref", started, commands);
    }

    public async Task<ProviderOperationResult> FetchRemoteAsync(
        string workingCopy,
        CancellationToken cancellationToken = default)
    {
        var root = GuardWorkingCopy(workingCopy);
        var started = DateTimeOffset.UtcNow;
        var commands = new List<GitCommandResult>
        {
            await RunAsync(root, ["fetch", "--prune", "origin"], null, cancellationToken).ConfigureAwait(false)
        };
        return BuildResult("git:fetch_remote", started, commands);
    }

    public async Task<ProviderOperationResult> IntegrateFastForwardAsync(
        string workingCopy,
        string remoteBranchName,
        CancellationToken cancellationToken = default)
    {
        if (!BranchPattern.IsMatch(remoteBranchName))
        {
            throw new ArgumentException("Remote branch is outside the disposable Build 001 namespace.", nameof(remoteBranchName));
        }
        var root = GuardWorkingCopy(workingCopy);
        var started = DateTimeOffset.UtcNow;
        var commands = new List<GitCommandResult>
        {
            await RunAsync(root, ["merge", "--ff-only", $"refs/remotes/origin/{remoteBranchName}"], null, cancellationToken)
                .ConfigureAwait(false)
        };
        return BuildResult("git:integrate_fast_forward", started, commands);
    }

    public async Task<JsonElement> ObserveAsync(string workingCopy, CancellationToken cancellationToken = default)
    {
        var root = GuardWorkingCopy(workingCopy);
        var commands = new[]
        {
            await RunAsync(root, ["rev-parse", "HEAD"], null, cancellationToken).ConfigureAwait(false),
            await RunAsync(root, ["branch", "--show-current"], null, cancellationToken).ConfigureAwait(false),
            await RunAsync(root, ["status", "--porcelain=v2", "--branch"], null, cancellationToken).ConfigureAwait(false),
            await RunAsync(root, ["remote", "get-url", "origin"], null, cancellationToken).ConfigureAwait(false),
            await RunAsync(root, ["for-each-ref", "--format=%(refname)%00%(objectname)", "refs/remotes/origin"], null, cancellationToken)
                .ConfigureAwait(false)
        };
        return JsonSerializer.SerializeToElement(new
        {
            provider = "stock-git-experiment-facet",
            observed_at = DateTimeOffset.UtcNow,
            working_copy = root,
            commands
        }, JsonDefaults.Options);
    }

    private string GuardWorkingCopy(string workingCopy)
    {
        var root = Path.GetFullPath(workingCopy);
        EnsureUnder(_fixtureRoot, root);
        if (!Directory.Exists(root) || (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git"))))
        {
            throw new InvalidOperationException("Target is not an isolated Build 001 Git working copy.");
        }
        return root;
    }

    private static void EnsureUnder(string parent, string child)
    {
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(child);
        if (!normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedChild.TrimEnd(Path.DirectorySeparatorChar), parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Git operation target escapes the disposable fixture root.");
        }
    }

    private async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument is "--force" or "--force-with-lease" or "-f"))
        {
            throw new InvalidOperationException("Force operations are forbidden in Build 001.");
        }

        var start = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "Never";
        if (environment is not null)
        {
            foreach (var value in environment)
            {
                start.Environment[value.Key] = value.Value;
            }
        }

        var beganAt = DateTimeOffset.UtcNow;
        using var process = new Process { StartInfo = start };
        if (!process.Start())
        {
            throw new InvalidOperationException("git.exe failed to start.");
        }
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new GitCommandResult(
            arguments.ToArray(),
            process.ExitCode,
            stdout,
            stderr,
            beganAt,
            DateTimeOffset.UtcNow);
    }

    private static ProviderOperationResult BuildResult(
        string semanticAction,
        DateTimeOffset started,
        IReadOnlyList<GitCommandResult> commands)
    {
        var completed = DateTimeOffset.UtcNow;
        var accepted = commands.All(command => command.ExitCode == 0);
        var typed = JsonSerializer.SerializeToElement(new
        {
            semantic_action = semanticAction,
            provider = "stock-git-experiment-facet",
            accepted,
            started_at = started,
            completed_at = completed,
            commands
        }, JsonDefaults.Options);
        return new ProviderOperationResult(
            semanticAction,
            accepted,
            commands.LastOrDefault()?.ExitCode ?? -1,
            started,
            completed,
            CanonicalJson.Canonicalize(typed),
            typed);
    }
}

public sealed record GitCommandResult(
    IReadOnlyList<string> Arguments,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

