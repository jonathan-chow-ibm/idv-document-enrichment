using IdvEnrichment.Functions.Activities;
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
    public void DecidePdfRoute_FirstPageOnly_OverLocalParsingCap_UsesDocumentIntelligence()
    {
        // FirstPageOnly bounds DI to page 1 regardless of file size, so oversized files route to DI
        // instead of giving up — they are not too large when only one page will ever be analyzed.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: (250 * 1024 * 1024) + 1, firstPageOnly: true);

        Assert.Equal(PdfSizeRoute.UseDocumentIntelligence, route);
    }

    [Fact]
    public void DecidePdfRoute_FirstPageOnly_UnavailableContentLength_UsesDocumentIntelligence()
    {
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: -1, firstPageOnly: true);

        Assert.Equal(PdfSizeRoute.UseDocumentIntelligence, route);
    }

    [Fact]
    public void DecidePdfRoute_FirstPageOnly_SmallFile_AttemptsLocalParsing()
    {
        // Still cheaper to read locally when the file is small enough to safely open.
        var route = ExtractContentActivity.DecidePdfRoute(contentLength: 1024, firstPageOnly: true);

        Assert.Equal(PdfSizeRoute.AttemptLocalParsing, route);
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_UnderCap_ReturnsFalse()
    {
        Assert.False(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 120, firstPageOnly: false));
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_OverCap_ReturnsTrue()
    {
        Assert.True(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 121, firstPageOnly: false));
    }

    [Fact]
    public void ExceedsDocumentIntelligencePageCap_FirstPageOnly_OverCap_ReturnsFalse()
    {
        // FirstPageOnly bounds DI to page 1 regardless of the document's real page count.
        Assert.False(ExtractContentActivity.ExceedsDocumentIntelligencePageCap(pageCount: 5000, firstPageOnly: true));
    }

    [Fact]
    public void BuildPagesParameter_Pdf_FirstPageOnly_ReturnsOne()
    {
        Assert.Equal("1", ExtractContentActivity.BuildPagesParameter("report.pdf", firstPageOnly: true));
    }

    [Fact]
    public void BuildPagesParameter_Pdf_NotFirstPageOnly_ReturnsNull()
    {
        Assert.Null(ExtractContentActivity.BuildPagesParameter("report.pdf", firstPageOnly: false));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("deck.pptx")]
    public void BuildPagesParameter_NonPdf_FirstPageOnly_ReturnsNull(string fileName)
    {
        // DI errors on the Pages parameter for Word documents, and pagination isn't a fixed concept
        // for flowing Office formats generally -- so these always get a full analysis regardless.
        Assert.Null(ExtractContentActivity.BuildPagesParameter(fileName, firstPageOnly: true));
    }
}
