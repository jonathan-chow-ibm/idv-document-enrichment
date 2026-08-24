namespace IdvEnrichment.Functions.Shared;

public static class TextUtils
{
    private const string PageBreakMarker = "<!-- PageBreak -->";

    // Common TOC patterns that consume space without carrying extractable metadata
    private static readonly string[] TocEndMarkers =
    [
        "\nARTICLE 1",
        "\nArticle 1",
        "\n1. ",
        "\n1.",
        "\nSECTION 1",
        "\nSection 1",
    ];

    public static string TruncateForClassification(string text, int maxChars = 4000)
        => TruncatePageAware(text, maxChars);

    public static string TruncateForExtraction(string text, int maxChars = 24000)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        // For long documents: skip TOC, take front content + tail (signature block / exhibits)
        var headBudget = maxChars * 3 / 4; // 18K for the front
        var tailBudget = maxChars / 4;       // 6K for the end

        var contentStart = FindContentStart(text);
        var head = TruncatePageAware(text[contentStart..], headBudget);
        var tail = text[^tailBudget..];

        // Include a short preamble (title/parties) from before the TOC if we skipped it
        var preamble = contentStart > 0 ? text[..Math.Min(contentStart, 1000)] : "";

        return preamble + head + "\n\n[... middle content omitted ...]\n\n" + tail;
    }

    private static int FindContentStart(string text)
    {
        // Look for the end of a TOC section — find the first "ARTICLE 1" or similar marker
        // that appears AFTER the TOC listing (typically 2K+ chars into the document)
        foreach (var marker in TocEndMarkers)
        {
            // Search for the marker after the first 2000 chars to skip TOC references to "Article 1"
            var idx = text.IndexOf(marker, Math.Min(2000, text.Length), StringComparison.Ordinal);
            if (idx > 0)
            {
                // Back up to include the contract header (parties, price) which is between TOC and Article 1
                var headerStart = text.LastIndexOf('\n', idx - 1, Math.Min(idx, 5000));
                // Find where the actual contract body starts (after "TABLE OF CONTENTS" block ends)
                var tocEnd = text.IndexOf("CONTRACT\n", Math.Min(2000, text.Length), StringComparison.OrdinalIgnoreCase);
                if (tocEnd > 0 && tocEnd < idx)
                {
                    return tocEnd;
                }

                return headerStart > 0 ? headerStart : 0;
            }
        }

        return 0; // no TOC detected, start from beginning
    }

    private static string TruncatePageAware(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        var lastBreak = text.LastIndexOf(PageBreakMarker, maxChars, StringComparison.Ordinal);
        if (lastBreak > 0)
        {
            return text[..lastBreak];
        }

        var lastSentenceEnd = text.LastIndexOfAny(['.', '!', '?'], maxChars - 1);
        if (lastSentenceEnd > maxChars / 2)
        {
            return text[..(lastSentenceEnd + 1)];
        }

        return text[..maxChars];
    }
}
