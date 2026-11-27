// Records which glyphs of a font are used in a document and assigns their codes.
//
// The page content does not contain characters but glyph codes. IndyraPdf numbers the glyphs
// in the order of their first use: .notdef = 0, the first glyph used = 1, and so on. The subset
// font is later built with exactly this numbering, so code = glyph number in the subset font.
// In PDF terms the code is the "CID" (character identifier) of a CID font.

namespace IndyraPdf.Writer.Fonts;

/// <summary>
/// The glyphs of a font used so far, numbered in the order of their first use.
/// </summary>
internal sealed class GlyphSubset
{
    /// <summary>Glyph number in the source font → code (CID).</summary>
    readonly Dictionary<int, int> _codeByGlyph = new() { [0] = 0 };

    /// <summary>Code (CID) → glyph number in the source font. Code 0 is always .notdef.</summary>
    readonly List<int> _glyphByCode = [0];

    /// <summary>Code (CID) → Unicode text, for the ToUnicode table.</summary>
    readonly Dictionary<int, string> _unicodeByCode = [];

    /// <summary>Number of glyphs, including .notdef.</summary>
    public int Count => _glyphByCode.Count;

    /// <summary>Source glyph numbers in code order (index = code).</summary>
    public IReadOnlyList<int> Glyphs => _glyphByCode;

    /// <summary>Unicode text per code, for the ToUnicode table.</summary>
    public IReadOnlyDictionary<int, string> UnicodeByCode => _unicodeByCode;

    /// <summary>
    /// Records a glyph and returns its code. The first text mapped to a glyph determines its
    /// Unicode meaning (relevant when several characters share one glyph).
    /// </summary>
    /// <param name="glyph">Glyph number in the source font.</param>
    /// <param name="unicode">Text the glyph stands for.</param>
    public int Use(int glyph, string unicode)
    {
        if (!_codeByGlyph.TryGetValue(glyph, out int code))
        {
            code = _glyphByCode.Count;
            _codeByGlyph[glyph] = code;
            _glyphByCode.Add(glyph);
        }
        if (code != 0)
            _unicodeByCode.TryAdd(code, unicode);
        return code;
    }
}
