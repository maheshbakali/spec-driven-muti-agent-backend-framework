namespace Platform.ClaudeRuntime;

public sealed class ClaudeCodeOptions
{
    public const string SectionName = "ClaudeCode";

    /// <summary>Path to the <c>claude</c> executable. Defaults to PATH lookup.</summary>
    public string ClaudeExecutablePath { get; init; } = "claude";

    /// <summary>
    /// When true, <see cref="ClaudeCodeProcessRunner"/> logs the command it would run
    /// but does not spawn a subprocess. Flip to false to enable real execution.
    /// </summary>
    public bool DryRun { get; init; } = true;
}
