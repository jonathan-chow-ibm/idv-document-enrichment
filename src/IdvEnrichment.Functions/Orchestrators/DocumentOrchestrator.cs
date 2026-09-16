using IdvEnrichment.Functions.Activities;
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
            var downloadResult = await ctx.CallActivityAsync<DocumentDownloadResult>(
                "GetDocumentDownloadUrl",
                new GetDocumentDownloadUrlInput(message.DriveId, message.ItemId, message.FileUrl, FileName: message.FileName),
                retry);
            var downloadUrl = downloadResult.Url;

            // ExtractContent gets NO retry, deliberately. It submits a billable Document Intelligence job,
            // so a whole-activity retry can resubmit work already accepted — a network error while polling
            // an in-flight analysis is not a TimeoutException, so no predicate separates "already billed"
            // from "not billed" reliably. (The RetryPolicy.HandleFailure predicate that used to sit here
            // produced no retries at all: an ExtractContent failure completed in 2s against the ~15s floor
            // for 3 attempts.) DI's own transient failures are handled by the Azure SDK's retry pipeline,
            // which retries individual requests — including each status poll — rather than resubmitting the
            // operation. Transient DOWNLOAD failures retry inside the activity, where DI is never involved;
            // see DownloadRetryHelper.
            var extraction = await ctx.CallActivityAsync<ExtractionResult>(
                "ExtractContent",
                new ExtractContentInput(
                    downloadUrl,
                    message.FileName,
                    MaxPages: ResolveMaxPages(message.MaxPages, message.ClassifyOnly),
                    ConvertedToPdf: downloadResult.IsConvertedToPdf,
                    OriginalUrl: downloadResult.OriginalUrl));

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

            if (extraction.IsPasswordProtected)
            {
                var passwordProtectedResult = new EnrichmentResult(
                    DocumentId: message.DocumentId,
                    FileName: message.FileName,
                    Extraction: extraction,
                    TypeClassification: new TypeClassificationResult(DocumentType.Other, 0.0, "Password-protected file cannot be processed automatically"),
                    Metadata: null,
                    ProcessingMetrics: new ProcessingMetrics(),
                    RoutingDecision: RoutingDecision.Review,
                    LowConfidenceCategories: ["password"]);

                await ctx.CallActivityAsync(
                    "WriteMetadata",
                    new WriteMetadataInput(message.SiteId, message.DriveId, message.ItemId, passwordProtectedResult),
                    retry);

                await ctx.CallActivityAsync(
                    "RecordProcessingResult",
                    new RecordProcessingResultInput(message.DriveId, message.DocumentId, "review", message.BatchId),
                    retry);

                return passwordProtectedResult;
            }

            // No Durable-level retry: ClassifyType is pure OpenAI work already retried internally by
            // OpenAiRetryHelper (429s with Retry-After backoff, up to 90s/attempt). Wrapping it in another
            // 3-attempt Durable retry compounded worst-case latency to ~20 minutes for no added protection.
            var typeClassification = await ctx.CallActivityAsync<TypeClassificationResult>(
                "ClassifyType",
                new ClassifyTypeInput(message.DocumentId, message.FileName, extraction.Text, extraction.KeyValuePairs));

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
                    // No Durable-level retry, for the same reason as ClassifyType above: the HTTP download
                    // now goes through DownloadRetryHelper and the OpenAI call through OpenAiRetryHelper, so
                    // a third, compounding retry layer here only added latency without added protection.
                    drawingClassification = await ctx.CallActivityAsync<DrawingClassification>(
                        "ExtractDrawingDetails",
                        new ExtractDrawingDetailsInput(downloadUrl, message.FileName, extraction.Text));

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

            // Uses the (possibly vision-corrected) type resolved above, so a vision override into or
            // out of an extraction-excluded type is respected here.
            var extractionEnabledForType = await ctx.CallActivityAsync<bool>(
                "GetTypeExtractionPolicy", typeClassification.DocumentType, retry);

            var extractionExcludedByType = IsExcludedByTypeConfig(
                skipExtraction, message.ClassifyOnly, extractionEnabledForType);

            if (extractionExcludedByType)
            {
                skipExtraction = true;
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
                new RouteResultInput(message, typeClassification, metadata, extraction, drawingClassification, extractionExcludedByType),
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

    // Resolves the MaxPages passed to ExtractContent: an explicit page limit from the queue message
    // wins outright; otherwise a classify-only run still gets the 1-page cost optimization. Goes through
    // HasPageLimit -- rather than a bare "message.MaxPages ?? ..." -- so a zero or negative override is
    // treated the same as "unset" here as it is everywhere else MaxPages is checked; a plain ?? would let
    // an explicit MaxPages: 0 slip past the classify-only default and trigger a full-document extraction.
    internal static int? ResolveMaxPages(int? requestedMaxPages, bool classifyOnly) =>
        ExtractContentActivity.HasPageLimit(requestedMaxPages) ? requestedMaxPages : (classifyOnly ? 1 : null);

    // The type-config exclusion is deliberately the weakest signal: it never overrides an existing skip
    // reason (low confidence, DocumentType.Other), and it never fires for a classify-only run — those
    // must stay non-terminal (see the "status" comment below) regardless of the type's extraction policy.
    internal static bool IsExcludedByTypeConfig(bool skipExtraction, bool classifyOnly, bool extractionEnabledForType) =>
        !skipExtraction && !classifyOnly && !extractionEnabledForType;
}
