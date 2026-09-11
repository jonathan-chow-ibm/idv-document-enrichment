using IdvEnrichment.Functions.Activities;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class GetDocumentDownloadUrlActivityTests
{
    [Theory]
    [InlineData("report.docx", true)]
    [InlineData("slides.pptx", true)]
    [InlineData("REPORT.DOCX", true)]
    [InlineData("drawing.pdf", false)]
    [InlineData("workbook.xlsx", false)]
    [InlineData("notes.txt", false)]
    public void ShouldConvertToPdf_ReturnsExpected(string fileName, bool expected)
    {
        var result = GetDocumentDownloadUrlActivity.ShouldConvertToPdf(fileName);

        Assert.Equal(expected, result);
    }
}
