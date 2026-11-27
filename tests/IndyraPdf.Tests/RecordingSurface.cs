// Test helper: a drawing surface that records all calls instead of drawing.

using IndyraPdf.Drawing;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

/// <summary>Records the calls of the layout engine, so tests can check positions.</summary>
internal sealed class RecordingSurface : IDrawingSurface
{
    /// <summary>Creates a surface of the given size (default A4).</summary>
    public RecordingSurface(double width = 595.2756, double height = 841.8898)
    {
        Width = width;
        Height = height;
    }

    /// <inheritdoc/>
    public double Width { get; }

    /// <inheritdoc/>
    public double Height { get; }

    /// <summary>All glyph runs drawn.</summary>
    public List<GlyphRun> Runs { get; } = [];

    /// <summary>All filled rectangles (backgrounds, decorations).</summary>
    public List<(Box Box, PdfColor Color)> Fills { get; } = [];

    /// <summary>Text of all runs, concatenated.</summary>
    public string Text => string.Concat(Runs.SelectMany(r => r.Glyphs).Select(g => g.Text));

    /// <summary>Right edge of the last glyph of a run (position + natural advance).</summary>
    public static double RightEdge(GlyphRun run)
    {
        var last = run.Glyphs[^1];
        return run.X + last.X + run.AdvanceOf(last.GlyphId);
    }

    /// <inheritdoc/>
    public void SaveState() { }

    /// <inheritdoc/>
    public void RestoreState() { }

    /// <inheritdoc/>
    public void Clip(Box box) { }

    /// <inheritdoc/>
    public void FillRectangle(Box box, PdfColor color) => Fills.Add((box, color));

    /// <inheritdoc/>
    public void StrokeRectangle(Box box, LineStyle style) { }

    /// <inheritdoc/>
    public void DrawLine(double x1, double y1, double x2, double y2, LineStyle style) { }

    /// <inheritdoc/>
    public void DrawGlyphs(GlyphRun run) => Runs.Add(run);

    /// <inheritdoc/>
    public void DrawImage(ImageSource image, Box box) { }
}
