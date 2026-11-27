// The list marker of a paragraph ("•", "1."), shaped once when the paragraph is formatted.

using IndyraPdf.Fonts;

namespace IndyraPdf.Layout.Text;

/// <summary>A shaped list marker.</summary>
internal sealed class ShapedMarker
{
    /// <summary>Shapes the marker text in the paragraph's default style.</summary>
    public ShapedMarker(string text, TrueTypeFont font, TextStyle style)
    {
        Font = font;
        Style = style;
        Glyphs = TextShaper.Shape(text, font, style.FontSize, style.Kerning);
    }

    /// <summary>Font of the marker.</summary>
    public TrueTypeFont Font { get; }

    /// <summary>Style of the marker (size, color).</summary>
    public TextStyle Style { get; }

    /// <summary>The glyphs.</summary>
    public IReadOnlyList<ShapedGlyph> Glyphs { get; }
}
