using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace IdvEnrichment.Functions.Activities;

public sealed class RouteResultActivity(TaxonomyLoader taxonomyLoader, IOptions<PipelineSettings> settings)
{
    [Function(nameof(RouteResult))]
    public async Task<EnrichmentResult> RouteResult(
        [ActivityTrigger] RouteResultInput input,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        var (decision, lowConfidenceCategories) = DetermineRouting(input.Metadata, taxonomy.Thresholds);

        return new EnrichmentResult(
            DocumentId: input.Message.DocumentId,
            FileName: input.Message.FileName,
            Extraction: input.Extraction,
            TypeClassification: input.TypeClassification,
            Metadata: input.Metadata,
            ProcessingMetrics: new ProcessingMetrics(),
            RoutingDecision: decision,
            LowConfidenceCategories: lowConfidenceCategories);
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
