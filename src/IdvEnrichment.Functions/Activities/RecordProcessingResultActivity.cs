using Azure.Data.Tables;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;

namespace IdvEnrichment.Functions.Activities;

public sealed class RecordProcessingResultActivity(TableServiceClient tableServiceClient)
{
    private const string TableName = "ProcessingTracking";

    [Function(nameof(RecordProcessingResult))]
    public async Task RecordProcessingResult(
        [ActivityTrigger] RecordProcessingResultInput input,
        CancellationToken ct = default)
    {
        var tableClient = tableServiceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync(ct);

        var entity = new TableEntity(input.BatchId, input.DocumentId)
        {
            ["Status"] = input.Status,
            ["ProcessedAt"] = DateTimeOffset.UtcNow,
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }
}
