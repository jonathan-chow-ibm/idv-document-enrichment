using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Orchestrators;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class BatchOrchestratorTests
{
    private static LibraryDocument BuildDoc(string id, string name) =>
        new(Id: id, Name: name, MimeType: "application/pdf", LastModifiedDateTime: DateTimeOffset.UnixEpoch);

    [Fact]
    public void FilterByItemIds_NullItemIds_ReturnsAllDocuments()
    {
        var documents = new[] { BuildDoc("1", "a.pdf"), BuildDoc("2", "b.pdf") };

        var result = BatchOrchestrator.FilterByItemIds(documents, itemIds: null);

        Assert.Same(documents, result);
    }

    [Fact]
    public void FilterByItemIds_EmptyItemIds_ReturnsAllDocuments()
    {
        var documents = new[] { BuildDoc("1", "a.pdf"), BuildDoc("2", "b.pdf") };

        var result = BatchOrchestrator.FilterByItemIds(documents, itemIds: []);

        Assert.Same(documents, result);
    }

    [Fact]
    public void FilterByItemIds_WithMatchingIds_ReturnsOnlyRequestedDocuments()
    {
        var documents = new[] { BuildDoc("1", "a.pdf"), BuildDoc("2", "b.pdf"), BuildDoc("3", "c.pdf") };

        var result = BatchOrchestrator.FilterByItemIds(documents, itemIds: ["2", "3"]);

        Assert.Equal(["2", "3"], result.Select(d => d.Id));
    }

    [Fact]
    public void FilterByItemIds_IdsCaseSensitive_DoesNotMatchDifferentCasing()
    {
        var documents = new[] { BuildDoc("ABC", "a.pdf") };

        var result = BatchOrchestrator.FilterByItemIds(documents, itemIds: ["abc"]);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterByItemIds_NoMatchingIds_ReturnsEmpty()
    {
        var documents = new[] { BuildDoc("1", "a.pdf"), BuildDoc("2", "b.pdf") };

        var result = BatchOrchestrator.FilterByItemIds(documents, itemIds: ["missing"]);

        Assert.Empty(result);
    }
}
