// Entry point of the preview: renders pages of a laid-out document as PNG images.
//
// Meant for applications that show a document as images while it is being edited, e.g. a
// document designer in a web browser. Because the preview draws the same LaidOutDocument as the
// PDF output, both show identical line breaks, page breaks and positions.

using IndyraPdf.Layout;
using SkiaSharp;

namespace IndyraPdf.Skia;

/// <summary>Renders pages of a <see cref="LaidOutDocument"/> as PNG images.</summary>
public static class PngPreview
{
    /// <summary>Screen resolution used by default (96 dpi = one CSS pixel per image pixel, so a page appears in a browser at 100 % size).</summary>
    public const double DefaultDpi = 96;

    /// <summary>Renders one page as PNG.</summary>
    /// <param name="document">The laid-out document.</param>
    /// <param name="pageIndex">Page, starting at 0.</param>
    /// <param name="dpi">Resolution in dots per inch; 96 = screen size, 150–300 for print quality.</param>
    public static byte[] RenderPage(LaidOutDocument document, int pageIndex, double dpi = DefaultDpi)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var resources = new SkiaResourceCache();
        return Render(document, pageIndex, dpi, resources);
    }

    /// <summary>Renders the first <paramref name="maxPages"/> pages (all by default) as PNG.</summary>
    public static IReadOnlyList<byte[]> RenderPages(LaidOutDocument document, double dpi = DefaultDpi, int maxPages = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var resources = new SkiaResourceCache(); // fonts and images are shared by all pages
        return Enumerable.Range(0, Math.Min(document.PageCount, Math.Max(0, maxPages)))
            .Select(i => Render(document, i, dpi, resources))
            .ToList();
    }

    /// <summary>Renders one page with shared resources.</summary>
    static byte[] Render(LaidOutDocument document, int pageIndex, double dpi, SkiaResourceCache resources)
    {
        if (dpi <= 0 || dpi > 1200)
            throw new ArgumentOutOfRangeException(nameof(dpi), "The resolution must be between 1 and 1200 dpi.");

        var size = document.GetPageSize(pageIndex);
        double scale = dpi / Writer.PdfUnits.PointsPerInch;
        int width = Math.Max(1, (int)Math.Round(size.Width * scale));
        int height = Math.Max(1, (int)Math.Round(size.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Skia could not create the bitmap.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White); // paper
        canvas.Scale((float)scale);   // from here on, coordinates are points

        document.DrawPage(pageIndex, new SkiaDrawingSurface(canvas, size.Width, size.Height, resources));

        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
