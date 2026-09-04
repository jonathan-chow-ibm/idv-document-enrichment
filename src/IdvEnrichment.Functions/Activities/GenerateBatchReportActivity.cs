using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Activities;

public sealed class GenerateBatchReportActivity(
    BlobContainerClient? reportContainer,
    TaxonomyLoader taxonomyLoader,
    ILogger<GenerateBatchReportActivity> logger)
{
    private const double HighThreshold = 0.85;
    private const double MediumThreshold = 0.70;

    [Function(nameof(GenerateBatchReport))]
    public async Task<BatchReport> GenerateBatchReport(
        [ActivityTrigger] GenerateBatchReportInput input,
        CancellationToken ct = default)
    {
        var classified = input.Results.Count(r => r.RoutingDecision == RoutingDecision.Write && r.WriteBackSucceeded);
        var underReview = input.Results.Count(r => r.RoutingDecision == RoutingDecision.Review && r.WriteBackSucceeded);
        var writeBackFailed = input.Results.Count(r => !r.WriteBackSucceeded);

        var high = input.Results.Count(r => r.TypeConfidence >= HighThreshold);
        var medium = input.Results.Count(r => r.TypeConfidence >= MediumThreshold && r.TypeConfidence < HighThreshold);
        var low = input.Results.Count(r => r.TypeConfidence < MediumThreshold);

        var typeCounts = input.Results
            .GroupBy(r => JsonSerializer.Serialize(r.DocumentType).Trim('"'))
            .ToDictionary(g => g.Key, g => g.Count());

        var taxonomy = await taxonomyLoader.LoadAsync(ct);

        var suggestedFieldsByGroup = input.Results
            .Where(r => r.SuggestedFieldKeys != null && r.SuggestedFieldKeys.Count > 0)
            .GroupBy(r => taxonomy.GetGroupForDocumentType(r.DocumentType) ?? "Other")
            .OrderBy(g => g.Key)
            .Select(grouping => new SuggestedFieldsByGroup(
                Group: grouping.Key,
                ByDocumentType: grouping
                    .GroupBy(r => JsonSerializer.Serialize(r.DocumentType).Trim('"'))
                    .OrderByDescending(g => g.Count())
                    .Select(dtGrouping => new SuggestedFieldsByDocumentType(
                        DocumentType: dtGrouping.Key,
                        DocumentCount: dtGrouping.Count(),
                        TopFields: dtGrouping
                            .SelectMany(r => r.SuggestedFieldKeys)
                            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                            .OrderByDescending(g => g.Count())
                            .Take(10)
                            .Select(g => new SuggestedFieldEntry(g.Key, g.Count(), []))
                            .ToList()))
                    .ToList()))
            .ToList();

        var classificationInputTokens = input.Results.Sum(r => (long)r.ClassificationInputTokens);
        var classificationOutputTokens = input.Results.Sum(r => (long)r.ClassificationOutputTokens);
        var extractionInputTokens = input.Results.Sum(r => (long)r.ExtractionInputTokens);
        var extractionOutputTokens = input.Results.Sum(r => (long)r.ExtractionOutputTokens);
        var visionInputTokens = input.Results.Sum(r => (long)r.VisionInputTokens);
        var visionOutputTokens = input.Results.Sum(r => (long)r.VisionOutputTokens);

        var report = new BatchReport(
            BatchId: input.BatchId,
            Url: input.Url,
            StartedAt: input.StartedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            Summary: new BatchSummary(
                TotalDocuments: input.Results.Count + input.Errors,
                Classified: classified,
                UnderReview: underReview,
                WriteBackFailed: writeBackFailed,
                Errors: input.Errors,
                Skipped: 0),
            ConfidenceDistribution: new ConfidenceDistribution(High: high, Medium: medium, Low: low),
            DocumentTypeCounts: typeCounts,
            SuggestedFieldsByGroup: suggestedFieldsByGroup,
            Cost: new BatchCost(
                DocumentIntelligence: 0m,
                ClassificationTokens: new TokenUsage(classificationInputTokens, classificationOutputTokens),
                ExtractionTokens: new TokenUsage(extractionInputTokens, extractionOutputTokens),
                VisionTokens: new TokenUsage(visionInputTokens, visionOutputTokens),
                EstimatedTotalUsd: 0m),
            FailedDocuments: input.FailedDocuments);

        if (reportContainer is not null)
        {
            await WriteReportsAsync(report, input, ct);
        }
        else
        {
            logger.LogWarning("BatchReportsContainerUrl is not configured — batch report not persisted to blob storage.");
        }

        return report;
    }

    private async Task WriteReportsAsync(BatchReport report, GenerateBatchReportInput input, CancellationToken ct)
    {
        await reportContainer!.CreateIfNotExistsAsync(cancellationToken: ct);

        var prefix = $"{report.StartedAt:yyyy-MM-dd}/{report.BatchId}";

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true });
        var jsonBlob = reportContainer.GetBlobClient($"{prefix}/report.json");
        await jsonBlob.UploadAsync(new BinaryData(jsonBytes), new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } }, ct);

        var html = BuildHtmlReport(report, input);
        var htmlBytes = Encoding.UTF8.GetBytes(html);
        var htmlBlob = reportContainer.GetBlobClient($"{prefix}/report.html");
        await htmlBlob.UploadAsync(new BinaryData(htmlBytes), new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "text/html; charset=utf-8" } }, ct);

        logger.LogInformation("Batch report written to {Prefix}", prefix);
    }

    private static string BuildHtmlReport(BatchReport r, GenerateBatchReportInput input)
    {
        var duration = r.CompletedAt - r.StartedAt;
        var total = r.Summary.TotalDocuments;
        var processed = r.Summary.Classified + r.Summary.UnderReview;
        var reviewRate = total > 0 ? r.Summary.UnderReview * 100.0 / total : 0;
        var errorRate = total > 0 ? r.Summary.Errors * 100.0 / total : 0;
        // Percentages against processed (not total) so bar fills to 100% regardless of error rate
        var highPct = processed > 0 ? r.ConfidenceDistribution.High * 100.0 / processed : 0;
        var medPct = processed > 0 ? r.ConfidenceDistribution.Medium * 100.0 / processed : 0;
        var lowPct = processed > 0 ? r.ConfidenceDistribution.Low * 100.0 / processed : 0;

        var safeUrl = System.Net.WebUtility.HtmlEncode(input.Url);
        var safeBatchId = System.Net.WebUtility.HtmlEncode(r.BatchId);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Batch Tagging Report \u2014 {r.BatchId}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:40px auto;color:#222}");
        sb.AppendLine("h1{color:#0078d4}h2{color:#444;border-bottom:1px solid #ddd;padding-bottom:6px}");
        sb.AppendLine(".meta{color:#666;font-size:.9em;margin-bottom:24px}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(4,1fr);gap:16px;margin:20px 0}");
        sb.AppendLine(".card{background:#f4f4f4;border-radius:8px;padding:16px;text-align:center}");
        sb.AppendLine(".card .num{font-size:2em;font-weight:bold;color:#0078d4}.card .lbl{font-size:.85em;color:#666;margin-top:4px}");
        sb.AppendLine(".bar{display:flex;height:28px;border-radius:4px;overflow:hidden;margin:8px 0}");
        sb.AppendLine(".bar-high{background:#107c10}.bar-med{background:#ffaa44}.bar-low{background:#d13438}");
        sb.AppendLine("table{width:100%;border-collapse:collapse}th{background:#0078d4;color:#fff;padding:8px 12px;text-align:left}");
        sb.AppendLine("td{padding:8px 12px;border-bottom:1px solid #eee}tr:hover td{background:#f9f9f9}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>Batch Tagging Results Report</h1>");
        sb.AppendLine($"<div class=\"meta\">Batch ID: <strong>{safeBatchId}</strong> &nbsp;|&nbsp; Started: <strong>{r.StartedAt:yyyy-MM-dd HH:mm} UTC</strong> &nbsp;|&nbsp; Duration: <strong>{duration.TotalHours:F1}h</strong><br>Source: {safeUrl}</div>");

        sb.AppendLine("<h2>Summary</h2><div class=\"grid\">");
        sb.AppendLine($"<div class=\"card\"><div class=\"num\">{r.Summary.TotalDocuments:N0}</div><div class=\"lbl\">Total Documents</div></div>");
        sb.AppendLine($"<div class=\"card\"><div class=\"num\" style=\"color:#107c10\">{r.Summary.Classified:N0}</div><div class=\"lbl\">Auto-Classified</div></div>");
        sb.AppendLine($"<div class=\"card\"><div class=\"num\" style=\"color:#ffaa44\">{r.Summary.UnderReview:N0}</div><div class=\"lbl\">Under Review ({reviewRate:F1}%)</div></div>");
        sb.AppendLine($"<div class=\"card\"><div class=\"num\" style=\"color:#d13438\">{r.Summary.WriteBackFailed:N0}</div><div class=\"lbl\">Write-Back Failed</div></div>");
        sb.AppendLine($"<div class=\"card\"><div class=\"num\" style=\"color:#d13438\">{r.Summary.Errors:N0}</div><div class=\"lbl\">Errors ({errorRate:F1}%)</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Confidence Distribution</h2>");
        sb.AppendLine($"<p>High (&ge;85%): <strong>{r.ConfidenceDistribution.High:N0}</strong> &nbsp; Medium (70\u201385%): <strong>{r.ConfidenceDistribution.Medium:N0}</strong> &nbsp; Low (&lt;70%): <strong>{r.ConfidenceDistribution.Low:N0}</strong></p>");
        sb.AppendLine($"<div class=\"bar\"><div class=\"bar-high\" style=\"width:{highPct:F1}%\" title=\"High\"></div><div class=\"bar-med\" style=\"width:{medPct:F1}%\" title=\"Medium\"></div><div class=\"bar-low\" style=\"width:{lowPct:F1}%\" title=\"Low\"></div></div>");

        sb.AppendLine("<h2>Document Type Breakdown</h2><table><thead><tr><th>Document Type</th><th>Count</th><th>% of Total</th></tr></thead><tbody>");
        foreach (var (type, count) in r.DocumentTypeCounts.OrderByDescending(x => x.Value))
        {
            var pct = total > 0 ? count * 100.0 / total : 0;
            sb.AppendLine($"<tr><td>{type}</td><td>{count:N0}</td><td>{pct:F1}%</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        if (r.SuggestedFieldsByGroup.Count > 0)
        {
            sb.AppendLine("<h2>Suggested Fields by Document Type</h2>");
            sb.AppendLine("<p>Fields the AI discovered that are not in the current taxonomy. High-frequency fields are candidates for taxonomy additions.</p>");
            foreach (var group in r.SuggestedFieldsByGroup)
            {
                if (group.ByDocumentType.Count == 0)
                {
                    continue;
                }

                var safeGroup = System.Net.WebUtility.HtmlEncode(group.Group);
                sb.AppendLine($"<h3>{safeGroup}</h3>");
                sb.AppendLine("<table><thead><tr><th>Document Type</th><th>Docs with Suggestions</th><th>Top Suggested Fields</th></tr></thead><tbody>");
                foreach (var dt in group.ByDocumentType)
                {
                    var safeDt = System.Net.WebUtility.HtmlEncode(dt.DocumentType);
                    var fields = string.Join(", ", dt.TopFields.Select(f =>
                        $"{System.Net.WebUtility.HtmlEncode(f.Key)} ({f.DocumentCount:N0})"));
                    sb.AppendLine($"<tr><td>{safeDt}</td><td>{dt.DocumentCount:N0}</td><td>{fields}</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
            }
        }


        sb.AppendLine($"<p style=\"color:#999;font-size:.8em;margin-top:32px\">Generated {r.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
