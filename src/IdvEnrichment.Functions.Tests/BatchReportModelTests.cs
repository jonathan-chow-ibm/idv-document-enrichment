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
    public void LowConfidenceClassificationEntry_SerializesWithExpectedPropertyNames()
    {
        var entry = new LowConfidenceClassificationEntry(
            DocumentId: "doc-1",
            FileName: "sub/report.pdf",
            DocumentType: "Lease",
            Confidence: 0.62,
            Candidates: [new ClassificationCandidateEntry(DocumentType: "Amendment", Confidence: 0.31)]);

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"documentId\":\"doc-1\"", json);
        Assert.Contains("\"fileName\":\"sub/report.pdf\"", json);
        Assert.Contains("\"documentType\":\"Lease\"", json);
        Assert.Contains("\"confidence\":0.62", json);
        Assert.Contains("\"candidates\":[{\"documentType\":\"Amendment\",\"confidence\":0.31}]", json);
    }

    [Fact]
    public void LowConfidenceClassificationEntry_RoundTripsThroughJson()
    {
        var entry = new LowConfidenceClassificationEntry(
            DocumentId: "doc-1",
            FileName: "sub/report.pdf",
            DocumentType: "Lease",
            Confidence: 0.62,
            Candidates: [new ClassificationCandidateEntry(DocumentType: "Amendment", Confidence: 0.31)]);

        var json = JsonSerializer.Serialize(entry);
        var deserialized = JsonSerializer.Deserialize<LowConfidenceClassificationEntry>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(entry.DocumentId, deserialized!.DocumentId);
        Assert.Equal(entry.FileName, deserialized.FileName);
        Assert.Equal(entry.DocumentType, deserialized.DocumentType);
        Assert.Equal(entry.Confidence, deserialized.Confidence);
        Assert.Equal(entry.Candidates, deserialized.Candidates);
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
            LowConfidenceClassifications: [],
            SizeByDocumentType: []);

        var json = JsonSerializer.Serialize(report);

        Assert.Contains("\"failedDocuments\":[{\"documentId\":\"doc-1\",\"fileName\":\"a.pdf\"}]", json);
    }
}
