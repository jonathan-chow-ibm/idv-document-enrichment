using ClosedXML.Excel;
using IdvEnrichment.Functions.Shared;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class SpreadsheetExtractorTests
{
    private static Stream BuildWorkbook(Action<XLWorkbook> configure)
    {
        using var workbook = new XLWorkbook();
        configure(workbook);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void SmallWorkbook_ContainsAllSheetNames()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var s1 = wb.Worksheets.Add("Alpha");
            s1.Cell(1, 1).Value = "Value";
            var s2 = wb.Worksheets.Add("Beta");
            s2.Cell(1, 1).Value = "Other";
        });

        var result = SpreadsheetExtractor.ExtractToMarkdown(stream);

        Assert.Contains("Alpha", result);
        Assert.Contains("Beta", result);
    }

    [Fact]
    public void LargeWorkbook_SummarySheetGetsMoreContentThanRawData()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var summary = wb.Worksheets.Add("Summary");
            // Fill with enough data to exceed the budget when combined
            for (var row = 1; row <= 500; row++)
            {
                for (var col = 1; col <= 10; col++)
                {
                    summary.Cell(row, col).Value = $"SummaryData_{row}_{col}";
                }
            }

            var raw = wb.Worksheets.Add("RawData");
            for (var row = 1; row <= 500; row++)
            {
                for (var col = 1; col <= 10; col++)
                {
                    raw.Cell(row, col).Value = $"RawData_{row}_{col}";
                }
            }
        });

        var result = SpreadsheetExtractor.ExtractToMarkdown(stream);

        var summaryIdx = result.IndexOf("## Sheet: Summary", StringComparison.Ordinal);
        var rawIdx = result.IndexOf("## Sheet: RawData", StringComparison.Ordinal);
        Assert.True(summaryIdx >= 0 && rawIdx >= 0);

        // The summary section should span more characters than the raw data section
        var summaryLength = rawIdx - summaryIdx;
        var rawLength = result.Length - rawIdx;
        Assert.True(summaryLength > rawLength,
            $"Expected Summary ({summaryLength} chars) > RawData ({rawLength} chars)");
    }

    [Fact]
    public void EmptySheet_OutputContainsEmptySheetMarker()
    {
        using var stream = BuildWorkbook(wb => wb.Worksheets.Add("Empty"));

        var result = SpreadsheetExtractor.ExtractToMarkdown(stream);

        Assert.Contains("_(empty sheet)_", result);
    }

    [Fact]
    public void IrrModelSheet_OutputWithinBudget()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var sheet = wb.Worksheets.Add("IRR Model");
            for (var row = 1; row <= 600; row++)
            {
                for (var col = 1; col <= 12; col++)
                {
                    sheet.Cell(row, col).Value = $"IRRData_{row}_{col}_padding_padding";
                }
            }
        });

        var result = SpreadsheetExtractor.ExtractToMarkdown(stream);

        Assert.True(result.Length <= 24000,
            $"Output length {result.Length} exceeds 24000 char budget");
    }
}
