namespace Platform.AzureDevOps;

public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    public required string OrgUrl { get; init; }
    public required string Project { get; init; }
    /// <summary>Personal Access Token — read from configuration, never hardcoded.</summary>
    public required string PAT { get; init; }
}
