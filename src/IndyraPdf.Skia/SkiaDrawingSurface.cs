// Output 2 of the layout engine: draws onto a SkiaSharp canvas (bitmap).
//
// The layout engine works in points with the origin in the upper left corner – the same
// orientation as Skia. A scale transformation converts points into pixels (dpi / 72), so the
// drawing code below uses points throughout.
//
// Text is drawn glyph by glyph at the positions computed by the layout engine (SKTextBlob with
// positioned glyphs). Skia does not shape or measure anything itself, so line breaks and
// alignment are identical to the PDF.

using IndyraPdf.Drawing;
using IndyraPdf.Writer;
using SkiaSharp;

namespace IndyraPdf.Skia;

/// <summary>Implements <see cref="IDrawingSurface"/> on a SkiaSharp canvas.</summary>
internal sealed class SkiaDrawingSurface : IDrawingSurface
{
    /// <summary>Target canvas.</summary>
    readonly SKCanvas _canvas;

    /// <summary>Typefaces and images.</summary>
    readonly SkiaResourceCache _resources;

    /// <summary>Creates a surface for one page.</summary>
    /// <param name="canvas">Canvas already scaled from points to pixels.</param>
    /// <param name="width">Page width in points.</param>
    /// <param name="height">Page height in points.</param>
    /// <param name="resources">Shared typefaces and images.</param>
    public SkiaDrawingSurface(SKCanvas canvas, double width, double height, SkiaResourceCache resources)
    {
        _canvas = canvas;
        _resources = resources;
        Width = width;
        Height = height;
    }

    /// <inheritdoc/>
    public double Width { get; }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void SaveState() => _canvas.Save();

    /// <inheritdoc/>
    public void RestoreState() => _canvas.Restore();

    /// <inheritdoc/>
    public void Clip(Box box) => _canvas.ClipRect(Rect(box), SKClipOperation.Intersect, antialias: true);

    /// <inheritdoc/>
    public void FillRectangle(Box box, PdfColor color)
    {
        using var paint = new SKPaint { Color = SkiaColors.ToSkia(color), Style = SKPaintStyle.Fill, IsAntialias = true };
        _canvas.DrawRect(Rect(box), paint);
    }

    /// <inheritdoc/>
    public void StrokeRectangle(Box box, LineStyle style)
    {
        using var paint = StrokePaint(style);
        _canvas.DrawRect(Rect(box), paint);
    }

    /// <inheritdoc/>
    public void DrawLine(double x1, double y1, double x2, double y2, LineStyle style)
    {
        using var paint = StrokePaint(style);
        _canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2, paint);
    }

    /// <inheritdoc/>
    public void DrawGlyphs(GlyphRun run)
    {
        if (run.Glyphs.Count == 0)
            return;
        if (run.Font.HasColorBitmaps)
        {
            DrawColorGlyphs(run);
            return;
        }

        // Font settings for faithful positions: no hinting (it would move glyphs to whole pixels)
        // and subpixel positioning, so the glyphs land exactly where the layout engine put them.
        // Grayscale antialiasing: LCD subpixel rendering would leave coloured fringes in a PNG.
        using var font = new SKFont(_resources.Typeface(run.Font), (float)run.Size)
        {
            Subpixel = true,
            Hinting = SKFontHinting.None,
            Edging = SKFontEdging.Antialias,
            LinearMetrics = true,
        };

        var glyphs = new ushort[run.Glyphs.Count];
        var positions = new SKPoint[run.Glyphs.Count];
        for (int i = 0; i < glyphs.Length; i++)
        {
            glyphs[i] = (ushort)run.Glyphs[i].GlyphId;
            positions[i] = new SKPoint((float)(run.X + run.Glyphs[i].X), (float)run.Baseline);
        }

        using var builder = new SKTextBlobBuilder();
        builder.AddPositionedRun(glyphs, font, positions);
        using var blob = builder.Build();
        using var paint = new SKPaint { Color = SkiaColors.ToSkia(run.Color), IsAntialias = true };
        _canvas.DrawText(blob, 0, 0, paint);
    }

    /// <inheritdoc/>
    public void DrawImage(ImageSource image, Box box)
    {
        // Cubic resampling (Mitchell) keeps scaled-down logos sharp.
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        _canvas.DrawImage(_resources.Image(image), Rect(box), sampling, null);
    }

    /// <summary>
    /// Draws the glyphs of a color emoji font as their pictures, placed exactly as in the PDF
    /// (<see cref="Fonts.ColorBitmap.Placement"/>). The opacity of the text color applies, as in the PDF.
    /// </summary>
    void DrawColorGlyphs(GlyphRun run)
    {
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var paint = new SKPaint { Color = SKColors.Black.WithAlpha(SkiaColors.ToSkia(run.Color).Alpha) };
        foreach (var glyph in run.Glyphs)
        {
            if (run.Font.GetColorBitmap(glyph.GlyphId) is not { } bitmap)
                continue;
            var (left, top, width, height) = bitmap.Placement(run.Size);
            var box = new Box(run.X + glyph.X + left, run.Baseline - top, width, height);
            _canvas.DrawImage(_resources.ColorGlyph(bitmap), Rect(box), sampling, paint);
        }
    }

    /// <summary>Converts a box into a Skia rectangle (in points; the canvas scales to pixels).</summary>
    static SKRect Rect(Box box) => SKRect.Create((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);

    /// <summary>Paint for lines: width, colour and dash pattern; butt line ends as in the PDF output.</summary>
    static SKPaint StrokePaint(LineStyle style)
    {
        var paint = new SKPaint
        {
            Color = SkiaColors.ToSkia(style.Color),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)style.Width,
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = true,
        };
        if (style.DashPattern is { Count: > 0 } dash)
            paint.PathEffect = SKPathEffect.CreateDash(dash.Select(d => (float)d).ToArray(), 0);
        return paint;
    }
}
