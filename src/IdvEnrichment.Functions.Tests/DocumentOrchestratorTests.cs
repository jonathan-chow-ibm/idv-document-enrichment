using IdvEnrichment.Functions.Orchestrators;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class DocumentOrchestratorTests
{
    [Fact]
    public void ResolveMaxPages_PositiveValue_ReturnsIt()
    {
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages: 3, classifyOnly: false);

        Assert.Equal(3, resolved);
    }

    [Fact]
    public void ResolveMaxPages_PositiveValue_ClassifyOnly_ReturnsIt()
    {
        // An explicit override always wins over the classify-only default, regardless of its value.
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages: 5, classifyOnly: true);

        Assert.Equal(5, resolved);
    }

    [Fact]
    public void ResolveMaxPages_Null_ClassifyOnly_DefaultsToOne()
    {
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages: null, classifyOnly: true);

        Assert.Equal(1, resolved);
    }

    [Fact]
    public void ResolveMaxPages_Null_NotClassifyOnly_ReturnsNull()
    {
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages: null, classifyOnly: false);

        Assert.Null(resolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveMaxPages_ZeroOrNegative_ClassifyOnly_DefaultsToOne(int requestedMaxPages)
    {
        // Zero/negative MaxPages carries no page limit, same as null -- it must not be treated as an
        // explicit override and skip the classify-only default, which is exactly what a bare
        // "message.MaxPages ?? (classifyOnly ? 1 : null)" would do (?? only substitutes on null).
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages, classifyOnly: true);

        Assert.Equal(1, resolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveMaxPages_ZeroOrNegative_NotClassifyOnly_ReturnsNull(int requestedMaxPages)
    {
        var resolved = DocumentOrchestrator.ResolveMaxPages(requestedMaxPages, classifyOnly: false);

        Assert.Null(resolved);
    }

    [Fact]
    public void IsExcludedByTypeConfig_TypeDisabled_ReturnsTrue()
    {
        var excluded = DocumentOrchestrator.IsExcludedByTypeConfig(
            skipExtraction: false, classifyOnly: false, extractionEnabledForType: false);

        Assert.True(excluded);
    }

    [Fact]
    public void IsExcludedByTypeConfig_TypeEnabled_ReturnsFalse()
    {
        var excluded = DocumentOrchestrator.IsExcludedByTypeConfig(
            skipExtraction: false, classifyOnly: false, extractionEnabledForType: true);

        Assert.False(excluded);
    }

    [Fact]
    public void IsExcludedByTypeConfig_AlreadySkippingForAnotherReason_NeverTakesPrecedence()
    {
        // The type-config exclusion is the weakest signal -- it must not overwrite a skip that already
        // happened for low confidence or DocumentType.Other; those keep their own (Review) routing.
        var excluded = DocumentOrchestrator.IsExcludedByTypeConfig(
            skipExtraction: true, classifyOnly: false, extractionEnabledForType: false);

        Assert.False(excluded);
    }

    [Fact]
    public void IsExcludedByTypeConfig_ClassifyOnlyRun_NeverFires()
    {
        // Classify-only runs must stay non-terminal regardless of the resolved type's extraction policy --
        // RecordProcessingResult always writes "classified-only" for these, never "success" or "review".
        var excluded = DocumentOrchestrator.IsExcludedByTypeConfig(
            skipExtraction: false, classifyOnly: true, extractionEnabledForType: false);

        Assert.False(excluded);
    }
}
