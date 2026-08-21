using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Platform.Ingestion.Functions;

/// <summary>
/// HTTP-triggered function that receives Azure DevOps service hook payloads
/// (work item state change) and forwards the PBI ID to the Service Bus queue.
/// </summary>
public sealed class AdoWebhookFunction
{
    private readonly ILogger<AdoWebhookFunction> _logger;

    public AdoWebhookFunction(ILogger<AdoWebhookFunction> logger)
    {
        _logger = logger;
    }

    [Function("AdoWebhook")]
    [ServiceBusOutput("pbi-work-queue", Connection = "ServiceBus:ConnectionString")]
    public async Task<string?> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ado/webhook")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("ADO webhook received");

        // TODO: Validate HMAC-SHA1 signature from the X-Hub-Signature header.
        // See: https://learn.microsoft.com/azure/devops/service-hooks/events

        string body = await req.ReadAsStringAsync() ?? string.Empty;
        int? pbiId = TryExtractPbiId(body);

        if (pbiId is null)
        {
            _logger.LogWarning("Could not extract PBI ID from payload — discarding");
            return null;
        }

        _logger.LogInformation("Enqueuing PBI {PbiId} for processing", pbiId);

        // Return value is routed to the ServiceBus output binding.
        return pbiId.Value.ToString();
    }

    private static int? TryExtractPbiId(string json)
    {
        // TODO: Map the real ADO webhook payload shape.
        // Field path: resource.workItemId or resource.revision.id depending on event type.
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("resource", out var resource) &&
                resource.TryGetProperty("workItemId", out var id))
            {
                return id.GetInt32();
            }
        }
        catch { /* fall through */ }
        return null;
    }
}
