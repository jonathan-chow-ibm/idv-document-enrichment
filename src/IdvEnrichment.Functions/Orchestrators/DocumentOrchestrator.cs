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

        try
        {
            var downloadUrl = await ctx.CallActivityAsync<string>(
                "GetDocumentDownloadUrl",
                new GetDocumentDownloadUrlInput(message.DriveId, message.ItemId, message.FileUrl),
                retry);

            var extraction = await ctx.CallActivityAsync<ExtractionResult>(
                "ExtractContent", new ExtractContentInput(downloadUrl, message.FileName), retry);

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
                    new RecordProcessingResultInput(message.BatchId ?? message.DocumentId, message.DocumentId, "review"),
                    retry);

                return unsupportedResult;
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
            if (!skipExtraction)
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

            var status = enrichmentResult.RoutingDecision == RoutingDecision.Write && enrichmentResult.WriteBackSucceeded
                ? "success"
                : enrichmentResult.WriteBackSucceeded ? "review" : "write-back-failed";
            try
            {
                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.BatchId ?? message.DocumentId, message.DocumentId, status),
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
                    new RecordProcessingResultInput(message.BatchId ?? message.DocumentId, message.DocumentId, "error"),
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
