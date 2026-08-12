namespace IdvEnrichment.Functions.Shared;

public static class TextUtils
{
    // Doc Intelligence markdown output uses this comment tag between pages
    private const string PageBreakMarker = "<!-- PageBreak -->";

    public static string TruncateForClassification(string text, int maxChars = 4000)
        => TruncatePageAware(text, maxChars);

    public static string TruncateForExtraction(string text, int maxChars = 8000)
        => TruncatePageAware(text, maxChars);

    private static string TruncatePageAware(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        // Try to cut at the last page break within budget
        var lastBreak = text.LastIndexOf(PageBreakMarker, maxChars, StringComparison.Ordinal);
        if (lastBreak > 0)
        {
            return text[..lastBreak];
        }

        // Fall back to last sentence boundary within budget
        var lastSentenceEnd = text.LastIndexOfAny(['.', '!', '?'], maxChars - 1);
        if (lastSentenceEnd > maxChars / 2)
        {
            return text[..(lastSentenceEnd + 1)];
        }

        // Hard cut as last resort
        return text[..maxChars];
    }
}
