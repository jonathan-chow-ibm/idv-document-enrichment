using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
        var failedDocuments = new List<FailedDocumentEntry>();
        var lowConfidenceClassifications = new List<LowConfidenceClassificationEntry>();
        var errors = 0;

        // Process documents in parallel groups bounded by maxConcurrency
        for (var i = 0; i < input.Documents.Count; i += input.MaxConcurrency)
        {
            var group = input.Documents.Skip(i).Take(input.MaxConcurrency).ToList();

            var tasks = group.Select(doc => ProcessDocSafeAsync(ctx, doc, input, log));
            var groupResults = await Task.WhenAll(tasks);

            foreach (var (result, succeeded, failedDocument, lowConfidenceEntry) in groupResults)
            {
                if (succeeded)
                {
                    results.Add(result!);
                    if (lowConfidenceEntry is not null)
                    {
                        lowConfidenceClassifications.Add(lowConfidenceEntry);
                    }
                }
                else
                {
                    errors++;
                    failedDocuments.Add(failedDocument!);
                }
            }
        }

        return new ChunkResult(results, errors, failedDocuments, lowConfidenceClassifications);
    }

    private static async Task<(BatchDocumentEntry? Result, bool Succeeded, FailedDocumentEntry? FailedDocument, LowConfidenceClassificationEntry? LowConfidenceEntry)> ProcessDocSafeAsync(
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
                    FileUrl: string.Empty,
                    ContentType: doc.MimeType,
                    ModifiedDateTime: doc.LastModifiedDateTime,
                    Source: ProcessingSource.Batch,
                    BatchId: input.BatchId,
                    ClassifyOnly: input.ClassifyOnly,
                    MaxPages: input.MaxPages),
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
                result.ProcessingMetrics.VisionOutputTokens,
                result.Metadata?.SuggestedFields.Select(f => f.Key).ToList() ?? [],
                doc.Size),
                true, null,
                BuildLowConfidenceEntry(doc.Id, doc.RelativePath, result.TypeClassification));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TaskFailedException ex)
        {
            log.LogWarning(ex, "Document {DocId} failed after retries", doc.Id);
            return (null, false, new FailedDocumentEntry(doc.Id, doc.RelativePath), null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error processing document {DocId}", doc.Id);
            return (null, false, new FailedDocumentEntry(doc.Id, doc.RelativePath), null);
        }
    }

    // Internal for testability: Agent 1 only populates Candidates when it couldn't confidently settle
    // on one type, so this is null whenever there's nothing worth surfacing to a human reviewer.
    internal static LowConfidenceClassificationEntry? BuildLowConfidenceEntry(
        string documentId, string fileName, TypeClassificationResult classification)
    {
        if (classification.Candidates is not { Count: > 0 } candidates)
        {
            return null;
        }

        return new LowConfidenceClassificationEntry(
            documentId,
            fileName,
            JsonSerializer.Serialize(classification.DocumentType).Trim('"'),
            classification.Confidence,
            candidates
                // Already a string — carries the model's own wording, including types outside the taxonomy.
                .Select(c => new ClassificationCandidateEntry(c.DocumentType, c.Confidence))
                .ToList());
    }
}
