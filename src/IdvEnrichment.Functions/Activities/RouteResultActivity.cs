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
        var (decision, lowConfidenceCategories) = DetermineRouting(input.Metadata, taxonomy.Thresholds);

        var result = new EnrichmentResult(
            DocumentId: input.Message.DocumentId,
            FileName: input.Message.FileName,
            Extraction: input.Extraction,
            TypeClassification: input.TypeClassification,
            Metadata: input.Metadata,
            ProcessingMetrics: new ProcessingMetrics(),
            RoutingDecision: decision,
            LowConfidenceCategories: lowConfidenceCategories);

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

        // Common category confidence scores
        if (result.Metadata is { } meta)
        {
            metrics["dealTypeConfidence"] = meta.DealType.Confidence;
            metrics["submarketConfidence"] = meta.Submarket.Confidence;
            metrics["counterpartyConfidence"] = meta.Counterparty.Confidence;
            metrics["confidentialityConfidence"] = meta.Confidentiality.Confidence;

            // Type-specific field confidence — adapts to whatever taxonomy defines
            foreach (var (field, classification) in meta.TypeSpecificFields)
            {
                metrics[$"field_{field}_confidence"] = classification.Confidence;
            }
        }

        telemetry.TrackEvent("DocumentEnriched", properties, metrics);
    }

    private (RoutingDecision Decision, IReadOnlyList<string> LowConfidenceCategories) DetermineRouting(
        MetadataExtractionResult? metadata,
        Models.ConfidenceThresholds thresholds)
    {
        if (metadata is null)
        {
            return (RoutingDecision.Review, []);
        }

        var lowConfidence = new List<string>();
        var commonFields = new (string Name, CategoryClassification Classification)[]
        {
            ("dealType", metadata.DealType),
            ("submarket", metadata.Submarket),
            ("counterparty", metadata.Counterparty),
            ("confidentiality", metadata.Confidentiality),
        };

        foreach (var (name, classification) in commonFields)
        {
            var threshold = thresholds.Agent2Common.TryGetValue(name, out var t) ? t : settings.Value.ConfidenceThresholdDefault;
            if (classification.Confidence < threshold)
            {
                lowConfidence.Add(name);
            }
        }

        var decision = lowConfidence.Count == 0 ? RoutingDecision.Write : RoutingDecision.Review;
        return (decision, lowConfidence);
    }
}
