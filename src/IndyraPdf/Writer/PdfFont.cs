// A font as used inside one PDF document.
//
// PdfFont connects a TrueTypeFont (metrics, outlines) with the PDF output:
//   - it measures text for the caller (layout),
//   - it turns text into glyph codes for the page content (via GlyphSubset),
//   - it builds the font objects when the document is saved (via CidFontWriter).

using System.Globalization;
using System.Text;
using IndyraPdf.Fonts;
using IndyraPdf.Writer.Fonts;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>
/// A TrueType font in a <see cref="PdfDocument"/>. It is embedded as a subset containing only
/// the glyphs used and supports the full Unicode repertoire of the font.
/// </summary>
/// <remarks>
/// Characters missing in the font are replaced by "?". Control characters (below U+0020) are
/// ignored. Use <see cref="HasCharacter"/> to choose a fallback font beforehand.
/// </remarks>
public sealed class PdfFont
{
    /// <summary>Replacement character for characters missing in the font.</summary>
    const char ReplacementCharacter = '?';

    /// <summary>The glyphs used so far and their codes in the page content.</summary>
    readonly GlyphSubset _subset = new();

    /// <summary>Glyph of the replacement character, or 0 (.notdef) if the font lacks it.</summary>
    readonly int _replacementGlyph;

    /// <summary>Creates the font. Only <see cref="PdfDocument.AddFont(TrueTypeFont)"/> calls this.</summary>
    /// <param name="font">The TrueType font.</param>
    /// <param name="resourceName">Name in the page resources, e.g. "F1".</param>
    /// <exception cref="FontFormatException">
    /// The font license forbids embedding, or the font has no outlines (color emoji fonts are
    /// drawn through the layout engine as fallback fonts instead).
    /// </exception>
    internal PdfFont(TrueTypeFont font, string resourceName)
    {
        if (!font.HasOutlines)
            throw new FontFormatException($"The font \"{font.FamilyName}\" has no outlines and cannot be embedded as a font. Use it as a fallback font of the layout engine (FontCollection.AddFallback).");
        if (!font.EmbeddingAllowed)
            throw new FontFormatException($"The license of the font \"{font.FamilyName}\" does not allow embedding.");
        Font = font;
        ResourceName = resourceName;
        _replacementGlyph = font.GetGlyphId(ReplacementCharacter);
    }

    /// <summary>The underlying TrueType font with its metrics.</summary>
    public TrueTypeFont Font { get; }

    /// <summary>PostScript name of the font.</summary>
    public string Name => Font.PostScriptName;

    /// <summary>Name under which pages refer to the font, e.g. "F1" (written as /F1).</summary>
    internal string ResourceName { get; }

    /// <summary>Number of glyphs used so far, including .notdef.</summary>
    public int UsedGlyphCount => _subset.Count;

    /// <summary>Ascender at the given font size in points (positive).</summary>
    public double GetAscent(double size) => ToPoints(Font.Ascender, size);

    /// <summary>Descender at the given font size in points (negative).</summary>
    public double GetDescent(double size) => ToPoints(Font.Descender, size);

    /// <summary>Recommended line height at the given font size in points.</summary>
    public double GetLineHeight(double size) => ToPoints(Font.LineHeight, size);

    /// <summary>Whether the font contains a Unicode character.</summary>
    public bool HasCharacter(int codePoint) => Font.HasCharacter(codePoint);

    /// <summary>Width of a text at the given font size in points.</summary>
    public double MeasureText(string text, double size)
    {
        ArgumentNullException.ThrowIfNull(text);
        long units = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsControlCharacter(rune))
                units += Font.GetAdvanceWidth(ResolveGlyph(rune));
        }
        return ToPoints(units, size);
    }

    /// <summary>
    /// Converts text into the hexadecimal glyph codes for the page content (four hex digits per
    /// glyph) and records the glyphs for the subset.
    /// </summary>
    internal string EncodeAsHex(string text)
    {
        var sb = new StringBuilder(text.Length * 4);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsControlCharacter(rune))
                continue;
            int glyph = ResolveGlyph(rune);
            string unicode = Font.HasCharacter(rune.Value) ? rune.ToString() : ReplacementCharacter.ToString();
            int code = _subset.Use(glyph, unicode);
            sb.Append(code.ToString("X4", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Records a single glyph for the subset and returns its code (four hex digits).
    /// Used for glyph runs positioned by the layout engine.
    /// </summary>
    /// <param name="glyphId">Glyph number in the TrueType font.</param>
    /// <param name="text">Text the glyph represents (for the ToUnicode table).</param>
    internal string EncodeGlyphAsHex(int glyphId, string text)
        => _subset.Use(glyphId, text).ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Creates the PDF objects of the font and returns the font dictionary.</summary>
    internal PdfDictionary Build(PdfObjectTable table, bool compress)
        => new CidFontWriter(Font, _subset, compress).Write(table);

    /// <summary>Glyph for a character, or the replacement glyph if the font lacks the character.</summary>
    int ResolveGlyph(Rune rune)
    {
        int glyph = Font.GetGlyphId(rune.Value);
        return glyph != 0 ? glyph : _replacementGlyph;
    }

    /// <summary>Control characters (tab, line feed, …) have no glyph and are skipped.</summary>
    static bool IsControlCharacter(Rune rune) => rune.Value < 0x20;

    /// <summary>Converts font units to points at the given font size.</summary>
    double ToPoints(double units, double size) => units * size / Font.UnitsPerEm;
}
