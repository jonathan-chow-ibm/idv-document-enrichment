using Azure;
using Azure.AI.DocumentIntelligence;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using PipelineDocumentField = IdvEnrichment.Functions.Models.DocumentField;

namespace IdvEnrichment.Functions.Activities;

public sealed class ExtractContentActivity(
    DocumentIntelligenceClient docIntelClient,
    IHttpClientFactory httpClientFactory,
    ILogger<ExtractContentActivity> logger)
{
    [Function(nameof(ExtractContent))]
    public async Task<ExtractionResult> ExtractContent(
        [ActivityTrigger] ExtractContentInput input,
        CancellationToken ct = default)
    {
        if (SpreadsheetExtractor.IsUnsupportedSpreadsheet(input.FileName))
        {
            logger.LogWarning("Unsupported spreadsheet format for {FileName}; routing to review.", input.FileName);
            return ExtractionResult.UnsupportedFormat(input.FileName);
        }

        if (SpreadsheetExtractor.IsSpreadsheet(input.FileName))
        {
            return await ExtractSpreadsheetAsync(input, ct);
        }

        return await ExtractWithDocumentIntelligenceAsync(input, ct);
    }

    private async Task<ExtractionResult> ExtractSpreadsheetAsync(ExtractContentInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var client = httpClientFactory.CreateClient("spreadsheet");
        using var response = await client.GetAsync(input.DocumentUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? -1;
        using var stream = await response.Content.ReadAsStreamAsync(ct);

        string markdown;
        try
        {
            markdown = SpreadsheetExtractor.ExtractToMarkdown(stream, contentLength);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or System.Xml.XmlException)
        {
            logger.LogWarning(ex, "Failed to parse spreadsheet {FileName}; routing to review.", input.FileName);
            return ExtractionResult.UnsupportedFormat(input.FileName);
        }

        sw.Stop();
        return new ExtractionResult(
            Text: markdown,
            PageCount: 1,
            TextLength: markdown.Length,
            KeyValuePairs: [],
            Language: "unknown",
            ExtractionMethod: "closedxml",
            DurationMs: (int)sw.Elapsed.TotalMilliseconds);
    }

    private async Task<ExtractionResult> ExtractWithDocumentIntelligenceAsync(ExtractContentInput input, CancellationToken ct)
    {
        var options = new AnalyzeDocumentOptions("prebuilt-layout", new Uri(input.DocumentUrl))
        {
            OutputContentFormat = DocumentContentFormat.Markdown,
        };

        var sw = Stopwatch.StartNew();
        var operation = await docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed, options, ct);
        sw.Stop();

        var result = operation.Value;
        var text = result.Content;
        var kvPairs = ExtractKeyValuePairs(result);
        var language = result.Languages?.Count > 0
            ? result.Languages[0].Locale
            : "unknown";

        return new ExtractionResult(
            Text: text,
            PageCount: result.Pages.Count,
            TextLength: text.Length,
            KeyValuePairs: kvPairs,
            Language: language,
            ExtractionMethod: "document-intelligence",
            DurationMs: (int)sw.Elapsed.TotalMilliseconds);
    }

    private static IReadOnlyList<PipelineDocumentField> ExtractKeyValuePairs(AnalyzeResult result)
    {
        if (result.KeyValuePairs is not { Count: > 0 })
        {
            return [];
        }

        return result.KeyValuePairs
            .Where(kv => kv.Key?.Content is not null)
            .Select(kv => new PipelineDocumentField(
                Key: kv.Key.Content,
                Value: kv.Value?.Content ?? string.Empty,
                Confidence: kv.Confidence))
            .ToList();
    }
}
