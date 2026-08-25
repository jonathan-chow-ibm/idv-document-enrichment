using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace IdvEnrichment.Functions.Activities;

public sealed class RouteResultActivity(
    TaxonomyLoader taxonomyLoader,
    TelemetryClient telemetry,
    IOptions<PipelineSettings> settings)
{
    [Function(nameof(RouteResult))]
    public async Task<EnrichmentResult> RouteResult(
        [ActivityTrigger] RouteResultInput input,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        var (decision, lowConfidenceCategories) = DetermineRouting(input.Metadata, taxonomy);

        var result = new EnrichmentResult(
            DocumentId: input.Message.DocumentId,
            FileName: input.Message.FileName,
            Extraction: input.Extraction,
            TypeClassification: input.TypeClassification,
            Metadata: input.Metadata,
            ProcessingMetrics: new ProcessingMetrics(
                ExtractionDurationMs: input.Extraction.DurationMs,
                ClassificationDurationMs: input.TypeClassification.DurationMs,
                MetadataExtractionDurationMs: input.Metadata?.DurationMs ?? 0,
                TotalDurationMs: input.Extraction.DurationMs
                    + input.TypeClassification.DurationMs
                    + (input.Metadata?.DurationMs ?? 0),
                ClassificationInputTokens: input.TypeClassification.InputTokens,
                ClassificationOutputTokens: input.TypeClassification.OutputTokens,
                ExtractionInputTokens: input.Metadata?.InputTokens ?? 0,
                ExtractionOutputTokens: input.Metadata?.OutputTokens ?? 0),
            RoutingDecision: decision,
            LowConfidenceCategories: lowConfidenceCategories,
            DrawingClassification: input.DrawingClassification);

        TrackEnrichmentEvent(input, result);

        return result;
    }

    private void TrackEnrichmentEvent(RouteResultInput input, EnrichmentResult result)
    {
        var properties = new Dictionary<string, string>
        {
            ["documentId"] = input.Message.DocumentId,
            ["fileName"] = input.Message.FileName,
            ["documentType"] = result.TypeClassification.DocumentType.ToString(),
            ["routingDecision"] = result.RoutingDecision.ToString(),
            ["extractionMethod"] = result.Extraction.ExtractionMethod,
            ["source"] = input.Message.Source.ToString(),
            ["batchId"] = input.Message.BatchId ?? "",
        };

        var metrics = new Dictionary<string, double>
        {
            ["typeConfidence"] = result.TypeClassification.Confidence,
            ["pageCount"] = result.Extraction.PageCount,
            ["textLength"] = result.Extraction.TextLength,
        };

        // Per-field confidence scores — adapts to whatever the taxonomy defines
        if (result.Metadata is { } meta)
        {
            foreach (var (field, classification) in meta.Fields)
            {
                metrics[$"field_{field}_confidence"] = classification.Confidence;
            }
        }

        telemetry.TrackEvent("DocumentEnriched", properties, metrics);
    }

    private (RoutingDecision Decision, IReadOnlyList<string> LowConfidenceCategories) DetermineRouting(
        MetadataExtractionResult? metadata,
        TaxonomyData taxonomy)
    {
        if (metadata is null)
        {
            return (RoutingDecision.Review, []);
        }

        var thresholds = taxonomy.Thresholds;
        var universal = taxonomy.UniversalFieldNames();
        var defaultThreshold = thresholds.Agent2Content.TryGetValue("default", out var d)
            ? d
            : settings.Value.ConfidenceThresholdDefault;

        var lowConfidence = new List<string>();
        foreach (var (name, classification) in metadata.Fields)
        {
            var hasValue = !string.IsNullOrWhiteSpace(classification.Value);
            // Sparse fields (non-universal and empty) are "not applicable" to this document — don't force review.
            if (!universal.Contains(name) && !hasValue)
            {
                continue;
            }

            var threshold = thresholds.Agent2Content.TryGetValue(name, out var t) ? t : defaultThreshold;
            if (classification.Confidence < threshold)
            {
                lowConfidence.Add(name);
            }
        }

        var decision = lowConfidence.Count == 0 ? RoutingDecision.Write : RoutingDecision.Review;
        return (decision, lowConfidence);
    }
}
