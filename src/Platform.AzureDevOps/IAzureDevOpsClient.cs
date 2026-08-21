using Platform.Core.Models;

namespace Platform.AzureDevOps;

/// <summary>
/// Abstraction over Azure DevOps REST API calls needed by the platform.
/// Mock this in tests; inject the real implementation in production.
/// </summary>
public interface IAzureDevOpsClient
{
    /// <summary>Fetch a PBI work item and parse it into a <see cref="PbiSpec"/>.</summary>
    Task<PbiSpec> GetPbiAsync(int pbiId, CancellationToken ct = default);

    /// <summary>Return the IDs of child task work items linked to the PBI.</summary>
    Task<IReadOnlyList<int>> GetChildTaskIdsAsync(int pbiId, CancellationToken ct = default);

    /// <summary>
    /// Create a PR in the given repository that links back to the PBI via AB#&lt;id&gt;.
    /// Returns the PR web URL.
    /// </summary>
    Task<string> CreatePullRequestAsync(
        string repositoryId,
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        int linkedPbiId,
        CancellationToken ct = default);

    /// <summary>Update the PBI's state field (e.g., "In Progress", "Done").</summary>
    Task UpdateWorkItemStateAsync(int workItemId, string state, CancellationToken ct = default);

    /// <summary>Attach a text/markdown document to the PBI as a work-item attachment.</summary>
    Task AttachDocumentAsync(int workItemId, string fileName, string content, CancellationToken ct = default);
}
