using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Platform.Ingestion.Functions;

/// <summary>
/// Service Bus queue–triggered function. Picks up a PBI ID from the queue
/// and hands it to the orchestrator (future: call the Worker Service via gRPC or HTTP).
/// </summary>
public sealed class PbiQueueTriggerFunction
{
    private readonly ILogger<PbiQueueTriggerFunction> _logger;

    public PbiQueueTriggerFunction(ILogger<PbiQueueTriggerFunction> logger)
    {
        _logger = logger;
    }

    [Function("PbiQueueTrigger")]
    public async Task RunAsync(
        [ServiceBusTrigger("pbi-work-queue", Connection = "ServiceBus:ConnectionString")] string message,
        FunctionContext context)
    {
        _logger.LogInformation("Dequeued PBI message: {Message}", message);

        if (!int.TryParse(message, out var pbiId))
        {
            _logger.LogError("Invalid PBI ID in queue message: {Message}", message);
            return;
        }

        // TODO: Forward pbiId to Platform.Orchestrator (Worker Service).
        // Options: HTTP call, shared Service Bus topic, or Azure Durable Functions.
        _logger.LogInformation("PBI {PbiId} dequeued — hand off to orchestrator (TODO)", pbiId);

        await Task.CompletedTask;
    }
}
