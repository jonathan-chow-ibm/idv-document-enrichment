using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdvEnrichment.Functions.Orchestrators;

public sealed class BatchOrchestrator(IOptions<PipelineSettings> settings)
{
    [Function(nameof(BatchProcessingOrchestrator))]
    public async Task<BatchReport> BatchProcessingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var input = ctx.GetInput<BatchRequest>()
            ?? throw new InvalidOperationException("Batch orchestrator input was null.");

        var log = ctx.CreateReplaySafeLogger<BatchOrchestrator>();
        var batchId = ctx.InstanceId;

        var retry = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));

        var target = await ctx.CallActivityAsync<ResolvedSharePointTarget>(
            "ResolveSharePointTarget", input.Url, retry);

        var documents = await ctx.CallActivityAsync<IReadOnlyList<LibraryDocument>>(
            "EnumerateLibrary", target, retry);

        documents = FilterByItemIds(documents, input.ItemIds);

        var unprocessed = await ctx.CallActivityAsync<IReadOnlyList<LibraryDocument>>(
            "FilterProcessed", new FilterProcessedInput(documents, target.DriveId), retry);

        log.LogInformation("Batch {BatchId}: {Total} documents to process", batchId, unprocessed.Count);

        var maxConcurrency = input.MaxConcurrency ?? settings.Value.BatchMaxConcurrency;
        var chunkSize = input.ChunkSize ?? settings.Value.BatchChunkSize;
        var startedAt = ctx.CurrentUtcDateTime;

        // Split into chunks; each chunk orchestrator keeps its own history bounded
        var chunks = unprocessed
            .Select((doc, i) => (doc, i))
            .GroupBy(x => x.i / chunkSize)
            .Select((g, i) => (Index: i, Docs: (IReadOnlyList<LibraryDocument>)g.Select(x => x.doc).ToList()))
            .ToList();

        ctx.SetCustomStatus(new { Phase = "processing", Chunks = chunks.Count, Total = unprocessed.Count });

        // Run chunks sequentially so real concurrent OpenAI load never exceeds maxConcurrency
        // regardless of chunk count (each chunk sub-orchestration re-applies maxConcurrency
        // internally, so parallel chunks would multiply it). Top-level history still only
        // records one "chunk started/completed" pair per chunk, same as before.
        var chunkResults = new List<ChunkResult>(chunks.Count);
        foreach (var c in chunks)
        {
            var chunkResult = await ctx.CallSubOrchestratorAsync<ChunkResult>(
                "ChunkProcessingOrchestrator",
                new ChunkRequest(c.Docs, batchId, target, maxConcurrency, input.ClassifyOnly, input.MaxPages),
                new SubOrchestrationOptions { InstanceId = $"{batchId}:chunk:{c.Index}" });
            chunkResults.Add(chunkResult);
        }

        var results = chunkResults.SelectMany(r => r.Results).ToList();
        var errors = chunkResults.Sum(r => r.Errors);
        var failedDocuments = chunkResults.SelectMany(r => r.FailedDocuments).ToList();
        var lowConfidenceClassifications = chunkResults.SelectMany(r => r.LowConfidenceClassifications).ToList();

        return await ctx.CallActivityAsync<BatchReport>(
            "GenerateBatchReport",
            new GenerateBatchReportInput(batchId, input.Url, startedAt, results, errors, failedDocuments, lowConfidenceClassifications),
            retry);
    }

    // Scopes a re-run to only the requested item IDs, e.g. retrying documents that failed in a prior batch.
    internal static IReadOnlyList<LibraryDocument> FilterByItemIds(
        IReadOnlyList<LibraryDocument> documents, IReadOnlyList<string>? itemIds)
    {
        if (itemIds is null || itemIds.Count == 0)
        {
            return documents;
        }

        var requestedIds = new HashSet<string>(itemIds, StringComparer.Ordinal);
        return documents.Where(d => requestedIds.Contains(d.Id)).ToList();
    }
}
