using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Platform.AzureDevOps;
using Platform.ClaudeRuntime;
using Platform.Core.Models;
using Platform.Orchestrator.Services;
using Xunit;

namespace Platform.Orchestrator.Tests;

public sealed class OrchestratorTests
{
    private static PbiSpec MakePbi(int id = 1) => new()
    {
        Id = id,
        Title = "Build customer search API",
        Description = "Expose a GET /customers endpoint that supports full-text search.",
        AcceptanceCriteria = "Returns HTTP 200 with paginated results for valid queries.",
        RequestedArtifacts = [ArtifactType.Api, ArtifactType.Tests],
    };

    [Fact]
    public async Task ProcessPbiAsync_WhenAdoAndClassifierSucceed_ReturnsCompletedSession()
    {
        // Arrange
        var pbi = MakePbi(42);
        var adoClient = Substitute.For<IAzureDevOpsClient>();
        adoClient.GetPbiAsync(42, Arg.Any<CancellationToken>()).Returns(pbi);

        var classifier = Substitute.For<IAnthropicClassifier>();
        classifier.ClassifyPbiAsync(pbi, Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(
                [ArtifactType.Api, ArtifactType.Tests],
                "Identified API and test artifact types from spec."));

        var runner = Substitute.For<IClaudeCodeRunner>();
        runner.RunAsync(Arg.Any<ClaudeCodeRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeCodeRunResult { Success = true, ExitCode = 0, Output = "[dry run]" });

        var sut = new PbiOrchestrationService(
            adoClient, classifier, runner, NullLogger<PbiOrchestrationService>.Instance);

        // Act
        var session = await sut.ProcessPbiAsync(42);

        // Assert
        Assert.Equal(SessionState.Completed, session.State);
        Assert.Equal(42, session.PbiId);
        Assert.Equal(2, session.Results.Count);
        Assert.All(session.Results, r => Assert.Equal(AgentRunStatus.Success, r.Status));
    }

    [Fact]
    public async Task ProcessPbiAsync_WhenSubagentFails_SessionCompletesWithFailureResult()
    {
        // Arrange
        var pbi = MakePbi(7);
        var adoClient = Substitute.For<IAzureDevOpsClient>();
        adoClient.GetPbiAsync(7, Arg.Any<CancellationToken>()).Returns(pbi);

        var classifier = Substitute.For<IAnthropicClassifier>();
        classifier.ClassifyPbiAsync(pbi, Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult([ArtifactType.Api], "Only API needed."));

        var runner = Substitute.For<IClaudeCodeRunner>();
        runner.RunAsync(Arg.Any<ClaudeCodeRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ClaudeCodeRunResult
            {
                Success = false,
                ExitCode = 1,
                ErrorOutput = "Simulated subprocess failure",
            });

        var sut = new PbiOrchestrationService(
            adoClient, classifier, runner, NullLogger<PbiOrchestrationService>.Instance);

        // Act
        var session = await sut.ProcessPbiAsync(7);

        // Assert
        Assert.Equal(SessionState.Completed, session.State);
        Assert.Single(session.Results);
        Assert.Equal(AgentRunStatus.Failure, session.Results[0].Status);
    }
}
