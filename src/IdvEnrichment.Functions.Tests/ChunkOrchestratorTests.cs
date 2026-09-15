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

    private static ChunkRequest BuildRequest(LibraryDocument doc) =>
        new(Documents: [doc],
            BatchId: "batch-1",
            Target: new ResolvedSharePointTarget("site", "drive", null, "Site", "Library"),
            MaxConcurrency: 1);

    private static EnrichmentResult BuildEnrichmentResult() =>
        new(DocumentId: "doc-1",
            FileName: "a.pdf",
            Extraction: new ExtractionResult("text", PageCount: 1, TextLength: 4, KeyValuePairs: []),
            TypeClassification: new TypeClassificationResult(DocumentType.LetterOfIntent, 0.95, "clear match"),
            Metadata: null,
            ProcessingMetrics: new ProcessingMetrics(),
            RoutingDecision: RoutingDecision.Write,
            LowConfidenceCategories: []);

    [Fact]
    public async Task ChunkProcessingOrchestrator_SubOrchestrationNeverCompletes_RecordsDocumentAsFailed()
    {
        var doc = new LibraryDocument("doc-1", "a.pdf", "application/pdf", DateTimeOffset.UnixEpoch);
        var ctx = new FakeOrchestrationContext(BuildRequest(doc))
        {
            // A wedged sub-orchestration: without the timer guard the chunk would await this forever.
            SubOrchestratorHandler = (_, _) => new TaskCompletionSource<object?>().Task,
        };
        ctx.FireTimers();

        var result = await new ChunkOrchestrator().ChunkProcessingOrchestrator(ctx);

        Assert.Equal(1, result.Errors);
        Assert.Equal("doc-1", Assert.Single(result.FailedDocuments).DocumentId);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task ChunkProcessingOrchestrator_SubOrchestrationCompletes_CancelsTimerAndKeepsResult()
    {
        var doc = new LibraryDocument("doc-1", "a.pdf", "application/pdf", DateTimeOffset.UnixEpoch);
        var ctx = new FakeOrchestrationContext(BuildRequest(doc))
        {
            SubOrchestratorHandler = (_, _) => Task.FromResult<object?>(BuildEnrichmentResult()),
        };

        var result = await new ChunkOrchestrator().ChunkProcessingOrchestrator(ctx);

        Assert.Equal(0, result.Errors);
        Assert.Single(result.Results);
        // An unexpired timer would hold the orchestration in Running until its deadline passed.
        Assert.True(ctx.TimerCancelled);
    }

    [Fact]
    public async Task ChunkProcessingOrchestrator_ArmsTimeoutFromOrchestrationClock()
    {
        var doc = new LibraryDocument("doc-1", "a.pdf", "application/pdf", DateTimeOffset.UnixEpoch);
        var ctx = new FakeOrchestrationContext(BuildRequest(doc))
        {
            SubOrchestratorHandler = (_, _) => Task.FromResult<object?>(BuildEnrichmentResult()),
        };

        await new ChunkOrchestrator().ChunkProcessingOrchestrator(ctx);

        // Deterministic replay depends on the deadline coming off CurrentUtcDateTime, not DateTime.UtcNow.
        Assert.Equal(ctx.CurrentUtcDateTime.AddMinutes(20), Assert.Single(ctx.TimerDeadlines));
    }
}
