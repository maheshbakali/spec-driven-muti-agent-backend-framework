using Platform.Core.Models;

namespace Platform.ClaudeRuntime;

/// <summary>
/// Drives headless Claude Code (<c>claude -p</c>) as a subprocess.
/// Each call maps to one subagent invocation — e.g. code gen, Bicep, tests.
/// </summary>
public interface IClaudeCodeRunner
{
    Task<ClaudeCodeRunResult> RunAsync(
        ClaudeCodeRunRequest request,
        CancellationToken ct = default);
}

public sealed class ClaudeCodeRunRequest
{
    public required string Prompt { get; init; }
    public ArtifactType ArtifactType { get; init; }
    /// <summary>Working directory passed to the subprocess.</summary>
    public required string WorkingDirectory { get; init; }
    /// <summary>Extra environment variables to inject (e.g. ANTHROPIC_API_KEY).</summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }
        = new Dictionary<string, string>();
    /// <summary>Path to a <c>.claude/agents/</c> definition file to activate, if any.</summary>
    public string? AgentDefinitionPath { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}

public sealed class ClaudeCodeRunResult
{
    public bool Success { get; init; }
    public string? Output { get; init; }
    public string? ErrorOutput { get; init; }
    public int ExitCode { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ClaudeCodeJsonEvent> Events { get; init; } = [];
}

/// <summary>One parsed event from the <c>claude -p --output-format stream-json</c> stream.</summary>
public sealed class ClaudeCodeJsonEvent
{
    public required string Type { get; init; }
    public string? Content { get; init; }
    public IDictionary<string, object?> Extra { get; init; } = new Dictionary<string, object?>();
}
