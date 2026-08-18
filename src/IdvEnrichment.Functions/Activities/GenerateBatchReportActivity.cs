using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;

namespace IdvEnrichment.Functions.Activities;

public sealed class GenerateBatchReportActivity
{
    [Function(nameof(GenerateBatchReport))]
    public Task<BatchReport> GenerateBatchReport(
        [ActivityTrigger] GenerateBatchReportInput input,
        CancellationToken ct = default)
    {
        var classified = input.Results.Count(r => r.RoutingDecision == RoutingDecision.Write);
        var underReview = input.Results.Count(r => r.RoutingDecision == RoutingDecision.Review);

        var typeCounts = input.Results
            .GroupBy(r => r.DocumentType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var report = new BatchReport(
            BatchId: input.BatchId,
            Url: input.Url,
            StartedAt: input.StartedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            Summary: new BatchSummary(
                TotalDocuments: input.Results.Count + input.Errors,
                Classified: classified,
                UnderReview: underReview,
                Errors: input.Errors,
                Skipped: 0),
            ConfidenceDistribution: new ConfidenceDistribution(High: 0, Medium: 0, Low: 0),
            ErrorsByStep: new Dictionary<ProcessingStep, int>(),
            DocumentTypeCounts: typeCounts,
            Cost: new BatchCost(
                DocumentIntelligence: 0m,
                ClassificationTokens: new TokenUsage(0, 0),
                ExtractionTokens: new TokenUsage(0, 0),
                EstimatedTotalUsd: 0m));

        return Task.FromResult(report);
    }
}
