namespace Platform.Core.Models;

/// <summary>Parsed representation of an Azure DevOps Product Backlog Item.</summary>
public sealed class PbiSpec
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? AcceptanceCriteria { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<ArtifactType> RequestedArtifacts { get; init; } = [];
    public string? AreaPath { get; init; }
    public string? IterationPath { get; init; }
}
