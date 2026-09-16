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
using UglyToad.PdfPig.Exceptions;
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
    // Post-open equivalent of DocumentIntelligenceSizeCapBytes, using the REAL page count once the file
    // is open instead of a byte-based estimate — derived from this corpus's measured ~440KB/page density
    // at this size range (50MB / 440KB ≈ 119 pages; see pricing.json bytesPerPageBands).
    private const int DocumentIntelligencePageCap = 120;
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

        if (input.ConvertedToPdf || Path.GetExtension(input.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return await ExtractPdfAsync(input, ct);
            }
            catch (HttpRequestException ex) when (input.ConvertedToPdf && !string.IsNullOrEmpty(input.OriginalUrl))
            {
                // Graph handed back a URL for a converted PDF that the media service then refused to
                // produce — it does that for corrupt Office files and documents carrying embedded OLE
                // objects, and the refusal is only visible once the URL is fetched. Conversion is a cost
                // optimization, not a requirement, so read the original file rather than failing the
                // document. GetDocumentDownloadUrlActivity cannot catch this: by the time the refusal
                // happens, its own fallback is two activities behind us.
                //
                // Only HttpRequestException is caught, which inside ExtractPdfAsync can only come from the
                // download — Document Intelligence surfaces failures as RequestFailedException or
                // TimeoutException, so a DI failure still propagates.
                logger.LogWarning(ex,
                    "Converted PDF for {FileName} could not be retrieved; extracting the original file instead.",
                    input.FileName);

                return await ExtractWithDocumentIntelligenceAsync(
                    input with { DocumentUrl = input.OriginalUrl, ConvertedToPdf = false }, ct);
            }
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
        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            token => client.GetAsync(input.DocumentUrl, token), input.FileName, logger, ct);
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
        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            token => client.GetAsync(input.DocumentUrl, HttpCompletionOption.ResponseHeadersRead, token),
            input.FileName, logger, ct);

        var contentLength = response.Content.Headers.ContentLength ?? -1;

        // ClosedXML's OpenXML repair path (used on malformed .xlsx/.xlsm packages) requires a seekable
        // stream, and HttpClient's own stream is forward-only. Buffer into memory before handing off.
        using var stream = new MemoryStream();
        await response.Content.CopyToAsync(stream, ct);
        stream.Position = 0;

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
        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            token => client.GetAsync(input.DocumentUrl, HttpCompletionOption.ResponseHeadersRead, token),
            input.FileName, logger, ct);

        var contentLength = response.Content.Headers.ContentLength ?? -1;

        var route = DecidePdfRoute(contentLength, input.MaxPages);
        if (route == PdfSizeRoute.TooLargeForProcessing)
        {
            return ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0));
        }

        // MaxPages set + over the local-parsing cap: DI takes the URL directly and only analyzes the
        // requested page range (Pages="1" or "1-N"), so there's no need to download/open the file
        // locally at all.
        if (route == PdfSizeRoute.UseDocumentIntelligence)
        {
            return await ExtractWithDocumentIntelligenceAsync(input, ct);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);

        // TryExtractLocally's own try/catch covers only the PdfPig-specific work (opening the file,
        // walking pages). The DI fallback call below sits outside it deliberately -- DI surfaces its own
        // failures as RequestFailedException/TimeoutException, and a catch-all wrapping this call too
        // would mistake a DI failure for a local-parsing failure and retry DI a second time.
        var localResult = TryExtractLocally(stream, input, contentLength);
        return localResult ?? await ExtractWithDocumentIntelligenceAsync(input, ct);
    }

    // Returns a terminal ExtractionResult when the outcome is decided locally (a successful pdfpig
    // extraction, or a review sentinel such as TooLarge/PasswordProtected), or null when the document
    // should fall back to Document Intelligence.
    private ExtractionResult? TryExtractLocally(Stream stream, ExtractContentInput input, long contentLength)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var document = PdfDocument.Open(stream);

            if (document.NumberOfPages > LocalPdfParsingPageCap && !HasPageLimit(input.MaxPages))
            {
                logger.LogWarning(
                    "{FileName} has {PageCount} pages, exceeding the local parsing cap of {PageCap}.",
                    input.FileName, document.NumberOfPages, LocalPdfParsingPageCap);
                // Already over the 2000-page local cap, which always exceeds the 120-page DI cap too —
                // no DI fallback is reachable here, so this is unconditionally too large.
                return ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0));
            }

            var pageLimit = HasPageLimit(input.MaxPages) ? Math.Min(input.MaxPages!.Value, document.NumberOfPages) : document.NumberOfPages;
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
                return ExceedsDocumentIntelligencePageCap(document.NumberOfPages, input.MaxPages)
                    ? ExtractionResult.TooLargeForProcessing(input.FileName, Math.Max(contentLength, 0))
                    : null; // scanned — fall back to DI
            }

            sw.Stop();
            var text = textBuilder.ToString();
            return new ExtractionResult(
                Text: text,
                PageCount: pageLimit,
                TextLength: text.Length,
                KeyValuePairs: [],
                Language: "unknown",
                ExtractionMethod: "pdfpig",
                DurationMs: (int)sw.Elapsed.TotalMilliseconds);
        }
        catch (PdfDocumentEncryptedException ex)
        {
            // DI cannot open an encrypted PDF either -- route straight to review instead of falling back.
            logger.LogWarning(ex, "{FileName} is password-protected; routing to review.", input.FileName);
            return ExtractionResult.PasswordProtected(input.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Couldn't determine locally (corrupt/malformed PDF) — let DI attempt it instead of failing outright.
            logger.LogWarning(ex, "Failed to parse {FileName} locally with PdfPig; falling back to Document Intelligence.", input.FileName);
            return null;
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

    // Whether maxPages represents an actual page limit. Null, zero, and negative values all mean "no
    // limit" -- a document can't have zero or fewer pages, so those inputs are treated as unbounded
    // rather than as an error. Every place that branches on MaxPages goes through this -- including
    // DocumentOrchestrator's classify-only default -- so "bounded" means the same thing everywhere
    // (size/page caps, the DI Pages parameter, and the classify-only default all agree). Internal
    // rather than private so DocumentOrchestrator can share it instead of duplicating the check.
    internal static bool HasPageLimit(int? maxPages) => maxPages is { } n && n > 0;

    // Given the file's content-length (as reported by response headers, before downloading the body),
    // decide whether it's safe to attempt local PdfPig parsing, or whether it should be routed straight
    // to review without incurring any download or DI cost. Unavailable content-length (-1) is treated
    // conservatively, the same as exceeding the cap, since size can't be verified either way.
    // When maxPages is set, the DI cost/timeout risk these caps guard against doesn't apply — DI is
    // bounded to that page count regardless of the file's total size — so oversized files route to DI
    // instead of giving up.
    internal static PdfSizeRoute DecidePdfRoute(long contentLength, int? maxPages = null) =>
        contentLength >= 0 && contentLength <= LocalPdfParsingSizeCapBytes
            ? PdfSizeRoute.AttemptLocalParsing
            : HasPageLimit(maxPages)
                ? PdfSizeRoute.UseDocumentIntelligence
                : ExceedsDocumentIntelligenceSizeCap(contentLength)
                    ? PdfSizeRoute.TooLargeForProcessing
                    : PdfSizeRoute.UseDocumentIntelligence;

    private static bool ExceedsDocumentIntelligenceSizeCap(long contentLength) =>
        contentLength < 0 || contentLength > DocumentIntelligenceSizeCapBytes;

    // Real-page-count counterpart to ExceedsDocumentIntelligenceSizeCap, used once the file is open and
    // the actual page count is known rather than estimated from bytes.
    internal static bool ExceedsDocumentIntelligencePageCap(int pageCount, int? maxPages) =>
        pageCount > DocumentIntelligencePageCap && !HasPageLimit(maxPages);

    // DI's Pages parameter is only reliable for PDF input -- it errors on Word documents, and "pages"
    // isn't a fixed, well-defined concept for flowing Office formats generally (unlike PDF/TIFF). So
    // MaxPages is only honored for PDFs here; other formats always get a full analysis regardless.
    // convertedToPdf counts as a PDF: the URL handed to DI points at a PDF Graph produced from the Office
    // file, so Pages is safe there even though the original file name still reads .docx/.pptx. Deriving
    // this from the name alone would silently bill a full-document analysis on every converted Office
    // file that reaches DI, which is exactly what MaxPages exists to prevent.
    internal static string? BuildPagesParameter(string fileName, int? maxPages, bool convertedToPdf = false) =>
        HasPageLimit(maxPages) && (convertedToPdf || Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            ? maxPages == 1 ? "1" : $"1-{maxPages}"
            : null;

    private async Task<ExtractionResult> ExtractWithDocumentIntelligenceAsync(ExtractContentInput input, CancellationToken ct)
    {
        var options = new AnalyzeDocumentOptions("prebuilt-layout", new Uri(input.DocumentUrl))
        {
            OutputContentFormat = DocumentContentFormat.Markdown,
            Pages = BuildPagesParameter(input.FileName, input.MaxPages, input.ConvertedToPdf),
        };

        // Bounds the call so a stalled/unresponsive service fails the document — ExtractContent gets no
        // retry (see DocumentOrchestrator), so this surfaces as a failed entry in the batch report — rather
        // than hanging until the host's 10-minute functionTimeout kills the worker mid-activity, which
        // leaves the work item to be redelivered while the billed DI job runs on regardless. Must stay
        // comfortably under both functionTimeout and durableTask.workItemQueueVisibilityTimeout in
        // host.json. Longer than OpenAiRetryHelper's because DI runs legitimately take longer on large
        // scans than an LLM call.
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
