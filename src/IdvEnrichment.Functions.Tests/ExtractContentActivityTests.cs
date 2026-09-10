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
}
