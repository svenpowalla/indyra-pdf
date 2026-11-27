// A sequence of glyphs in one font, size and color, with exact positions.
//
// The layout engine decides where every glyph goes (line breaking, alignment, justification).
// Drawing surfaces only have to put the glyphs there. Because the positions are computed once,
// PDF and preview show identical line breaks.

using IndyraPdf.Fonts;
using IndyraPdf.Writer;

namespace IndyraPdf.Drawing;

/// <summary>Glyphs in one font, size and color, positioned along a baseline.</summary>
public sealed class GlyphRun
{
    /// <summary>Creates a glyph run.</summary>
    /// <param name="font">Font of all glyphs.</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="color">Text color.</param>
    /// <param name="x">Start of the run on the baseline.</param>
    /// <param name="baseline">Y coordinate of the baseline.</param>
    /// <param name="glyphs">Glyphs with positions relative to <paramref name="x"/>.</param>
    public GlyphRun(TrueTypeFont font, double size, PdfColor color, double x, double baseline, IReadOnlyList<PositionedGlyph> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        Font = font;
        Size = size;
        Color = color;
        X = x;
        Baseline = baseline;
        Glyphs = glyphs;
    }

    /// <summary>Font of all glyphs.</summary>
    public TrueTypeFont Font { get; }

    /// <summary>Font size in points.</summary>
    public double Size { get; }

    /// <summary>Text color.</summary>
    public PdfColor Color { get; }

    /// <summary>Start of the run on the baseline.</summary>
    public double X { get; }

    /// <summary>Y coordinate of the baseline.</summary>
    public double Baseline { get; }

    /// <summary>Glyphs with positions relative to <see cref="X"/>.</summary>
    public IReadOnlyList<PositionedGlyph> Glyphs { get; }

    /// <summary>Natural advance width of a glyph of this run in points.</summary>
    public double AdvanceOf(int glyphId) => Font.GetAdvanceWidth(glyphId) * Size / Font.UnitsPerEm;
}
