using Microsoft.Extensions.Logging;
using Platform.Core.Models;

namespace Platform.AzureDevOps;

public sealed class AzureDevOpsClient : IAzureDevOpsClient
{
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsClient> _logger;

    public AzureDevOpsClient(AzureDevOpsOptions options, ILogger<AzureDevOpsClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<PbiSpec> GetPbiAsync(int pbiId, CancellationToken ct = default)
    {
        // TODO: Call GET {OrgUrl}/{Project}/_apis/wit/workitems/{id}?api-version=7.1
        // and map the JSON response fields to PbiSpec.
        _logger.LogInformation("Fetching PBI {PbiId} from {OrgUrl}", pbiId, _options.OrgUrl);

        var stub = new PbiSpec
        {
            Id = pbiId,
            Title = $"[STUB] PBI {pbiId}",
            Description = "Stub description — replace with real ADO call.",
            AcceptanceCriteria = "AC not yet loaded.",
            RequestedArtifacts = [ArtifactType.Api, ArtifactType.Tests],
        };
        return Task.FromResult(stub);
    }

    public Task<IReadOnlyList<int>> GetChildTaskIdsAsync(int pbiId, CancellationToken ct = default)
    {
        // TODO: Query work-item relations for type "Child".
        _logger.LogInformation("Fetching child tasks for PBI {PbiId}", pbiId);
        IReadOnlyList<int> empty = [];
        return Task.FromResult(empty);
    }

    public Task<string> CreatePullRequestAsync(
        string repositoryId,
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        int linkedPbiId,
        CancellationToken ct = default)
    {
        // TODO: POST {OrgUrl}/{Project}/_apis/git/repositories/{repositoryId}/pullrequests
        // Include "AB#{linkedPbiId}" in the description so ADO auto-links the work item.
        _logger.LogInformation(
            "Creating PR in {Repo}: {Source} -> {Target} linked to AB#{PbiId}",
            repositoryId, sourceBranch, targetBranch, linkedPbiId);

        var stubUrl = $"{_options.OrgUrl}/{_options.Project}/_git/{repositoryId}/pullrequest/0";
        return Task.FromResult(stubUrl);
    }

    public Task UpdateWorkItemStateAsync(int workItemId, string state, CancellationToken ct = default)
    {
        // TODO: PATCH {OrgUrl}/{Project}/_apis/wit/workitems/{id} with JSON Patch op
        // to set /fields/System.State.
        _logger.LogInformation("Updating work item {Id} state to '{State}'", workItemId, state);
        return Task.CompletedTask;
    }

    public Task AttachDocumentAsync(int workItemId, string fileName, string content, CancellationToken ct = default)
    {
        // TODO: POST attachment binary, then PATCH work item to add the attachment relation.
        _logger.LogInformation("Attaching '{FileName}' to work item {Id}", fileName, workItemId);
        return Task.CompletedTask;
    }
}
