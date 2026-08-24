using IdvEnrichment.Functions.Shared;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class TextUtilsTests
{
    [Fact]
    public void TruncateForExtraction_ShortText_ReturnsUnchanged()
    {
        var text = new string('A', 24000);
        Assert.Equal(text, TextUtils.TruncateForExtraction(text));
    }

    [Fact]
    public void TruncateForExtraction_ExactlyAtLimit_ReturnsUnchanged()
    {
        var text = new string('A', 24000);
        Assert.Same(text, TextUtils.TruncateForExtraction(text));
    }

    [Fact]
    public void TruncateForClassification_ShortText_ReturnsUnchanged()
    {
        var text = new string('A', 4000);
        Assert.Equal(text, TextUtils.TruncateForClassification(text));
    }

    [Fact]
    public void TruncateForExtraction_LongText_ContainsOmissionMarker()
    {
        var text = new string('A', 30000);
        var result = TextUtils.TruncateForExtraction(text);
        Assert.Contains("[... middle content omitted ...]", result);
    }

    [Fact]
    public void TruncateForExtraction_TextWithTocAndArticle1_PreamblePrepended()
    {
        // preamble (500 A's) + TOC block (2000 B's) + content starting at ARTICLE 1 (30000 C's)
        var preamble500 = new string('A', 500);
        var input = preamble500
            + "\nTABLE OF CONTENTS\n"
            + new string('B', 2000)
            + "\nARTICLE 1\n"
            + new string('C', 30000);

        var result = TextUtils.TruncateForExtraction(input);

        // Preamble must appear at the start and the middle marker must be present
        Assert.StartsWith("A", result);
        Assert.Contains("[... middle content omitted ...]", result);
        // The output is longer than a plain headBudget (18000) because the preamble is prepended
        Assert.True(result.Length > 18000);
    }

    [Fact]
    public void TruncateForExtraction_NoTocMarkers_StartsFromBeginning()
    {
        var text = new string('X', 30000);
        var result = TextUtils.TruncateForExtraction(text);
        Assert.StartsWith("X", result);
    }
}
