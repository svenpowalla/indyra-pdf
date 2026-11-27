// Part of a text line in one style, placed at a horizontal position.

using IndyraPdf.Fonts;

namespace IndyraPdf.Layout.Text;

/// <summary>Glyphs of one style within a line, starting at <see cref="X"/>.</summary>
internal sealed class LineSegment
{
    /// <summary>Creates a segment.</summary>
    /// <param name="style">Character format.</param>
    /// <param name="font">Font of the glyphs.</param>
    /// <param name="metricsFont">Font of the style, for line height and decorations.</param>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="x">Start position relative to the start of the line.</param>
    /// <param name="isSpace">Whether the segment consists of spaces.</param>
    public LineSegment(TextStyle style, TrueTypeFont font, TrueTypeFont metricsFont, IReadOnlyList<ShapedGlyph> glyphs, double x, bool isSpace)
    {
        Style = style;
        Font = font;
        MetricsFont = metricsFont;
        Glyphs = glyphs;
        X = x;
        IsSpace = isSpace;
        Width = glyphs.Sum(g => g.Advance);
    }

    /// <summary>Character format.</summary>
    public TextStyle Style { get; }

    /// <summary>Font of the glyphs (style font or fallback font).</summary>
    public TrueTypeFont Font { get; }

    /// <summary>Font of the style; line height, highlight and decorations follow it.</summary>
    public TrueTypeFont MetricsFont { get; }

    /// <summary>The glyphs.</summary>
    public IReadOnlyList<ShapedGlyph> Glyphs { get; }

    /// <summary>Start position, relative to the start of the line, before justification.</summary>
    public double X { get; }

    /// <summary>Width in points, before justification.</summary>
    public double Width { get; }

    /// <summary>Whether the segment consists of spaces (these are widened for justified text).</summary>
    public bool IsSpace { get; }

    /// <summary>Ascender of the style font at the segment's font size (positive).</summary>
    public double Ascent => MetricsFont.Ascender * Style.FontSize / MetricsFont.UnitsPerEm;

    /// <summary>Descender of the style font at the segment's font size (negative).</summary>
    public double Descent => MetricsFont.Descender * Style.FontSize / MetricsFont.UnitsPerEm;

    /// <summary>Line gap of the style font at the segment's font size.</summary>
    public double LineGap => MetricsFont.LineGap * Style.FontSize / MetricsFont.UnitsPerEm;
}
