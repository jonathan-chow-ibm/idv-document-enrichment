using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Orchestrators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Triggers;

public sealed class HttpBatchTrigger(ILogger<HttpBatchTrigger> logger)
{
    [Function(nameof(HttpBatch))]
    public async Task<HttpResponseData> HttpBatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "batch")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken ct = default)
    {
        var batchRequest = await JsonSerializer.DeserializeAsync<BatchRequest>(req.Body, cancellationToken: ct)
            ?? throw new InvalidOperationException("Request body could not be deserialized.");

        if (string.IsNullOrWhiteSpace(batchRequest.Url) ||
            !Uri.TryCreate(batchRequest.Url, UriKind.Absolute, out _))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Url must be a valid absolute URI.", ct);
            return badResponse;
        }

        if (batchRequest.MaxConcurrency is { } maxConcurrency && maxConcurrency <= 0)
        {
            // Zero silently degrades the chunk orchestrator's sliding window to a no-op that returns an
            // empty, error-free result instead of processing anything -- reject it here rather than let
            // a batch that never touched the client's SharePoint site look like it succeeded.
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("MaxConcurrency must be a positive integer.", ct);
            return badResponse;
        }

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(BatchOrchestrator.BatchProcessingOrchestrator),
            batchRequest,
            ct);

        logger.LogInformation("Started batch orchestration {InstanceId} for URL {Url}",
            instanceId, batchRequest.Url);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId }, cancellationToken: ct);
        return response;
    }
}
