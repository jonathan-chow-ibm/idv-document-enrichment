using Azure;
using Azure.AI.DocumentIntelligence;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PipelineDocumentField = IdvEnrichment.Functions.Models.DocumentField;

namespace IdvEnrichment.Functions.Activities;

// Outcome of the pre-download size check for a PDF, based on its reported content-length.
internal enum PdfSizeRoute
{
    AttemptLocalParsing,
    UseDocumentIntelligence,
    TooLargeForProcessing,
}

public sealed class ExtractContentActivity(
    DocumentIntelligenceClient docIntelClient,
    IHttpClientFactory httpClientFactory,
    ILogger<ExtractContentActivity> logger)
{
    private static readonly TimeSpan DocumentIntelligenceTimeout = TimeSpan.FromMinutes(5);

    // Safety cap for even ATTEMPTING local PdfPig parsing — above this, opening the file in memory is a
    // risk in itself, regardless of whether it turns out to be born-digital or scanned.
    private const long LocalPdfParsingSizeCapBytes = 250 * 1024 * 1024; // 250 MB
    // Secondary safety check once PdfPig has opened the file and can report NumberOfPages cheaply.
    private const int LocalPdfParsingPageCap = 2000;
    // Only relevant for documents that turn out to be SCANNED; above this, DI is skipped and the doc
    // routes to Review instead, since a DI call would risk both cost and the host functionTimeout.
    private const long DocumentIntelligenceSizeCapBytes = 50 * 1024 * 1024; // 50 MB
    // An embedded image covering more than this fraction of the page area is treated as a full-page scan image.
    private const double FullPageImageAreaRatio = 0.8;

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

        if (Path.GetExtension(input.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ExtractPdfAsync(input, ct);
        }

        if (Path.GetExtension(input.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return await ExtractPlainTextAsync(input, ct);
        }

        return await ExtractWithDocumentIntelligenceAsync(input, ct);
    }

    private async Task<ExtractionResult> ExtractPlainTextAsync(ExtractContentInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var client = httpClientFactory.CreateClient("spreadsheet");
        using var response = await client.GetAsync(input.DocumentUrl, ct);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();

        return new ExtractionResult(
            Text: text,
            PageCount: 1,
            TextLength: text.Length,
            KeyValuePairs: [],
            Language: "unknown",
            ExtractionMethod: "plaintext",
            DurationMs: (int)sw.Elapsed.TotalMilliseconds);
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

    private async Task<ExtractionResult> ExtractPdfAsync(ExtractContentInput input, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("spreadsheet");
        using var response = await client.GetAsync(input.DocumentUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? -1;

        if (DecidePdfRoute(contentLength) == PdfSizeRoute.TooLargeForProcessing)
        {
            return ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0));
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);

        var sw = Stopwatch.StartNew();
        try
        {
            using var document = PdfDocument.Open(stream);

            if (document.NumberOfPages > LocalPdfParsingPageCap && !input.FirstPageOnly)
            {
                logger.LogWarning(
                    "{FileName} has {PageCount} pages, exceeding the local parsing cap of {PageCap}.",
                    input.FileName, document.NumberOfPages, LocalPdfParsingPageCap);
                return ExceedsDocumentIntelligenceSizeCap(contentLength)
                    ? ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0))
                    : await ExtractWithDocumentIntelligenceAsync(input, ct);
            }

            var pageLimit = input.FirstPageOnly ? 1 : document.NumberOfPages;
            var pages = new List<PageTextInfo>(pageLimit);
            var textBuilder = new StringBuilder();
            foreach (var page in document.GetPages().Take(pageLimit))
            {
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append("\n\n");
                }
                textBuilder.Append(page.Text);
                pages.Add(new PageTextInfo(page.Text.Length, HasFullPageImage(page)));
            }

            if (!PdfDigitalDetector.IsBornDigital(pages))
            {
                return ExceedsDocumentIntelligenceSizeCap(contentLength)
                    ? ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0))
                    : await ExtractWithDocumentIntelligenceAsync(input, ct);
            }

            sw.Stop();
            var text = textBuilder.ToString();
            return new ExtractionResult(
                Text: text,
                PageCount: document.NumberOfPages,
                TextLength: text.Length,
                KeyValuePairs: [],
                Language: "unknown",
                ExtractionMethod: "pdfpig",
                DurationMs: (int)sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Couldn't determine locally (corrupt/malformed PDF) — let DI attempt it instead of failing outright.
            logger.LogWarning(ex, "Failed to parse {FileName} locally with PdfPig; falling back to Document Intelligence.", input.FileName);
            return await ExtractWithDocumentIntelligenceAsync(input, ct);
        }
    }

    private static bool HasFullPageImage(Page page)
    {
        var pageArea = page.Width * page.Height;
        if (pageArea <= 0)
        {
            return false;
        }

        return page.GetImages().Any(image => (image.BoundingBox.Width * image.BoundingBox.Height) / pageArea > FullPageImageAreaRatio);
    }

    // Given the file's content-length (as reported by response headers, before downloading the body),
    // decide whether it's safe to attempt local PdfPig parsing, or whether it should be routed straight
    // to review without incurring any download or DI cost. Unavailable content-length (-1) is treated
    // conservatively, the same as exceeding the cap, since size can't be verified either way.
    internal static PdfSizeRoute DecidePdfRoute(long contentLength) =>
        contentLength >= 0 && contentLength <= LocalPdfParsingSizeCapBytes
            ? PdfSizeRoute.AttemptLocalParsing
            : ExceedsDocumentIntelligenceSizeCap(contentLength)
                ? PdfSizeRoute.TooLargeForProcessing
                : PdfSizeRoute.UseDocumentIntelligence;

    private static bool ExceedsDocumentIntelligenceSizeCap(long contentLength) =>
        contentLength < 0 || contentLength > DocumentIntelligenceSizeCapBytes;

    private async Task<ExtractionResult> ExtractWithDocumentIntelligenceAsync(ExtractContentInput input, CancellationToken ct)
    {
        var options = new AnalyzeDocumentOptions("prebuilt-layout", new Uri(input.DocumentUrl))
        {
            OutputContentFormat = DocumentContentFormat.Markdown,
            Pages = input.FirstPageOnly ? "1" : null,
        };

        // Bounds the call so a stalled/unresponsive service is treated as a failure (triggering the
        // orchestrator's existing retry policy) instead of hanging indefinitely — DI runs can legitimately
        // take longer than an LLM call on large scans, hence the longer timeout than OpenAiRetryHelper's.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DocumentIntelligenceTimeout);

        var sw = Stopwatch.StartNew();
        Operation<AnalyzeResult> operation;
        try
        {
            operation = await docIntelClient.AnalyzeDocumentAsync(
                WaitUntil.Completed, options, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Document Intelligence analysis did not complete within {DocumentIntelligenceTimeout.TotalMinutes} minutes for {input.FileName}.");
        }
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
