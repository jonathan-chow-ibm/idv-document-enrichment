using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;

namespace IdvEnrichment.Functions.Shared;

public static class PdfPageRenderer
{
    private const int MaxWidthPx = 1568;
    private const int MaxHeightPx = 2048;

    /// <summary>Renders the first page of a PDF to a PNG byte array.</summary>
    public static byte[] RenderFirstPageToPng(byte[] pdfBytes)
    {
        using var docLib = DocLib.Instance;
        using var docReader = docLib.GetDocReader(pdfBytes, new PageDimensions(MaxWidthPx, MaxHeightPx));
        using var pageReader = docReader.GetPageReader(0);

        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var rawPixels = pageReader.GetImage(); // BGRA

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        System.Runtime.InteropServices.Marshal.Copy(rawPixels, 0, bitmap.GetPixels(), rawPixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        return data.ToArray();
    }
}
