using Platform.Core.Models;

namespace Platform.ClaudeRuntime;

/// <summary>
/// Lightweight Claude SDK calls for classification and routing decisions.
/// These are single-shot API calls — not headless Claude Code subagents.
/// </summary>
public interface IAnthropicClassifier
{
    /// <summary>
    /// Given a PBI spec, return the list of artifact types that should be generated
    /// and a short natural-language rationale.
    /// </summary>
    Task<ClassificationResult> ClassifyPbiAsync(PbiSpec spec, CancellationToken ct = default);
}

public sealed record ClassificationResult(
    IReadOnlyList<ArtifactType> ArtifactTypes,
    string Rationale);
