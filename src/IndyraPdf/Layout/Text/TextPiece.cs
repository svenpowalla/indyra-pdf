// The smallest unit of line breaking: a word part, spaces or a forced break, in one style.

using IndyraPdf.Fonts;

namespace IndyraPdf.Layout.Text;

/// <summary>
/// A piece of shaped text in one style. A word that changes its format in the middle
/// (e.g. "<b>bold</b>er") consists of several pieces without break opportunity between them.
/// </summary>
internal sealed class TextPiece
{
    /// <summary>Creates a piece.</summary>
    /// <param name="kind">Kind of the piece.</param>
    /// <param name="style">Character format.</param>
    /// <param name="font">Font of the glyphs (the style font or a fallback font).</param>
    /// <param name="metricsFont">Font of the style, which determines line height and decorations.</param>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="breakAfter">Whether a line may break after this piece.</param>
    public TextPiece(PieceKind kind, TextStyle style, TrueTypeFont font, TrueTypeFont metricsFont, IReadOnlyList<ShapedGlyph> glyphs, bool breakAfter = false)
    {
        Kind = kind;
        Style = style;
        Font = font;
        MetricsFont = metricsFont;
        Glyphs = glyphs;
        BreakAfter = breakAfter;
        Width = glyphs.Sum(g => g.Advance);
    }

    /// <summary>Kind of the piece.</summary>
    public PieceKind Kind { get; }

    /// <summary>Character format.</summary>
    public TextStyle Style { get; }

    /// <summary>Font of the glyphs: the font of the style or, for missing characters, a fallback font.</summary>
    public TrueTypeFont Font { get; }

    /// <summary>
    /// Font of the style. Line height, highlight and underline follow this font even where a
    /// fallback font draws the glyphs, so an emoji does not change the line spacing.
    /// </summary>
    public TrueTypeFont MetricsFont { get; }

    /// <summary>The glyphs.</summary>
    public IReadOnlyList<ShapedGlyph> Glyphs { get; }

    /// <summary>Whether a line may break after this piece although no space follows (after a hyphen).</summary>
    public bool BreakAfter { get; }

    /// <summary>Total advance width in points.</summary>
    public double Width { get; }
}
