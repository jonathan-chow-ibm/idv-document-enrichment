using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Orchestrators;

public sealed class ChunkOrchestrator
{
    [Function(nameof(ChunkProcessingOrchestrator))]
    public async Task<ChunkResult> ChunkProcessingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var input = ctx.GetInput<ChunkRequest>()
            ?? throw new InvalidOperationException("Chunk orchestrator input was null.");

        var log = ctx.CreateReplaySafeLogger<ChunkOrchestrator>();
        var results = new List<BatchDocumentEntry>();
        var errors = 0;

        // Process documents in parallel groups bounded by maxConcurrency
        for (var i = 0; i < input.Documents.Count; i += input.MaxConcurrency)
        {
            var group = input.Documents.Skip(i).Take(input.MaxConcurrency).ToList();

            var tasks = group.Select(doc => ProcessDocSafeAsync(ctx, doc, input, log));
            var groupResults = await Task.WhenAll(tasks);

            foreach (var (result, succeeded) in groupResults)
            {
                if (succeeded)
                {
                    results.Add(result!);
                }
                else
                {
                    errors++;
                }
            }
        }

        return new ChunkResult(results, errors);
    }

    private static async Task<(BatchDocumentEntry? Result, bool Succeeded)> ProcessDocSafeAsync(
        TaskOrchestrationContext ctx, LibraryDocument doc, ChunkRequest input, ILogger log)
    {
        try
        {
            var result = await ctx.CallSubOrchestratorAsync<EnrichmentResult>(
                "DocumentProcessingOrchestrator",
                new QueueMessage(
                    DocumentId: doc.Id,
                    SiteId: input.Target.SiteId,
                    DriveId: input.Target.DriveId,
                    ItemId: doc.Id,
                    FileName: doc.RelativePath,
                    FileUrl: doc.DownloadUrl,
                    ContentType: doc.MimeType,
                    ModifiedDateTime: doc.LastModifiedDateTime,
                    Source: ProcessingSource.Batch,
                    BatchId: input.BatchId),
                new SubOrchestrationOptions { InstanceId = $"{input.BatchId}:{doc.Id}" });

            return (new BatchDocumentEntry(
                result.TypeClassification.DocumentType,
                result.RoutingDecision,
                result.TypeClassification.Confidence,
                result.WriteBackSucceeded,
                result.ProcessingMetrics.ClassificationInputTokens,
                result.ProcessingMetrics.ClassificationOutputTokens,
                result.ProcessingMetrics.ExtractionInputTokens,
                result.ProcessingMetrics.ExtractionOutputTokens,
                result.ProcessingMetrics.VisionInputTokens,
                result.ProcessingMetrics.VisionOutputTokens), true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TaskFailedException ex)
        {
            log.LogWarning(ex, "Document {DocId} failed after retries", doc.Id);
            return (null, false);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error processing document {DocId}", doc.Id);
            return (null, false);
        }
    }
}
