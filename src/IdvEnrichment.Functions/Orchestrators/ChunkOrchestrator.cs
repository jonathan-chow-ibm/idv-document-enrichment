using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IdvEnrichment.Functions.Orchestrators;

public sealed class ChunkOrchestrator
{
    // Bounds a single document's sub-orchestration. Generous enough that a slow-but-healthy document
    // still finishes: the longest activity is bounded by the host's 10-minute functionTimeout, and a
    // document runs several of them in sequence. Safe to keep generous now that the sliding window
    // below means an expiring document only ever occupies its own concurrency slot, rather than
    // blocking a whole fixed-size group (and the rest of the batch behind it) for the full duration.
    private static readonly TimeSpan DocumentTimeout = TimeSpan.FromMinutes(20);

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

        // Sliding window bounded by maxConcurrency: a new document starts the moment any in-flight
        // slot frees up, instead of waiting for a whole fixed-size group to finish first. Documents
        // are started in order and Task.WhenAny is re-evaluated over a deterministic, monotonically
        // advancing set of ctx-issued tasks, so replay still reproduces the same sequence of "which
        // one finished next" from history.
        var nextIndex = 0;
        var inFlight = new List<Task<DocumentOutcome>>(Math.Min(input.MaxConcurrency, input.Documents.Count));

        void StartNext()
        {
            if (nextIndex < input.Documents.Count)
            {
                inFlight.Add(ProcessDocSafeAsync(ctx, input.Documents[nextIndex++], input, log));
            }
        }

        while (inFlight.Count < input.MaxConcurrency && nextIndex < input.Documents.Count)
        {
            StartNext();
        }

        while (inFlight.Count > 0)
        {
            var completed = await Task.WhenAny(inFlight);
            inFlight.Remove(completed);
            StartNext();

            var outcome = await completed;
            if (outcome.Succeeded)
            {
                results.Add(outcome.Result!);
                if (outcome.LowConfidenceEntry is not null)
                {
                    lowConfidenceClassifications.Add(outcome.LowConfidenceEntry);
                }
            }
            else
            {
                errors++;
                failedDocuments.Add(outcome.FailedDocument!);
            }
        }

        return new ChunkResult(results, errors, failedDocuments, lowConfidenceClassifications);
    }

    private sealed record DocumentOutcome(
        BatchDocumentEntry? Result,
        bool Succeeded,
        FailedDocumentEntry? FailedDocument,
        LowConfidenceClassificationEntry? LowConfidenceEntry);

    private static async Task<DocumentOutcome> ProcessDocSafeAsync(
        TaskOrchestrationContext ctx, LibraryDocument doc, ChunkRequest input, ILogger log)
    {
        try
        {
            var documentTask = ctx.CallSubOrchestratorAsync<EnrichmentResult>(
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

            using var timerCts = new CancellationTokenSource();
            var timeoutTask = ctx.CreateTimer(ctx.CurrentUtcDateTime.Add(DocumentTimeout), timerCts.Token);

            if (await Task.WhenAny(documentTask, timeoutTask) == timeoutTask)
            {
                // The when-any pattern can't cancel an execution that's already in flight, so the
                // sub-orchestration is left to finish (or stay wedged) on its own; the chunk records
                // the document as failed and moves on rather than waiting on it.
                log.LogWarning(
                    "Document {DocId} exceeded {TimeoutMinutes} minutes; abandoning it",
                    doc.Id, DocumentTimeout.TotalMinutes);
                return new DocumentOutcome(null, false, new FailedDocumentEntry(doc.Id, doc.RelativePath), null);
            }

            // Unexpired timers keep the orchestration in Running until they fire.
            timerCts.Cancel();
            var result = await documentTask;

            return new DocumentOutcome(
                new BatchDocumentEntry(
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
                    result.Metadata?.SuggestedFields.ToList() ?? [],
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
            return new DocumentOutcome(null, false, new FailedDocumentEntry(doc.Id, doc.RelativePath), null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error processing document {DocId}", doc.Id);
            return new DocumentOutcome(null, false, new FailedDocumentEntry(doc.Id, doc.RelativePath), null);
        }
    }

    // Internal for testability: null whenever there's nothing worth surfacing to a human reviewer --
    // Agent 1 only populates Candidates when it couldn't confidently settle on one type, and
    // UnrecognizedType is only set when the primary pick itself didn't match the taxonomy.
    internal static LowConfidenceClassificationEntry? BuildLowConfidenceEntry(
        string documentId, string fileName, TypeClassificationResult classification)
    {
        var candidates = classification.Candidates ?? [];
        if (candidates.Count == 0 && classification.UnrecognizedType is null)
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
                .ToList(),
            classification.UnrecognizedType);
    }
}
