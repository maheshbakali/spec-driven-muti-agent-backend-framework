namespace Platform.Core.Models;

public enum SessionState { Queued, Classifying, Running, Validating, WritingBack, Completed, Failed }

public sealed class AgentRunSession
{
    public required Guid SessionId { get; init; }
    public required int PbiId { get; init; }
    public required PbiSpec Spec { get; init; }
    public SessionState State { get; set; } = SessionState.Queued;
    public string? ClassificationDecision { get; set; }
    public IReadOnlyList<ArtifactType> PlannedArtifacts { get; set; } = [];
    public List<AgentRunResult> Results { get; } = [];
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? PullRequestUrl { get; set; }
}
