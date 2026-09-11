using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Orchestrators;

public sealed class DocumentOrchestrator
{
    [Function(nameof(DocumentProcessingOrchestrator))]
    public async Task<EnrichmentResult> DocumentProcessingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var message = ctx.GetInput<QueueMessage>()
            ?? throw new InvalidOperationException("Orchestrator input was null.");

        var log = ctx.CreateReplaySafeLogger<DocumentOrchestrator>();

        var retry = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0));

        // A timeout means DI already ran (or is still running) the analysis and will be billed for it
        // regardless of whether we saw the result — retrying would submit a brand-new billable job on top
        // of one that may still be in progress, tripling cost on the slowest documents for no real chance
        // of success. Genuine transient failures (429s, network blips) still get the normal 3x retry.
        var extractContentRetry = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0)
        {
            HandleFailure = failure => !failure.IsCausedBy<TimeoutException>(),
        });

        try
        {
            var downloadResult = await ctx.CallActivityAsync<DocumentDownloadResult>(
                "GetDocumentDownloadUrl",
                new GetDocumentDownloadUrlInput(message.DriveId, message.ItemId, message.FileUrl, FileName: message.FileName),
                retry);
            var downloadUrl = downloadResult.Url;

            var extraction = await ctx.CallActivityAsync<ExtractionResult>(
                "ExtractContent",
                new ExtractContentInput(downloadUrl, message.FileName, FirstPageOnly: message.ClassifyOnly, ConvertedToPdf: downloadResult.IsConvertedToPdf),
                extractContentRetry);

            if (extraction.IsUnsupported)
            {
                var unsupportedResult = new EnrichmentResult(
                    DocumentId: message.DocumentId,
                    FileName: message.FileName,
                    Extraction: extraction,
                    TypeClassification: new TypeClassificationResult(DocumentType.Other, 0.0, "Unsupported file format"),
                    Metadata: null,
                    ProcessingMetrics: new ProcessingMetrics(),
                    RoutingDecision: RoutingDecision.Review,
                    LowConfidenceCategories: ["format"]);

                await ctx.CallActivityAsync(
                    "WriteMetadata",
                    new WriteMetadataInput(message.SiteId, message.DriveId, message.ItemId, unsupportedResult),
                    retry);

                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.DriveId, message.DocumentId, "review", message.BatchId),
                    retry);

                return unsupportedResult;
            }

            if (extraction.IsTooLarge)
            {
                var tooLargeResult = new EnrichmentResult(
                    DocumentId: message.DocumentId,
                    FileName: message.FileName,
                    Extraction: extraction,
                    TypeClassification: new TypeClassificationResult(DocumentType.Other, 0.0, "File too large for automatic processing"),
                    Metadata: null,
                    ProcessingMetrics: new ProcessingMetrics(),
                    RoutingDecision: RoutingDecision.Review,
                    LowConfidenceCategories: ["size"]);

                await ctx.CallActivityAsync(
                    "WriteMetadata",
                    new WriteMetadataInput(message.SiteId, message.DriveId, message.ItemId, tooLargeResult),
                    retry);

                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.DriveId, message.DocumentId, "review", message.BatchId),
                    retry);

                return tooLargeResult;
            }

            var typeClassification = await ctx.CallActivityAsync<TypeClassificationResult>(
                "ClassifyType",
                new ClassifyTypeInput(message.DocumentId, message.FileName, extraction.Text, extraction.KeyValuePairs),
                retry);

            var typeThreshold = await ctx.CallActivityAsync<double>(
                "GetTypeConfidenceThreshold", typeClassification.DocumentType, retry);

            var skipExtraction = typeClassification.Confidence < typeThreshold
                || typeClassification.DocumentType == DocumentType.Other;

            DrawingClassification? drawingClassification = null;
            var shouldClassifyDrawing =
                typeClassification.DocumentType == DocumentType.DesignDrawing
                || typeClassification.DocumentType == DocumentType.Plat
                || typeClassification.DocumentType == DocumentType.Survey
                || (extraction.TextLength < 500
                    && message.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    && typeClassification.DocumentType == DocumentType.Other);

            if (shouldClassifyDrawing)
            {
                try
                {
                    drawingClassification = await ctx.CallActivityAsync<DrawingClassification>(
                        "ExtractDrawingDetails",
                        new ExtractDrawingDetailsInput(downloadUrl, message.FileName, extraction.Text),
                        retry);

                    // Vision sees the title block, seals and dedication blocks directly, so it is
                    // better placed than Agent 1 (scrambled OCR) to tell Plat / Survey / Design Drawing
                    // apart. "Not a Drawing" maps to null so the override is skipped entirely.
                    DocumentType? visionType = drawingClassification.DrawingType switch
                    {
                        "Plat" => DocumentType.Plat,
                        "Survey" => DocumentType.Survey,
                        "Design Drawing" => DocumentType.DesignDrawing,
                        _ => null,
                    };

                    var agent1IsDrawingFamily = typeClassification.DocumentType
                        is DocumentType.DesignDrawing or DocumentType.Plat or DocumentType.Survey;

                    // Fill-in: Agent 1 had no answer — low bar, anything beats Other.
                    var fillIn = typeClassification.DocumentType == DocumentType.Other
                                 && drawingClassification.Confidence > 0.7;

                    // Correction: Agent 1 had an answer within the drawing family and vision
                    // disagrees — higher bar to overturn a considered classification.
                    var correction = agent1IsDrawingFamily
                                     && visionType != typeClassification.DocumentType
                                     && drawingClassification.Confidence > 0.8;

                    if (visionType is { } resolvedType
                        && (fillIn || correction)
                        && !string.IsNullOrEmpty(drawingClassification.Discipline))
                    {
                        typeClassification = new TypeClassificationResult(
                            resolvedType,
                            drawingClassification.Confidence,
                            $"Vision override: {drawingClassification.Reasoning}",
                            typeClassification.InputTokens,
                            typeClassification.OutputTokens,
                            typeClassification.DurationMs);
                    }
                }
                catch (TaskFailedException ex)
                {
                    log.LogWarning(ex, "DrawingDetails failed for {DocumentId} — continuing without vision enrichment", message.DocumentId);
                }
            }

            MetadataExtractionResult? metadata = null;
            if (!skipExtraction && !message.ClassifyOnly)
            {
                metadata = await ctx.CallActivityAsync<MetadataExtractionResult>(
                    "ExtractMetadata",
                    new ExtractMetadataInput(
                        message.DocumentId,
                        message.FileName,
                        typeClassification.DocumentType,
                        extraction.Text,
                        extraction.KeyValuePairs));
            }

            var enrichmentResult = await ctx.CallActivityAsync<EnrichmentResult>(
                "RouteResult",
                new RouteResultInput(message, typeClassification, metadata, extraction, drawingClassification),
                retry);

            try
            {
                await ctx.CallActivityAsync(
                    "WriteMetadata",
                    new WriteMetadataInput(message.SiteId, message.DriveId, message.ItemId, enrichmentResult),
                    retry);
            }
            catch (TaskFailedException ex)
            {
                log.LogError(ex, "WriteMetadata failed for {DocumentId} — metadata not written to SharePoint", message.DocumentId);
                enrichmentResult = enrichmentResult with { WriteBackSucceeded = false };
            }

            // Classify-only runs never resolve to "success"/"review" — those are treated as durably
            // terminal by FilterProcessed's cross-batch skip logic, and a test run must never block a
            // real one from processing these documents later.
            var status = !enrichmentResult.WriteBackSucceeded
                ? "write-back-failed"
                : message.ClassifyOnly
                    ? "classified-only"
                    : enrichmentResult.RoutingDecision == RoutingDecision.Write ? "success" : "review";
            try
            {
                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.DriveId, message.DocumentId, status, message.BatchId),
                    retry);
            }
            catch (TaskFailedException ex)
            {
                log.LogError(ex, "RecordProcessingResult failed for {DocumentId}", message.DocumentId);
            }

            log.LogInformation("Orchestration complete for document {DocumentId}: {Status}",
                message.DocumentId, status);

            return enrichmentResult;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Orchestration failed for document {DocumentId}", message.DocumentId);

            try
            {
                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.DriveId, message.DocumentId, "error", message.BatchId),
                    retry);
            }
            catch (Exception recordEx)
            {
                log.LogError(recordEx, "Failed to record error status for document {DocumentId}", message.DocumentId);
            }

            throw;
        }
    }
}
