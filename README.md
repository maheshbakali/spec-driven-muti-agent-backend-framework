# Spec-Driven Multi-Agent Backend Platform

A .NET 10 platform that turns Azure DevOps Product Backlog Items into backend
artifacts (API code, Bicep infrastructure, tests, pipeline YAML, design docs)
by dispatching work to Claude-powered subagents and writing results back to
Azure DevOps as a PR.

## Solution layout

```
PlatformSolution.sln
├── src/
│   ├── Platform.Core/              Shared models (PbiSpec, AgentRunResult, …)
│   ├── Platform.AzureDevOps/       ADO REST client wrapper (IAzureDevOpsClient)
│   ├── Platform.ClaudeRuntime/     Anthropic SDK classifier + Claude Code runner
│   ├── Platform.Ingestion/         Azure Functions: webhook receiver + queue trigger
│   └── Platform.Orchestrator/      Worker Service: main PBI processing loop
└── tests/
    └── Platform.Orchestrator.Tests/ xUnit tests
```

## Claude integration paths

| Path | Used for |
|---|---|
| **Anthropic C# SDK** (`Anthropic` NuGet) | Single-shot classification call — decides which artifact types to generate |
| **Headless Claude Code** (`claude -p`) | Generative subagents — writes code, runs `dotnet build`/`dotnet test`, fixes failures |

## Running locally (stub mode)

All secrets go in **user secrets** — never in `appsettings.json`.

```bash
cd src/Platform.Orchestrator

# One-time setup
dotnet user-secrets set "Anthropic:ApiKey"        "sk-ant-..."
dotnet user-secrets set "AzureDevOps:OrgUrl"      "https://dev.azure.com/YOUR_ORG"
dotnet user-secrets set "AzureDevOps:Project"     "YOUR_PROJECT"
dotnet user-secrets set "AzureDevOps:PAT"         "your-ado-pat"
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://..."

# Run against PBI 42 (uses stub ADO data, logs Claude Code command but does not exec)
dotnet run -- 42
```

`ClaudeCode:DryRun` is `true` by default — the orchestrator logs the `claude -p`
command it *would* run without actually spawning a subprocess. Set it to `false`
(via user secrets or environment) once the `claude` CLI is installed and you are
ready for real generation.

## Subagent definitions

`.claude/agents/` contains one markdown + YAML-frontmatter file per subagent:

| File | Purpose |
|---|---|
| `api-codegen.md` | .NET Web API generation (starter — fully wired) |
| `bicep-infra.md` | Azure Bicep infrastructure (add yourself) |
| `test-gen.md` | Additional test generation (add yourself) |
| `pipeline-docs.md` | ADO pipeline YAML + design docs (add yourself) |

## What needs real wiring

| Component | Status | Where to look |
|---|---|---|
| ADO REST calls | **Stub** — returns fake data | `AzureDevOpsClient.cs` — each method has a `TODO` comment |
| Anthropic classifier | **Real SDK call** — runs on startup | `AnthropicClassifier.cs` |
| Claude Code subprocess | **Dry-run** — logs command only | `ClaudeCodeProcessRunner.cs` — set `DryRun: false` |
| Service Bus queue trigger | **Stub** | `PbiQueueTriggerFunction.cs` |
| PR creation / write-back | **Stub** | `AzureDevOpsClient.CreatePullRequestAsync` |

## Running tests

```bash
dotnet test tests/Platform.Orchestrator.Tests
```

The test project uses **NSubstitute** to mock `IAzureDevOpsClient`,
`IAnthropicClassifier`, and `IClaudeCodeRunner` — no live credentials needed.
