using Azure;
using Azure.AI.DocumentIntelligence;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using PipelineDocumentField = IdvEnrichment.Functions.Models.DocumentField;

namespace IdvEnrichment.Functions.Activities;

public sealed class ExtractContentActivity(DocumentIntelligenceClient docIntelClient)
{
    [Function(nameof(ExtractContent))]
    public async Task<ExtractionResult> ExtractContent(
        [ActivityTrigger] ExtractContentInput input,
        CancellationToken ct = default)
    {
        var options = new AnalyzeDocumentOptions("prebuilt-layout", new Uri(input.DocumentUrl))
        {
            OutputContentFormat = DocumentContentFormat.Markdown,
        };

        var operation = await docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed, options, ct);

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
            Language: language);
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
