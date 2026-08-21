using Microsoft.Extensions.Logging;
using Platform.AzureDevOps;
using Platform.ClaudeRuntime;
using Platform.Core.Models;

namespace Platform.Orchestrator.Services;

/// <summary>
/// Core processing loop for a single PBI.
/// Inject into a hosted BackgroundService or call directly from tests.
/// </summary>
public sealed class PbiOrchestrationService
{
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IAnthropicClassifier _classifier;
    private readonly IClaudeCodeRunner _codeRunner;
    private readonly ILogger<PbiOrchestrationService> _logger;

    public PbiOrchestrationService(
        IAzureDevOpsClient adoClient,
        IAnthropicClassifier classifier,
        IClaudeCodeRunner codeRunner,
        ILogger<PbiOrchestrationService> logger)
    {
        _adoClient = adoClient;
        _classifier = classifier;
        _codeRunner = codeRunner;
        _logger = logger;
    }

    public async Task<AgentRunSession> ProcessPbiAsync(int pbiId, CancellationToken ct = default)
    {
        var session = new AgentRunSession
        {
            SessionId = Guid.NewGuid(),
            PbiId = pbiId,
            Spec = await FetchSpecAsync(pbiId, ct),
            State = SessionState.Classifying,
        };

        _logger.LogInformation("Session {SessionId}: processing PBI {PbiId}", session.SessionId, pbiId);

        // Step 1 — classify with the Anthropic SDK (lightweight, single-shot)
        var classification = await _classifier.ClassifyPbiAsync(session.Spec, ct);
        session.ClassificationDecision = classification.Rationale;
        session.PlannedArtifacts = classification.ArtifactTypes;
        session.State = SessionState.Running;

        _logger.LogInformation(
            "Session {SessionId}: classified → {Artifacts}. Rationale: {Rationale}",
            session.SessionId,
            string.Join(", ", classification.ArtifactTypes),
            classification.Rationale);

        // Step 2 — dispatch one headless Claude Code subagent per artifact type
        foreach (var artifactType in session.PlannedArtifacts)
        {
            var result = await RunSubagentAsync(session, artifactType, ct);
            session.Results.Add(result);
        }

        session.State = SessionState.Completed;
        session.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Session {SessionId}: all subagents done. Results: {Results}",
            session.SessionId,
            string.Join(", ", session.Results.Select(r => $"{r.ArtifactType}={r.Status}")));

        return session;
    }

    private async Task<PbiSpec> FetchSpecAsync(int pbiId, CancellationToken ct)
    {
        _logger.LogInformation("Fetching PBI {PbiId} from Azure DevOps", pbiId);
        return await _adoClient.GetPbiAsync(pbiId, ct);
    }

    private async Task<AgentRunResult> RunSubagentAsync(
        AgentRunSession session,
        ArtifactType artifactType,
        CancellationToken ct)
    {
        var prompt = BuildSubagentPrompt(session.Spec, artifactType);
        var workDir = Path.Combine(Path.GetTempPath(), $"platform-{session.SessionId}", artifactType.ToString());
        Directory.CreateDirectory(workDir);

        _logger.LogInformation(
            "Session {SessionId}: invoking Claude Code subagent for {ArtifactType} in {WorkDir}",
            session.SessionId, artifactType, workDir);

        var request = new ClaudeCodeRunRequest
        {
            Prompt = prompt,
            ArtifactType = artifactType,
            WorkingDirectory = workDir,
            AgentDefinitionPath = ResolveAgentDefinition(artifactType),
        };

        var start = DateTimeOffset.UtcNow;
        var runResult = await _codeRunner.RunAsync(request, ct);

        return new AgentRunResult
        {
            ArtifactType = artifactType,
            Status = runResult.Success ? AgentRunStatus.Success : AgentRunStatus.Failure,
            Output = runResult.Output,
            ErrorDetail = runResult.ErrorOutput,
            Duration = runResult.Duration,
        };
    }

    private static string BuildSubagentPrompt(PbiSpec spec, ArtifactType artifactType) =>
        $"""
        You are generating a {artifactType} artifact for the following PBI.

        PBI ID: {spec.Id}
        Title: {spec.Title}
        Description: {spec.Description ?? "(none)"}
        Acceptance Criteria: {spec.AcceptanceCriteria ?? "(none)"}

        Generate production-quality {artifactType} code/config that satisfies the above spec.
        Use idiomatic .NET 10 patterns where code is involved.
        Write all output files to the current working directory.
        """;

    private static string? ResolveAgentDefinition(ArtifactType artifactType) =>
        artifactType switch
        {
            ArtifactType.Api => ".claude/agents/api-codegen.md",
            ArtifactType.Infra => ".claude/agents/bicep-infra.md",
            ArtifactType.Tests => ".claude/agents/test-gen.md",
            ArtifactType.Pipeline => ".claude/agents/pipeline-docs.md",
            _ => null,
        };
}
