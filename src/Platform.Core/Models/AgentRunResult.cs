namespace Platform.Core.Models;

public enum AgentRunStatus { Success, Failure, PartialSuccess }

public sealed class AgentRunResult
{
    public required ArtifactType ArtifactType { get; init; }
    public required AgentRunStatus Status { get; init; }
    public string? Output { get; init; }
    public string? ErrorDetail { get; init; }
    public IReadOnlyList<string> GeneratedFilePaths { get; init; } = [];
    public TimeSpan Duration { get; init; }
}
