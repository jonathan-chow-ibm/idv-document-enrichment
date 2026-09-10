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
}
