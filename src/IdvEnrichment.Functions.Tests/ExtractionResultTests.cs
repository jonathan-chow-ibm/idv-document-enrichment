using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ExtractionResultTests
{
    [Fact]
    public void TooLargeForProcessing_SetsIsTooLarge_AndNotIsUnsupported()
    {
        var result = ExtractionResult.TooLargeForProcessing("huge-scan.pdf", 300 * 1024 * 1024);

        Assert.True(result.IsTooLarge);
        Assert.False(result.IsUnsupported);
        Assert.Contains(".pdf", result.Text);
        Assert.Contains("300 MB", result.Text);
    }

    [Fact]
    public void UnsupportedFormat_SetsIsUnsupported_AndNotIsTooLarge()
    {
        var result = ExtractionResult.UnsupportedFormat("legacy.xls");

        Assert.True(result.IsUnsupported);
        Assert.False(result.IsTooLarge);
    }
}
