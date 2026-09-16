using Azure;
using Azure.AI.DocumentIntelligence;
using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ExtractContentActivityTests
{
    [Fact]
    public void DecidePdfRoute_SmallFile_AttemptsLocalParsing()
    {
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: 1024);

        Assert.Equal(PdfSizeRoute.AttemptLocalParsing, route);
    }

    [Fact]
    public void DecidePdfRoute_AtLocalParsingCap_AttemptsLocalParsing()
    {
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: 250 * 1024 * 1024);

        Assert.Equal(PdfSizeRoute.AttemptLocalParsing, route);
    }

    [Fact]
    public void DecidePdfRoute_OverLocalParsingCap_IsTooLarge()
    {
        // Local cap (250 MB) is higher than the DI cap (50 MB), so anything over it is too large for either path.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: (250 * 1024 * 1024) + 1);

        Assert.Equal(PdfSizeRoute.TooLargeForProcessing, route);
    }

    [Fact]
    public void DecidePdfRoute_UnavailableContentLength_IsTooLarge()
    {
        // -1 (unavailable) is treated conservatively, the same as exceeding the cap.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: -1);

        Assert.Equal(PdfSizeRoute.TooLargeForProcessing, route);
    }

    [Fact]
    public void DecidePdfRoute_MaxPagesSet_OverLocalParsingCap_UsesDocumentIntelligence()
    {
        // MaxPages bounds DI to that page count regardless of file size, so oversized files route to DI
        // instead of giving up — they are not too large when only a handful of pages will ever be analyzed.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: (250 * 1024 * 1024) + 1, maxPages: 1);

        Assert.Equal(PdfSizeRoute.UseDocumentIntelligence, route);
    }

    [Fact]
    public void DecidePdfRoute_MaxPagesGreaterThanOne_OverLocalParsingCap_UsesDocumentIntelligence()
    {
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: (250 * 1024 * 1024) + 1, maxPages: 5);

        Assert.Equal(PdfSizeRoute.UseDocumentIntelligence, route);
    }

    [Fact]
    public void DecidePdfRoute_MaxPagesSet_UnavailableContentLength_UsesDocumentIntelligence()
    {
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: -1, maxPages: 1);

        Assert.Equal(PdfSizeRoute.UseDocumentIntelligence, route);
    }

    [Fact]
    public void DecidePdfRoute_MaxPagesSet_SmallFile_AttemptsLocalParsing()
    {
        // Still cheaper to read locally when the file is small enough to safely open.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: 1024, maxPages: 1);

        Assert.Equal(PdfSizeRoute.AttemptLocalParsing, route);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DecidePdfRoute_MaxPagesZeroOrNegative_OverLocalParsingCap_IsTooLarge(int maxPages)
    {
        // Zero/negative MaxPages carries no page limit, same as null -- it must not be treated as
        // "bounded" and bypass the size cap that guards a full-document DI call.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: (250 * 1024 * 1024) + 1, maxPages: maxPages);

        Assert.Equal(PdfSizeRoute.TooLargeForProcessing, route);
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_UnderCap_ReturnsFalse()
    {
        Assert.False(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 120, maxPages: null));
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_OverCap_ReturnsTrue()
    {
        Assert.True(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 121, maxPages: null));
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_MaxPagesSet_OverCap_ReturnsFalse()
    {
        // MaxPages bounds DI to that page count regardless of the document's real page count.
        Assert.False(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 5000, maxPages: 1));
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_MaxPagesGreaterThanOne_OverCap_ReturnsFalse()
    {
        Assert.False(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 5000, maxPages: 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExceedsDocumentIntelligencePageCap_MaxPagesZeroOrNegative_OverCap_ReturnsTrue(int maxPages)
    {
        // Zero/negative MaxPages carries no page limit, same as null -- it must not be treated as
        // "bounded" and let an over-cap document skip straight to an uncapped DI call.
        Assert.True(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 121, maxPages: maxPages));
    }

    [Fact]
    public void BuildPagesParameter_Pdf_MaxPagesOne_ReturnsOne()
    {
        Assert.Equal("1", ExtractContentActivity.BuildPagesParameter("report.pdf", maxPages: 1));
    }

    [Fact]
    public void BuildPagesParameter_Pdf_MaxPagesGreaterThanOne_ReturnsRange()
    {
        // DI's Pages parameter accepts range syntax -- "1-N" requests the first N pages.
        Assert.Equal("1-5", ExtractContentActivity.BuildPagesParameter("report.pdf", maxPages: 5));
    }

    [Fact]
    public void BuildPagesParameter_Pdf_NoMaxPages_ReturnsNull()
    {
        Assert.Null(ExtractContentActivity.BuildPagesParameter("report.pdf", maxPages: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildPagesParameter_Pdf_MaxPagesZeroOrNegative_ReturnsNull(int maxPages)
    {
        // Zero/negative MaxPages carries no page limit, same as null -- it must not be treated as
        // "bounded" and produce a nonsensical Pages value like "1-0" or "1--1".
        Assert.Null(ExtractContentActivity.BuildPagesParameter("report.pdf", maxPages: maxPages));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("deck.pptx")]
    public void BuildPagesParameter_NonPdf_MaxPagesSet_ReturnsNull(string fileName)
    {
        // DI errors on the Pages parameter for Word documents, and pagination isn't a fixed concept
        // for flowing Office formats generally -- so these always get a full analysis regardless.
        Assert.Null(ExtractContentActivity.BuildPagesParameter(fileName, maxPages: 1, convertedToPdf: false));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("deck.pptx")]
    public void BuildPagesParameter_ConvertedToPdf_MaxPagesOne_ReturnsOne(string fileName)
    {
        // The URL DI receives points at the converted PDF, not the Office original, so Pages applies
        // even though the file name still says .docx/.pptx. Without this, a converted Office file that
        // falls back to DI would be billed for a full-document analysis on a classify-only run.
        Assert.Equal("1", ExtractContentActivity.BuildPagesParameter(fileName, maxPages: 1, convertedToPdf: true));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("deck.pptx")]
    public void BuildPagesParameter_ConvertedToPdf_MaxPagesGreaterThanOne_ReturnsRange(string fileName)
    {
        Assert.Equal("1-3", ExtractContentActivity.BuildPagesParameter(fileName, maxPages: 3, convertedToPdf: true));
    }

    [Fact]
    public void BuildPagesParameter_ConvertedToPdf_NoMaxPages_ReturnsNull()
    {
        Assert.Null(ExtractContentActivity.BuildPagesParameter("report.docx", maxPages: null, convertedToPdf: true));
    }

    [Theory]
    [InlineData(2, 2)]  // under the document's real page count -- bounds down
    [InlineData(10, 5)] // over the document's real page count -- clamps to what's actually there
    public async Task ExtractPdfAsync_BoundsPageCountToMaxPagesAndActualPageCount(int maxPages, int expectedPageCount)
    {
        var activity = CreateActivity(CreateTestPdf(pageCount: 5));

        var result = await activity.ExtractContent(new ExtractContentInput(
            DocumentUrl: "https://example.com/test.pdf",
            FileName: "test.pdf",
            MaxPages: maxPages));

        Assert.Equal(expectedPageCount, result.PageCount);
        Assert.Equal("pdfpig", result.ExtractionMethod);
    }

    [Fact]
    public async Task ExtractSpreadsheetAsync_OversizedContentLength_ReturnsTooLargeWithoutBuffering()
    {
        var activity = CreateSpreadsheetActivity(body: [1, 2, 3], contentLength: SpreadsheetExtractor.MaxFileSizeBytes + 1);

        var result = await activity.ExtractContent(new ExtractContentInput(
            DocumentUrl: "https://example.com/test.xlsx",
            FileName: "test.xlsx"));

        Assert.True(result.IsTooLarge);
        Assert.Equal(0, result.PageCount);
    }

    [Fact]
    public async Task ExtractSpreadsheetAsync_UnknownContentLength_ReturnsTooLarge()
    {
        // A missing/unavailable Content-Length header must not silently bypass the size cap -- it's
        // treated the same as exceeding it, since the size can't be verified either way.
        var activity = CreateSpreadsheetActivity(body: [1, 2, 3], contentLength: null);

        var result = await activity.ExtractContent(new ExtractContentInput(
            DocumentUrl: "https://example.com/test.xlsx",
            FileName: "test.xlsx"));

        Assert.True(result.IsTooLarge);
    }

    private static ExtractContentActivity CreateSpreadsheetActivity(byte[] body, long? contentLength)
    {
        var handler = new StubHttpMessageHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            };
            response.Content.Headers.ContentLength = contentLength;
            return response;
        });
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new StubHttpClientFactory(httpClient);
        var docIntelClient = new DocumentIntelligenceClient(new Uri("https://example.com"), new AzureKeyCredential("fake-key"));

        return new ExtractContentActivity(docIntelClient, httpClientFactory, NullLogger<ExtractContentActivity>.Instance);
    }

    private static ExtractContentActivity CreateActivity(byte[] pdfBytes)
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(pdfBytes),
        });
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new StubHttpClientFactory(httpClient);

        // Never invoked: the test PDF is born-digital and stays entirely on the local PdfPig path, but the
        // activity's constructor still requires a client instance.
        var docIntelClient = new DocumentIntelligenceClient(new Uri("https://example.com"), new AzureKeyCredential("fake-key"));

        return new ExtractContentActivity(docIntelClient, httpClientFactory, NullLogger<ExtractContentActivity>.Instance);
    }

    // Builds a minimal, hand-rolled multi-page PDF with a real text layer on every page -- PdfPig needs to
    // both open it and see extractable text for IsBornDigital to keep the document on the local-parsing
    // path this test targets, rather than falling back to Document Intelligence.
    private static byte[] CreateTestPdf(int pageCount)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();

        void WriteObject(int number, string body)
        {
            offsets.Add(sb.Length);
            sb.Append(number).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        }

        sb.Append("%PDF-1.4\n");

        var pageNumbers = Enumerable.Range(3, pageCount).ToArray();
        var contentNumbers = Enumerable.Range(3 + pageCount, pageCount).ToArray();
        var fontNumber = 3 + (2 * pageCount);

        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(2, $"<< /Type /Pages /Kids [{string.Join(' ', pageNumbers.Select(n => $"{n} 0 R"))}] /Count {pageCount} >>");

        for (var i = 0; i < pageCount; i++)
        {
            WriteObject(pageNumbers[i],
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
                $"/Resources << /Font << /F1 {fontNumber} 0 R >> >> /Contents {contentNumbers[i]} 0 R >>");
        }

        for (var i = 0; i < pageCount; i++)
        {
            var text = $"This is page {i + 1} of a test document with a real text layer.";
            var content = $"BT /F1 12 Tf 72 720 Td ({text}) Tj ET";
            WriteObject(contentNumbers[i], $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }

        WriteObject(fontNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var xrefOffset = sb.Length;
        var size = fontNumber + 1;
        sb.Append("xref\n0 ").Append(size).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }
        sb.Append("trailer\n<< /Size ").Append(size).Append(" /Root 1 0 R >>\nstartxref\n")
          .Append(xrefOffset).Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory());
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
