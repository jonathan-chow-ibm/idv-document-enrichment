namespace IdvEnrichment.Functions.Shared;

/// <summary>Per-page signal used to decide whether a PDF is born-digital or a scan.</summary>
internal readonly record struct PageTextInfo(int ExtractedTextLength, bool HasFullPageImage);

internal static class PdfDigitalDetector
{
    // A page with fewer than this many extracted characters is treated as textless. 20 chars is low
    // enough not to penalize sparse-but-real pages, yet high enough to exclude blank/near-empty cover
    // pages (stray whitespace, a page number) from skewing the digital-page ratio.
    private const int MinDigitalPageTextLength = 20;

    // TODO: Per-page splitting/stitching (extract born-digital pages via PdfPig, route only the scanned
    // pages to DI's `Pages` parameter within the same document) is a valid future optimization, but it
    // depends on confirming whether DI's `Pages` parameter actually reduces billing or just limits the
    // response. Until that's verified, classify the whole document one way or the other — never split
    // a single document across both extraction paths.

    /// <summary>
    /// Whole-document, v1 decision: a document is born-digital if at least <paramref name="minDigitalPageRatio"/>
    /// of its pages have a real text layer (as opposed to a scanned image, even one with an OCR text overlay).
    /// </summary>
    internal static bool IsBornDigital(IReadOnlyList<PageTextInfo> pages, double minDigitalPageRatio = 0.9)
    {
        if (pages.Count == 0)
        {
            // No pages to prove a text layer exists; fall through to the safe DI path.
            return false;
        }

        var digitalPageCount = pages.Count(IsDigitalPage);
        return (double)digitalPageCount / pages.Count >= minDigitalPageRatio;
    }

    // A full-page image alongside real text usually means the "text" is an OCR overlay on a scan, not a
    // genuine text layer, so it doesn't count as born-digital.
    private static bool IsDigitalPage(PageTextInfo page) =>
        page.ExtractedTextLength >= MinDigitalPageTextLength && !page.HasFullPageImage;
}
