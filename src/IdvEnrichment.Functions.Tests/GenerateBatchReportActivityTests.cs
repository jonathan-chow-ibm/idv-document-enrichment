using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class GenerateBatchReportActivityTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    public void FormatBytes_UnderOneKb_ReturnsWholeBytes(long bytes, string expected)
    {
        Assert.Equal(expected, GenerateBatchReportActivity.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(10240, "10.00 KB")]
    public void FormatBytes_UnderOneMb_ReturnsKb(long bytes, string expected)
    {
        Assert.Equal(expected, GenerateBatchReportActivity.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1024 * 1024, "1.00 MB")]
    [InlineData(12 * 1024 * 1024L + 512 * 1024, "12.50 MB")]
    public void FormatBytes_UnderOneGb_ReturnsMb(long bytes, string expected)
    {
        Assert.Equal(expected, GenerateBatchReportActivity.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1024L * 1024 * 1024, "1.00 GB")]
    [InlineData(3L * 1024 * 1024 * 1024 + 512L * 1024 * 1024, "3.50 GB")]
    public void FormatBytes_AtLeastOneGb_ReturnsGb(long bytes, string expected)
    {
        Assert.Equal(expected, GenerateBatchReportActivity.FormatBytes(bytes));
    }

    private static BatchDocumentEntry BuildEntry(DocumentType documentType, long sizeBytes) =>
        new(documentType, RoutingDecision.Write, TypeConfidence: 0.9, WriteBackSucceeded: true, SizeBytes: sizeBytes);

    [Fact]
    public void BuildSizeByDocumentType_GroupsAndAggregatesByDocumentType()
    {
        var results = new[]
        {
            BuildEntry(DocumentType.LetterOfIntent, 1000),
            BuildEntry(DocumentType.LetterOfIntent, 2000),
            BuildEntry(DocumentType.PsaAcquisition, 3000),
        };

        var stats = GenerateBatchReportActivity.BuildSizeByDocumentType(results);

        Assert.Collection(
            stats,
            s =>
            {
                Assert.Equal("Letter of Intent", s.DocumentType);
                Assert.Equal(2, s.DocumentCount);
                Assert.Equal(3000, s.TotalSizeBytes);
                Assert.Equal(1500, s.AverageSizeBytes);
            },
            s =>
            {
                Assert.Equal("PSA - Acquisition", s.DocumentType);
                Assert.Equal(1, s.DocumentCount);
                Assert.Equal(3000, s.TotalSizeBytes);
                Assert.Equal(3000, s.AverageSizeBytes);
            });
    }

    [Fact]
    public void BuildSizeByDocumentType_FormatsTotalsAndAverages()
    {
        var results = new[] { BuildEntry(DocumentType.Other, 1024) };

        var stats = GenerateBatchReportActivity.BuildSizeByDocumentType(results);

        var entry = Assert.Single(stats);
        Assert.Equal("1.00 KB", entry.TotalSizeFormatted);
        Assert.Equal("1.00 KB", entry.AverageSizeFormatted);
    }

    [Fact]
    public void BuildSizeByDocumentType_EmptyResults_ReturnsEmptyList()
    {
        var stats = GenerateBatchReportActivity.BuildSizeByDocumentType([]);

        Assert.Empty(stats);
    }
}
