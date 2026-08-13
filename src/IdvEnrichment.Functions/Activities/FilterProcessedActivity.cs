using Azure.Data.Tables;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;

namespace IdvEnrichment.Functions.Activities;

public sealed class FilterProcessedActivity(TableServiceClient tableServiceClient)
{
    private const string TableName = "ProcessingTracking";

    [Function(nameof(FilterProcessed))]
    public async Task<IReadOnlyList<LibraryDocument>> FilterProcessed(
        [ActivityTrigger] FilterProcessedInput input,
        CancellationToken ct = default)
    {
        var tableClient = tableServiceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync(ct);

        var documentIds = input.Documents.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var processedIds = new HashSet<string>(StringComparer.Ordinal);

        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {input.BatchId}");
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct))
        {
            if (documentIds.Contains(entity.RowKey))
            {
                processedIds.Add(entity.RowKey);
            }
        }

        return input.Documents
            .Where(d => !processedIds.Contains(d.Id))
            .ToList();
    }
}
