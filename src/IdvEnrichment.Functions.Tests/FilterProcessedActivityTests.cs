using IdvEnrichment.Functions.Activities;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class FilterProcessedActivityTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("review")]
    public void ShouldSkip_TerminalStatus_ReturnsTrue(string status)
    {
        Assert.True(FilterProcessedActivity.ShouldSkip(status));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("write-back-failed")]
    [InlineData("unknown-status")]
    public void ShouldSkip_NonTerminalStatus_ReturnsFalse(string status)
    {
        Assert.False(FilterProcessedActivity.ShouldSkip(status));
    }

    [Fact]
    public void ShouldSkip_NullStatus_ReturnsFalse()
    {
        Assert.False(FilterProcessedActivity.ShouldSkip(null));
    }
}
