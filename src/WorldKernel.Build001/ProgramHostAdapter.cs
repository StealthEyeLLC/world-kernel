using System.Diagnostics;
using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public sealed class ProgramHostAdapter
{
    private readonly string _nodeExecutable;
    private readonly string _scriptsRoot;

    public ProgramHostAdapter(string nodeExecutable, string scriptsRoot)
    {
        _nodeExecutable = Path.GetFullPath(nodeExecutable);
        _scriptsRoot = Path.GetFullPath(scriptsRoot);
        if (!File.Exists(_nodeExecutable)) throw new FileNotFoundException("Portable Node executable not found.", _nodeExecutable);
    }

    public Task<ProgramHostResult> ObserveCodeEyeAsync(
        string sdkPath,
        string solutionPath,
        string pipe = "codeeye-dev",
        CancellationToken cancellationToken = default) => RunAsync(
            "codeeye-observe.mjs",
            [sdkPath, solutionPath, pipe],
            cancellationToken);

    public Task<ProgramHostResult> PreflightEyeBrowseGitHubAsync(
        string sdkPath,
        string repositoryUrl,
        CancellationToken cancellationToken = default) => RunAsync(
            "eyebrowse-github-preflight.mjs",
            [sdkPath, repositoryUrl],
            cancellationToken,
            acceptedExitCodes: [0, 3]);

    public Task<ProgramHostResult> CreateRemoteCommitAsync(
        string sdkPath,
        string branchName,
        string filePath,
        string replacementText,
        string commitMessage,
        CancellationToken cancellationToken = default) => RunAsync(
            "eyebrowse-github-remote-commit.mjs",
            [sdkPath, branchName, filePath, replacementText, commitMessage],
            cancellationToken);

    private async Task<ProgramHostResult> RunAsync(
        string scriptName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyCollection<int>? acceptedExitCodes = null)
    {
        var script = Path.Combine(_scriptsRoot, scriptName);
        if (!File.Exists(script)) throw new FileNotFoundException("Program Host adapter script not found.", script);
        var start = new ProcessStartInfo
        {
            FileName = _nodeExecutable,
            WorkingDirectory = _scriptsRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(script);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var startedAt = DateTimeOffset.UtcNow;
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Program Host process failed to start.");
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var accepted = acceptedExitCodes ?? [0];
        if (!accepted.Contains(process.ExitCode))
        {
            throw new InvalidOperationException($"Program Host {scriptName} failed with exit {process.ExitCode}: {stderr}");
        }
        JsonElement? payload = null;
        var lastLine = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (lastLine is not null)
        {
            using var document = JsonDocument.Parse(lastLine);
            payload = document.RootElement.Clone();
        }
        return new ProgramHostResult(
            scriptName,
            process.ExitCode,
            startedAt,
            DateTimeOffset.UtcNow,
            stdout,
            stderr,
            payload);
    }
}

public sealed record ProgramHostResult(
    string ScriptName,
    int ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string StandardOutput,
    string StandardError,
    JsonElement? Payload)
{
    public byte[] EvidenceBytes => CanonicalJson.Serialize(new
    {
        script_name = ScriptName,
        exit_code = ExitCode,
        started_at = StartedAt,
        completed_at = CompletedAt,
        standard_output = StandardOutput,
        standard_error = StandardError,
        payload = Payload
    });
}
