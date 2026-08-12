using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace IdvEnrichment.Functions.Activities;

public sealed class GetTypeConfidenceThresholdActivity(
    TaxonomyLoader taxonomyLoader,
    IOptions<PipelineSettings> settings)
{
    [Function(nameof(GetTypeConfidenceThreshold))]
    public async Task<double> GetTypeConfidenceThreshold(
        [ActivityTrigger] DocumentType documentType,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        // per-type overrides reserved for Phase 3 — currently returns the global Agent 1 threshold
        var threshold = taxonomy.Thresholds.Agent1Classification;
        return threshold > 0 ? threshold : settings.Value.ConfidenceThresholdDefault;
    }
}
