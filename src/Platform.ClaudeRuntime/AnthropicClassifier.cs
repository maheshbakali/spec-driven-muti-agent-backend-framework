using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Platform.Core.Models;
using System.Text.Json;

namespace Platform.ClaudeRuntime;

public sealed class AnthropicClassifier : IAnthropicClassifier
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicClassifier> _logger;

    // Classification uses claude-opus-5 for accuracy; swap to Haiku if latency matters more.
    private const string ClassificationModel = "claude-opus-5";

    public AnthropicClassifier(AnthropicClient client, ILogger<AnthropicClassifier> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyPbiAsync(PbiSpec spec, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(spec);
        _logger.LogInformation("Classifying PBI {PbiId} with {Model}", spec.Id, ClassificationModel);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = ClassificationModel,
            MaxTokens = 1024,
            System = BuildSystemPrompt(),
            Messages =
            [
                new() { Role = Role.User, Content = prompt },
            ],
        }, cancellationToken: ct);

        var rawText = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        return ParseClassification(rawText);
    }

    private static string BuildSystemPrompt() =>
        """
        You are a technical classifier for a code-generation platform.
        Given an Azure DevOps Product Backlog Item, decide which artifact types should be generated.
        Respond ONLY with a JSON object in this exact shape:
        {
          "artifactTypes": ["Api", "Tests"],
          "rationale": "One sentence explaining the decision."
        }
        Valid artifact types: Api, EventDrivenPattern, Infra, Tests, Pipeline, DesignDoc.
        """;

    private static string BuildPrompt(PbiSpec spec) =>
        $"""
        PBI ID: {spec.Id}
        Title: {spec.Title}
        Description: {spec.Description ?? "(none)"}
        Acceptance Criteria: {spec.AcceptanceCriteria ?? "(none)"}
        Tags: {string.Join(", ", spec.Tags)}
        """;

    private ClassificationResult ParseClassification(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var types = root.GetProperty("artifactTypes")
                .EnumerateArray()
                .Select(e => Enum.Parse<ArtifactType>(e.GetString()!))
                .ToList();

            var rationale = root.GetProperty("rationale").GetString() ?? string.Empty;
            return new ClassificationResult(types, rationale);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse classification JSON; defaulting to Api + Tests. Raw: {Raw}", raw);
            return new ClassificationResult(
                [ArtifactType.Api, ArtifactType.Tests],
                "Defaulted due to parse failure.");
        }
    }
}
