using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Orchestrators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace IdvEnrichment.Functions.Triggers;

public sealed class HttpEnrichTrigger(
    GraphServiceClient graphClient,
    ILogger<HttpEnrichTrigger> logger)
{
    [Function(nameof(HttpEnrich))]
    public async Task<HttpResponseData> HttpEnrich(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "enrich")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken ct = default)
    {
        var message = await JsonSerializer.DeserializeAsync<QueueMessage>(req.Body, cancellationToken: ct)
            ?? throw new InvalidOperationException("Request body could not be deserialized.");

        // Expand listItem.fields to check AIProcessingStatus without a separate fields request.
        var driveItem = await graphClient.Drives[message.DriveId]
            .Items[message.ItemId]
            .GetAsync(
                cfg => cfg.QueryParameters.Expand = ["listItem($expand=fields)"],
                cancellationToken: ct);

        if (driveItem?.Folder is not null)
        {
            var skipFolder = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await skipFolder.WriteStringAsync($"skipped: item {message.ItemId} is a folder, not a file", ct);
            logger.LogInformation("Skipped folder item {ItemId} in drive {DriveId}", message.ItemId, message.DriveId);
            return skipFolder;
        }

        string? processingStatus = null;
        if (driveItem?.ListItem?.Fields?.AdditionalData is { } additionalData &&
            additionalData.TryGetValue("AIProcessingStatus", out var statusObj))
        {
            processingStatus = statusObj as string;
        }

        if (processingStatus is "Classified" or "Reviewed")
        {
            var alreadyProcessed = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await alreadyProcessed.WriteStringAsync("already processed", ct);
            return alreadyProcessed;
        }

        var instanceId = $"trigger:{message.SiteId}:{message.ItemId}:{message.ModifiedDateTime:yyyyMMddHHmmss}";

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DocumentOrchestrator.DocumentProcessingOrchestrator),
            message,
            new StartOrchestrationOptions { InstanceId = instanceId },
            ct);

        logger.LogInformation("Started orchestration {InstanceId} for document {DocumentId}",
            instanceId, message.DocumentId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId }, cancellationToken: ct);
        return response;
    }
}
