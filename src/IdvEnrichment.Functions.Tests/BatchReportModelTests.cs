using System.Text.Json;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class BatchReportModelTests
{
    [Fact]
    public void FailedDocumentEntry_SerializesWithExpectedPropertyNames()
    {
        var entry = new FailedDocumentEntry(DocumentId: "doc-1", FileName: "sub/report.pdf");

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"documentId\":\"doc-1\"", json);
        Assert.Contains("\"fileName\":\"sub/report.pdf\"", json);
    }

    [Fact]
    public void FailedDocumentEntry_RoundTripsThroughJson()
    {
        var entry = new FailedDocumentEntry(DocumentId: "doc-1", FileName: "sub/report.pdf");

        var json = JsonSerializer.Serialize(entry);
        var deserialized = JsonSerializer.Deserialize<FailedDocumentEntry>(json);

        Assert.Equal(entry, deserialized);
    }

    [Fact]
    public void BatchRequest_DeserializesItemIds()
    {
        const string json = """
            {
                "url": "https://tenant.sharepoint.com/sites/Site/Library",
                "itemIds": ["1", "2", "3"]
            }
            """;

        var request = JsonSerializer.Deserialize<BatchRequest>(json);

        Assert.NotNull(request);
        Assert.Equal(["1", "2", "3"], request!.ItemIds);
    }

    [Fact]
    public void BatchRequest_ItemIdsOmitted_DefaultsToNull()
    {
        const string json = """{ "url": "https://tenant.sharepoint.com/sites/Site/Library" }""";

        var request = JsonSerializer.Deserialize<BatchRequest>(json);

        Assert.NotNull(request);
        Assert.Null(request!.ItemIds);
    }

    [Fact]
    public void BatchReport_SerializesFailedDocumentsField()
    {
        var report = new BatchReport(
            BatchId: "batch-1",
            Url: "https://tenant.sharepoint.com/sites/Site/Library",
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch,
            Summary: new BatchSummary(TotalDocuments: 1, Classified: 0, UnderReview: 0, WriteBackFailed: 0, Errors: 1, Skipped: 0),
            ConfidenceDistribution: new ConfidenceDistribution(0, 0, 0),
            DocumentTypeCounts: new Dictionary<string, int>(),
            SuggestedFieldsByGroup: [],
            Cost: new BatchCost(0m, new TokenUsage(0, 0), new TokenUsage(0, 0), new TokenUsage(0, 0), 0m),
            FailedDocuments: [new FailedDocumentEntry("doc-1", "a.pdf")],
            LowConfidenceClassifications: []);

        var json = JsonSerializer.Serialize(report);

        Assert.Contains("\"failedDocuments\":[{\"documentId\":\"doc-1\",\"fileName\":\"a.pdf\"}]", json);
    }
}
