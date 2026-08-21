using Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.AzureDevOps;
using Platform.ClaudeRuntime;
using Platform.Orchestrator.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, config) =>
    {
        config
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)   // dotnet user-secrets for local dev
            .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // --- Anthropic C# SDK client ---
        services.AddSingleton(_ =>
        {
            var apiKey = config["Anthropic:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Anthropic:ApiKey is not configured. " +
                    "Run: dotnet user-secrets set \"Anthropic:ApiKey\" \"sk-ant-...\"");
            return new AnthropicClient { ApiKey = apiKey };
        });

        // --- Azure DevOps ---
        services.AddSingleton(_ =>
        {
            var section = config.GetSection(AzureDevOpsOptions.SectionName);
            return new AzureDevOpsOptions
            {
                OrgUrl = section["OrgUrl"] ?? throw new InvalidOperationException("AzureDevOps:OrgUrl missing"),
                Project = section["Project"] ?? throw new InvalidOperationException("AzureDevOps:Project missing"),
                PAT = section["PAT"] ?? throw new InvalidOperationException("AzureDevOps:PAT missing"),
            };
        });
        services.AddSingleton<IAzureDevOpsClient, AzureDevOpsClient>();

        // --- Claude Code runner ---
        services.AddSingleton(_ =>
        {
            var section = config.GetSection(ClaudeCodeOptions.SectionName);
            return new ClaudeCodeOptions
            {
                ClaudeExecutablePath = section["ClaudeExecutablePath"] ?? "claude",
                DryRun = section.GetValue("DryRun", defaultValue: true),
            };
        });
        services.AddSingleton<IClaudeCodeRunner, ClaudeCodeProcessRunner>();

        // --- Anthropic classifier (lightweight SDK calls) ---
        services.AddSingleton<IAnthropicClassifier, AnthropicClassifier>();

        // --- Orchestration ---
        services.AddSingleton<PbiOrchestrationService>();
    })
    .Build();

// Minimal end-to-end stub: process a single PBI ID from the CLI arg or default.
// Replace this with a queue-triggered BackgroundService when ready.
var pbiId = args.Length > 0 && int.TryParse(args[0], out var id) ? id : 42;

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting stub run for PBI {PbiId}", pbiId);

var orchestrator = host.Services.GetRequiredService<PbiOrchestrationService>();
var session = await orchestrator.ProcessPbiAsync(pbiId);

logger.LogInformation(
    "Stub run complete. Session {SessionId} finished with state {State}. PR URL: {PrUrl}",
    session.SessionId, session.State, session.PullRequestUrl ?? "(not set)");
