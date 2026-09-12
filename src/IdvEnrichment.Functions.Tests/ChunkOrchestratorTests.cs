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
                new ClassificationCandidate("Letter of Intent", 0.35),
                new ClassificationCandidate("PSA - Acquisition", 0.3),
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

    [Fact]
    public void BuildLowConfidenceEntry_CandidateOutsideTaxonomy_SurvivesVerbatim()
    {
        // Agent 1 runs without a response schema and names types the taxonomy folds into Other. Binding
        // candidates to the enum threw and failed the whole classification; keeping the model's own
        // wording is what makes the candidates list useful to a human -- and flags a taxonomy gap.
        var classification = new TypeClassificationResult(
            DocumentType.Other,
            0.4,
            "unclear",
            Candidates: [new ClassificationCandidate("Proposal/Pitch Deck", 0.45)]);

        var entry = ChunkOrchestrator.BuildLowConfidenceEntry("doc-1", "pitch.docx", classification);

        Assert.NotNull(entry);
        var candidate = Assert.Single(entry.Candidates);
        Assert.Equal("Proposal/Pitch Deck", candidate.DocumentType);
    }
}
