using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Platform.ClaudeRuntime;

/// <summary>
/// Shells out to <c>claude -p</c> with <c>--output-format stream-json</c>,
/// streams newline-delimited JSON events, and collects them into a typed result.
/// The subprocess is NOT actually invoked yet — the command is logged so you can
/// inspect and validate before wiring up real execution.
/// </summary>
public sealed class ClaudeCodeProcessRunner : IClaudeCodeRunner
{
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger<ClaudeCodeProcessRunner> _logger;

    public ClaudeCodeProcessRunner(ClaudeCodeOptions options, ILogger<ClaudeCodeProcessRunner> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ClaudeCodeRunResult> RunAsync(
        ClaudeCodeRunRequest request,
        CancellationToken ct = default)
    {
        var command = BuildCommand(request);

        _logger.LogInformation(
            "[DRY RUN] Would invoke Claude Code for {ArtifactType}:\n  cwd: {Cwd}\n  cmd: {Cmd}",
            request.ArtifactType, request.WorkingDirectory, command);

        // TODO: Remove the dry-run gate below and call ExecuteAsync when ready.
        if (_options.DryRun)
        {
            return new ClaudeCodeRunResult
            {
                Success = true,
                Output = $"[DRY RUN] claude -p would be invoked with: {command}",
                ExitCode = 0,
                Duration = TimeSpan.Zero,
            };
        }

        return await ExecuteAsync(request, command, ct);
    }

    private string BuildCommand(ClaudeCodeRunRequest request)
    {
        var args = new StringBuilder();
        args.Append("-p ");
        args.Append(QuoteArg(request.Prompt));
        args.Append(" --output-format stream-json");
        args.Append(" --no-interactive");

        if (request.AgentDefinitionPath is { } agentPath)
            args.Append($" --agent {QuoteArg(agentPath)}");

        return $"{_options.ClaudeExecutablePath} {args}";
    }

    private async Task<ClaudeCodeRunResult> ExecuteAsync(
        ClaudeCodeRunRequest request,
        string command,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var events = new List<ClaudeCodeJsonEvent>();
        var errorOutput = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = _options.ClaudeExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Build args
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(request.Prompt);
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--no-interactive");

        if (request.AgentDefinitionPath is { } agentPath)
        {
            psi.ArgumentList.Add("--agent");
            psi.ArgumentList.Add(agentPath);
        }

        foreach (var (key, value) in request.EnvironmentVariables)
            psi.Environment[key] = value;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(request.Timeout);

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Stream stdout as NDJSON
        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(cts.Token) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var evt = ParseJsonEvent(line);
                if (evt is not null) events.Add(evt);
            }
        }, cts.Token);

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(cts.Token) is { } line)
                errorOutput.AppendLine(line);
        }, cts.Token);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cts.Token);
        sw.Stop();

        _logger.LogInformation(
            "Claude Code exited with code {Code} after {Elapsed}ms for {ArtifactType}",
            process.ExitCode, sw.ElapsedMilliseconds, request.ArtifactType);

        var finalContent = events
            .Where(e => e.Type == "assistant")
            .Select(e => e.Content)
            .LastOrDefault();

        return new ClaudeCodeRunResult
        {
            Success = process.ExitCode == 0,
            Output = finalContent,
            ErrorOutput = errorOutput.Length > 0 ? errorOutput.ToString() : null,
            ExitCode = process.ExitCode,
            Duration = sw.Elapsed,
            Events = events,
        };
    }

    private ClaudeCodeJsonEvent? ParseJsonEvent(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "unknown" : "unknown";
            var content = root.TryGetProperty("content", out var c) ? c.GetString() : null;
            return new ClaudeCodeJsonEvent { Type = type, Content = content };
        }
        catch
        {
            _logger.LogDebug("Could not parse Claude Code JSON line: {Line}", line);
            return null;
        }
    }

    private static string QuoteArg(string arg) =>
        arg.Contains(' ') ? $"\"{arg.Replace("\"", "\\\"")}\"" : arg;
}
