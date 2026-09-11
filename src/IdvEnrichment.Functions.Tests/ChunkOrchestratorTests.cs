using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Orchestrators;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ChunkOrchestratorTests
{
    [Fact]
    public void BuildLowConfidenceEntry_NoCandidates_ReturnsNull()
    {
        var classification = new TypeClassificationResult(DocumentType.LetterOfIntent, 0.95, "clear match");

        var entry = ChunkOrchestrator.BuildLowConfidenceEntry("doc-1", "a.pdf", classification);

        Assert.Null(entry);
    }

    [Fact]
    public void BuildLowConfidenceEntry_EmptyCandidates_ReturnsNull()
    {
        var classification = new TypeClassificationResult(DocumentType.Other, 0.4, "unclear", Candidates: []);

        var entry = ChunkOrchestrator.BuildLowConfidenceEntry("doc-1", "a.pdf", classification);

        Assert.Null(entry);
    }

    [Fact]
    public void BuildLowConfidenceEntry_WithCandidates_MapsAllFields()
    {
        var classification = new TypeClassificationResult(
            DocumentType.Other,
            0.4,
            "unclear",
            Candidates:
            [
                new ClassificationCandidate(DocumentType.LetterOfIntent, 0.35),
                new ClassificationCandidate(DocumentType.PsaAcquisition, 0.3),
            ]);

        var entry = ChunkOrchestrator.BuildLowConfidenceEntry("doc-1", "a.pdf", classification);

        Assert.NotNull(entry);
        Assert.Equal("doc-1", entry.DocumentId);
        Assert.Equal("a.pdf", entry.FileName);
        Assert.Equal("Other", entry.DocumentType);
        Assert.Equal(0.4, entry.Confidence);
        Assert.Collection(
            entry.Candidates,
            c => Assert.Equal(("Letter of Intent", 0.35), (c.DocumentType, c.Confidence)),
            c => Assert.Equal(("PSA - Acquisition", 0.3), (c.DocumentType, c.Confidence)));
    }
}
