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

    private static TaxonomyData BuildSuggestionTaxonomy() => new(
        DocumentTypes:
        [
            new DocumentTypeDefinition { Label = "Lease", Group = "Contracts" },
            new DocumentTypeDefinition { Label = "Vendor Contract", Group = "Contracts" },
            new DocumentTypeDefinition { Label = "Survey", Group = "Drawing Files" },
        ],
        Metadata: new MetadataConfig(),
        Thresholds: new ConfidenceThresholds());

    private static BatchDocumentEntry BuildSuggestionEntry(
        DocumentType documentType, params (string Key, string Value)[] suggestions) =>
        new(documentType, RoutingDecision.Write, TypeConfidence: 0.9, WriteBackSucceeded: true,
            SuggestedFields: suggestions.Select(s => new SuggestedField(s.Key, s.Value, 0.8)).ToList());

    [Fact]
    public void BuildSuggestedFieldsByGroup_CarriesExampleValues()
    {
        // The old projection kept only f.Key, so ExampleValues was structurally always empty and a reader
        // could not tell what a suggested field actually held.
        var results = new[] { BuildSuggestionEntry(DocumentType.Lease, ("rentEscalation", "3% annually")) };

        var groups = GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy());

        var field = Assert.Single(Assert.Single(Assert.Single(groups).ByDocumentType).Fields);
        Assert.Equal("rentEscalation", field.Key);
        Assert.Equal(["3% annually"], field.ExampleValues);
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_DeduplicatesExampleValues_AndCapsAtThree()
    {
        var results = new[]
        {
            BuildSuggestionEntry(DocumentType.Lease, ("term", "5 years")),
            BuildSuggestionEntry(DocumentType.Lease, ("term", "5 YEARS")),
            BuildSuggestionEntry(DocumentType.Lease, ("term", "10 years")),
            BuildSuggestionEntry(DocumentType.Lease, ("term", "7 years")),
            BuildSuggestionEntry(DocumentType.Lease, ("term", "3 years")),
        };

        var field = Assert.Single(Assert.Single(Assert.Single(
            GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy()))
            .ByDocumentType).Fields);

        Assert.Equal(5, field.DocumentCount);
        Assert.Equal(3, field.ExampleValues.Count);
        // "5 YEARS" is the same value as "5 years" to a human reading the report.
        Assert.Single(field.ExampleValues, v => v == "5 years");
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_KeepsEveryDistinctKey_NoTopNCap()
    {
        // The previous .Take(10) silently dropped the low-frequency tail -- the exact region where a
        // type-specific field is most likely to sit.
        var results = Enumerable.Range(0, 14)
            .Select(i => BuildSuggestionEntry(DocumentType.Lease, ($"field{i:00}", $"value{i}")))
            .ToArray();

        var byType = Assert.Single(Assert.Single(
            GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy()))
            .ByDocumentType);

        Assert.Equal(14, byType.Fields.Count);
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_CountsOtherTypesSharingAKey()
    {
        var results = new[]
        {
            BuildSuggestionEntry(DocumentType.Lease, ("counterpartyRole", "Tenant"), ("rentEscalation", "3%")),
            BuildSuggestionEntry(DocumentType.VendorContract, ("counterpartyRole", "Vendor")),
            BuildSuggestionEntry(DocumentType.Survey, ("counterpartyRole", "Surveyor")),
        };

        var lease = GenerateBatchReportActivity
            .BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy())
            .SelectMany(g => g.ByDocumentType)
            .Single(t => t.DocumentType == "Lease");

        // Shared with Vendor Contract and Survey -- two other types.
        Assert.Equal(2, lease.Fields.Single(f => f.Key == "counterpartyRole").OtherTypeCount);
        // Suggested for Lease alone.
        Assert.Equal(0, lease.Fields.Single(f => f.Key == "rentEscalation").OtherTypeCount);
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_RanksExclusiveFieldsAboveMoreFrequentSharedOnes()
    {
        // "documentDate" appears in three Leases, "rentEscalation" in one. Frequency ranking would put
        // documentDate first, but it is suggested for other types too, so it is worthless as a
        // Lease-specific field -- exclusivity has to outrank raw count.
        var results = new[]
        {
            BuildSuggestionEntry(DocumentType.Lease, ("documentDate", "2026-01-01"), ("rentEscalation", "3%")),
            BuildSuggestionEntry(DocumentType.Lease, ("documentDate", "2026-02-01")),
            BuildSuggestionEntry(DocumentType.Lease, ("documentDate", "2026-03-01")),
            BuildSuggestionEntry(DocumentType.VendorContract, ("documentDate", "2026-04-01")),
        };

        var lease = GenerateBatchReportActivity
            .BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy())
            .SelectMany(g => g.ByDocumentType)
            .Single(t => t.DocumentType == "Lease");

        Assert.Equal("rentEscalation", lease.Fields[0].Key);
        Assert.Equal("documentDate", lease.Fields[1].Key);
        Assert.Equal(3, lease.Fields[1].DocumentCount);
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_GroupsByTaxonomyGroup_AndFallsBackToOther()
    {
        var results = new[]
        {
            BuildSuggestionEntry(DocumentType.Lease, ("a", "1")),
            BuildSuggestionEntry(DocumentType.Survey, ("b", "2")),
            // Proforma has no definition in the test taxonomy, so it lands in the "Other" bucket.
            BuildSuggestionEntry(DocumentType.Proforma, ("c", "3")),
        };

        var groups = GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy());

        Assert.Equal(["Contracts", "Drawing Files", "Other"], groups.Select(g => g.Group));
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_IgnoresDocumentsWithNoSuggestions()
    {
        var results = new[]
        {
            BuildSuggestionEntry(DocumentType.Lease),
            new BatchDocumentEntry(DocumentType.Survey, RoutingDecision.Write, 0.9, true),
        };

        Assert.Empty(GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy()));
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_SkipsBlankKeys()
    {
        var results = new[] { BuildSuggestionEntry(DocumentType.Lease, ("", "orphan"), ("realField", "value")) };

        var byType = Assert.Single(Assert.Single(
            GenerateBatchReportActivity.BuildSuggestedFieldsByGroup(results, BuildSuggestionTaxonomy()))
            .ByDocumentType);

        Assert.Equal("realField", Assert.Single(byType.Fields).Key);
    }

    [Fact]
    public void BuildSuggestedFieldsByGroup_EmptyResults_ReturnsEmptyList()
    {
        Assert.Empty(GenerateBatchReportActivity.BuildSuggestedFieldsByGroup([], BuildSuggestionTaxonomy()));
    }
}
