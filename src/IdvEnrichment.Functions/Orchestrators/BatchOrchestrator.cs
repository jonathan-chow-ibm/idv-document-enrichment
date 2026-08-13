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

        var unprocessed = await ctx.CallActivityAsync<IReadOnlyList<LibraryDocument>>(
            "FilterProcessed", new FilterProcessedInput(documents, batchId), retry);

        log.LogInformation("Batch {BatchId}: {Total} documents to process", batchId, unprocessed.Count);

        var maxConcurrency = input.MaxConcurrency ?? settings.Value.BatchMaxConcurrency;
        var results = new List<EnrichmentResult>(unprocessed.Count);
        var errors = 0;
        var startedAt = ctx.CurrentUtcDateTime;

        for (var i = 0; i < unprocessed.Count; i += maxConcurrency)
        {
            var chunk = unprocessed.Skip(i).Take(maxConcurrency).ToList();

            // Process each document with its own try/catch to isolate failures
            var chunkSuccesses = 0;
            var chunkErrors = 0;
            foreach (var doc in chunk)
            {
                try
                {
                    var result = await ctx.CallSubOrchestratorAsync<EnrichmentResult>(
                        "DocumentProcessingOrchestrator",
                        new QueueMessage(
                            DocumentId: doc.Id,
                            SiteId: target.SiteId,
                            DriveId: target.DriveId,
                            ItemId: doc.Id,
                            FileName: doc.Name,
                            FileUrl: doc.DownloadUrl,
                            ContentType: doc.MimeType,
                            ModifiedDateTime: doc.LastModifiedDateTime,
                            Source: ProcessingSource.Batch,
                            BatchId: batchId),
                        new SubOrchestrationOptions { InstanceId = $"{batchId}:{doc.Id}" });
                    results.Add(result);
                    chunkSuccesses++;
                }
                catch (Exception)
                {
                    chunkErrors++;
                }
            }

            errors += chunkErrors;

            ctx.SetCustomStatus(new
            {
                Processed = results.Count + errors,
                Total = unprocessed.Count,
                Errors = errors,
                PercentComplete = Math.Round((results.Count + errors) / (double)unprocessed.Count * 100, 1),
            });
        }

        return await ctx.CallActivityAsync<BatchReport>(
            "GenerateBatchReport",
            new GenerateBatchReportInput(batchId, input.Url, startedAt, results, errors),
            retry);
    }
}
