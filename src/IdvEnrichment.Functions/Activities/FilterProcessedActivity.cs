using Azure.Data.Tables;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Activities;

public sealed class FilterProcessedActivity(TableServiceClient tableServiceClient, ILogger<FilterProcessedActivity> logger)
{
    private const string TableName = "ProcessingTracking";

    // Durably-terminal statuses only — a document in one of these states may already have
    // human-reviewed or corrected output downstream, so it must never be silently reprocessed
    // and overwritten. Errors and write-back failures are left out on purpose: no trustworthy
    // output exists yet, so those stay eligible for retry on the next run.
    private static readonly HashSet<string> SkippableStatuses = new(StringComparer.Ordinal) { "success", "review" };

    [Function(nameof(FilterProcessed))]
    public async Task<IReadOnlyList<LibraryDocument>> FilterProcessed(
        [ActivityTrigger] FilterProcessedInput input,
        CancellationToken ct = default)
    {
        var tableClient = tableServiceClient.GetTableClient(TableName);

        var documentIds = input.Documents.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var processedIds = new HashSet<string>(StringComparer.Ordinal);

        await SdkExceptionHelper.RunAsync(async () =>
        {
            await tableClient.CreateIfNotExistsAsync(ct);

            // Partitioned by library (drive), not by batch run, so this survives across restarts,
            // stalls, and cost-driven stops against the same library instead of starting from zero.
            var filter = TableClient.CreateQueryFilter($"PartitionKey eq {input.LibraryKey}");
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct))
            {
                if (documentIds.Contains(entity.RowKey) && ShouldSkip(entity.GetString("Status")))
                {
                    processedIds.Add(entity.RowKey);
                }
            }
        }, $"Processed-document lookup for library {input.LibraryKey}", logger);

        return input.Documents
            .Where(d => !processedIds.Contains(d.Id))
            .ToList();
    }

    internal static bool ShouldSkip(string? status) => status is not null && SkippableStatuses.Contains(status);
}
