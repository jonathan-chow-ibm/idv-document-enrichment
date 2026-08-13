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
                "GetDocumentDownloadUrl", message, retry);

            var extraction = await ctx.CallActivityAsync<ExtractionResult>(
                "ExtractContent", new ExtractContentInput(downloadUrl), retry);

            var typeClassification = await ctx.CallActivityAsync<TypeClassificationResult>(
                "ClassifyType",
                new ClassifyTypeInput(message.DocumentId, message.FileName, extraction.Text, extraction.KeyValuePairs),
                retry);

            var typeThreshold = await ctx.CallActivityAsync<double>(
                "GetTypeConfidenceThreshold", typeClassification.DocumentType, retry);

            var skipExtraction = typeClassification.Confidence < typeThreshold
                || typeClassification.DocumentType == DocumentType.Other;

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
                        extraction.KeyValuePairs),
                    retry);
            }

            var enrichmentResult = await ctx.CallActivityAsync<EnrichmentResult>(
                "RouteResult",
                new RouteResultInput(message, typeClassification, metadata, extraction),
                retry);

            await ctx.CallActivityAsync(
                "WriteMetadata",
                new WriteMetadataInput(message.SiteId, message.DriveId, message.ItemId, enrichmentResult),
                retry);

            var status = enrichmentResult.RoutingDecision == RoutingDecision.Write ? "success" : "review";
            await ctx.CallActivityAsync(
                "RecordProcessingResult",
                new RecordProcessingResultInput(message.BatchId ?? message.DocumentId, message.DocumentId, status),
                retry);

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
