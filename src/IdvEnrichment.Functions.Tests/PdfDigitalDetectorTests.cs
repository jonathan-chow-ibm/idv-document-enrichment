using IdvEnrichment.Functions.Shared;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class PdfDigitalDetectorTests
{
    private static PageTextInfo DigitalPage(int textLength = 500) => new(textLength, HasFullPageImage: false);

    private static PageTextInfo ScannedPage() => new(ExtractedTextLength: 0, HasFullPageImage: true);

    [Fact]
    public void IsBornDigital_AllPagesHaveText_ReturnsTrue()
    {
        var pages = new[] { DigitalPage(), DigitalPage(), DigitalPage() };

        Assert.True(PdfDigitalDetector.IsBornDigital(pages));
    }

    [Fact]
    public void IsBornDigital_AllPagesAreScanned_ReturnsFalse()
    {
        var pages = new[] { ScannedPage(), ScannedPage(), ScannedPage() };

        Assert.False(PdfDigitalDetector.IsBornDigital(pages));
    }

    [Fact]
    public void IsBornDigital_RatioExactlyAtThreshold_ReturnsTrue()
    {
        // 9 of 10 pages digital == exactly the default 0.9 ratio.
        var pages = Enumerable.Repeat(DigitalPage(), 9).Append(ScannedPage()).ToList();

        Assert.True(PdfDigitalDetector.IsBornDigital(pages));
    }

    [Fact]
    public void IsBornDigital_RatioJustBelowThreshold_ReturnsFalse()
    {
        // 8 of 10 pages digital, just under the default 0.9 ratio.
        var pages = Enumerable.Repeat(DigitalPage(), 8).Concat([ScannedPage(), ScannedPage()]).ToList();

        Assert.False(PdfDigitalDetector.IsBornDigital(pages));
    }

    [Fact]
    public void IsBornDigital_EmptyPageList_ReturnsFalse()
    {
        Assert.False(PdfDigitalDetector.IsBornDigital([]));
    }

    [Fact]
    public void IsBornDigital_TextBelowMinimumLength_CountsAsScanned()
    {
        // Below the 20-char minimum, even a page with no full-page image doesn't count as digital.
        var pages = new[] { new PageTextInfo(ExtractedTextLength: 5, HasFullPageImage: false) };

        Assert.False(PdfDigitalDetector.IsBornDigital(pages));
    }

    [Fact]
    public void IsBornDigital_RealTextButFullPageImage_CountsAsScanned()
    {
        // Real text alongside a full-page image is treated as an OCR overlay on a scan, not a genuine text layer.
        var pages = new[] { new PageTextInfo(ExtractedTextLength: 500, HasFullPageImage: true) };

        Assert.False(PdfDigitalDetector.IsBornDigital(pages));
    }
}
