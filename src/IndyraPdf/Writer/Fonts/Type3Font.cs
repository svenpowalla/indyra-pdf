// Glyphs of a color emoji font, collected for one PDF Type3 font.
//
// A color bitmap font (Noto Color Emoji) cannot be embedded as a font program: it has no outlines.
// Instead IndyraPdf builds a "Type3 font" (ISO 32000-2, 9.6.4), whose glyphs are small content
// streams – here: "draw this picture". The page content shows the emoji as ordinary text
// (BT … TJ … ET), so viewers can copy, search and read it aloud through the ToUnicode table.
//
// Type3 fonts are simple fonts: every code in the page content is one byte, so one Type3 font
// holds at most 254 glyphs (codes 1–255 without 32). A document with more different emoji uses
// several Type3 fonts (resource names T1, T2, …); PdfDocument hands out the codes.
//
// Neither FPDF nor PDFsharp 6.2.4 can draw color fonts; this follows ISO 32000-2 directly.

using IndyraPdf.Fonts;

namespace IndyraPdf.Writer.Fonts;

/// <summary>Up to 254 glyphs of a color bitmap font, with their one-byte codes and texts.</summary>
internal sealed class Type3Font
{
    /// <summary>Highest one-byte code.</summary>
    const int LastCode = 255;

    /// <summary>
    /// Code 32 is skipped: in simple fonts the word spacing operator Tw applies to it, which would
    /// shift an emoji like a space.
    /// </summary>
    const int SpaceCode = 32;

    /// <summary>Code of every glyph used so far.</summary>
    readonly Dictionary<int, int> _codeByGlyph = [];

    /// <summary>Glyph and text of every code used so far.</summary>
    readonly SortedDictionary<int, (int GlyphId, string Text)> _glyphByCode = [];

    /// <summary>Code given to the next new glyph.</summary>
    int _nextCode = 1;

    /// <summary>Creates an empty Type3 font. Only <see cref="PdfDocument"/> calls this.</summary>
    /// <param name="font">The color bitmap font the glyphs come from.</param>
    /// <param name="resourceName">Name in the page resources, e.g. "T1".</param>
    public Type3Font(TrueTypeFont font, string resourceName)
    {
        Font = font;
        ResourceName = resourceName;
    }

    /// <summary>The color bitmap font the glyphs come from.</summary>
    public TrueTypeFont Font { get; }

    /// <summary>Name under which pages refer to the font, e.g. "T1" (written as /T1).</summary>
    public string ResourceName { get; }

    /// <summary>Glyph and text of every code, sorted by code.</summary>
    public IReadOnlyDictionary<int, (int GlyphId, string Text)> GlyphsByCode => _glyphByCode;

    /// <summary>
    /// Returns the code of a glyph, assigning a new one on first use. Returns false if the glyph
    /// is new and all codes are taken; the caller then uses another Type3 font.
    /// </summary>
    /// <param name="glyphId">Glyph number in the color font.</param>
    /// <param name="text">Text the glyph stands for (kept from its first use, for ToUnicode).</param>
    /// <param name="code">The one-byte code.</param>
    public bool TryUse(int glyphId, string text, out int code)
    {
        if (_codeByGlyph.TryGetValue(glyphId, out code))
            return true;
        if (_nextCode > LastCode)
            return false;
        code = _nextCode++;
        if (_nextCode == SpaceCode)
            _nextCode++;
        _codeByGlyph[glyphId] = code;
        _glyphByCode[code] = (glyphId, text);
        return true;
    }
}
