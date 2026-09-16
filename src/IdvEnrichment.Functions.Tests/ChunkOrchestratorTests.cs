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

        // The timer is created only once the orchestrator actually reaches CreateTimer, so it must be
        // started (not awaited) first -- firing before that would fire on an empty timer list and leave
        // the real timer, created afterward, waiting forever.
        var run = new ChunkOrchestrator().ChunkProcessingOrchestrator(ctx);
        ctx.FireTimers();

        var result = await run;

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

    [Fact]
    public async Task ChunkProcessingOrchestrator_OneDocumentWedged_NextDocumentStartsBeforeItTimesOut()
    {
        // Discriminates the sliding window from a fixed-size Task.WhenAll group: under a group barrier,
        // doc-3 can't start until doc-2's group resolves (either by completing or timing out). Under the
        // sliding window, doc-3 starts the moment doc-1's slot frees up -- before doc-2 ever times out.
        // Pre-firing timers (as an earlier version of this test did) erases that distinction, since it
        // lets doc-2's group resolve immediately either way. Here, timers are only fired after asserting
        // on the state the two implementations would actually leave behind differently.
        var docs = new[]
        {
            new LibraryDocument("doc-1", "a.pdf", "application/pdf", DateTimeOffset.UnixEpoch),
            new LibraryDocument("doc-2", "b.pdf", "application/pdf", DateTimeOffset.UnixEpoch),
            new LibraryDocument("doc-3", "c.pdf", "application/pdf", DateTimeOffset.UnixEpoch),
        };
        var request = new ChunkRequest(
            Documents: docs,
            BatchId: "batch-1",
            Target: new ResolvedSharePointTarget("site", "drive", null, "Site", "Library"),
            MaxConcurrency: 2);

        var ctx = new FakeOrchestrationContext(request)
        {
            // doc-2 is wedged; doc-1 and doc-3 resolve immediately.
            SubOrchestratorHandler = (_, input) =>
            {
                var message = (QueueMessage)input!;
                return message.DocumentId == "doc-2"
                    ? new TaskCompletionSource<object?>().Task
                    : Task.FromResult<object?>(BuildEnrichmentResult() with { DocumentId = message.DocumentId });
            },
        };

        var run = new ChunkOrchestrator().ChunkProcessingOrchestrator(ctx);

        // doc-1 and doc-3 resolve synchronously (Task.FromResult), so the orchestrator runs without a
        // real await until it's left waiting on doc-2 alone -- by which point doc-3 must already have
        // been dispatched under a sliding window. A fixed-size group would still be waiting on doc-2's
        // group (=[doc-1, doc-2]) here and would never have dispatched doc-3 at all.
        Assert.Contains(ctx.DispatchedInputs, i => ((QueueMessage)i!).DocumentId == "doc-3");
        Assert.False(run.IsCompleted);

        ctx.FireTimers();
        var result = await run;

        Assert.Equal(1, result.Errors);
        Assert.Equal("doc-2", Assert.Single(result.FailedDocuments).DocumentId);
        Assert.Equal(2, result.Results.Count);
    }
}
