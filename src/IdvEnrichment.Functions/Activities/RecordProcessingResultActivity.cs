using Azure.Data.Tables;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Activities;

public sealed class RecordProcessingResultActivity(TableServiceClient tableServiceClient, ILogger<RecordProcessingResultActivity> logger)
{
    private const string TableName = "ProcessingTracking";

    [Function(nameof(RecordProcessingResult))]
    public async Task RecordProcessingResult(
        [ActivityTrigger] RecordProcessingResultInput input,
        CancellationToken ct = default)
    {
        var tableClient = tableServiceClient.GetTableClient(TableName);

        var entity = new TableEntity(input.LibraryKey, input.DocumentId)
        {
            ["Status"] = input.Status,
            ["BatchId"] = input.BatchId,
            ["ProcessedAt"] = DateTimeOffset.UtcNow,
        };

        await SdkExceptionHelper.RunAsync(async () =>
        {
            await tableClient.CreateIfNotExistsAsync(ct);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
        }, $"Processing-result write for document {input.DocumentId}", logger);
    }
}
