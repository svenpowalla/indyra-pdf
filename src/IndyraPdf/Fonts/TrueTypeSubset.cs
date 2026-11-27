// Result of TrueTypeSubsetter.Build: the bytes of the subset font and its glyph list.

namespace IndyraPdf.Fonts;

/// <summary>A subset font created by <see cref="TrueTypeSubsetter"/>.</summary>
internal sealed class TrueTypeSubset
{
    /// <summary>Creates the result.</summary>
    /// <param name="fontProgram">Bytes of the new TrueType file.</param>
    /// <param name="glyphs">Glyph numbers of the source font; index = new glyph number.</param>
    public TrueTypeSubset(byte[] fontProgram, IReadOnlyList<int> glyphs)
    {
        FontProgram = fontProgram;
        Glyphs = glyphs;
    }

    /// <summary>Bytes of the new TrueType file ("font program" in PDF terms).</summary>
    public byte[] FontProgram { get; }

    /// <summary>
    /// Glyph numbers of the source font; the index is the new glyph number. Contains the requested
    /// glyphs first, followed by components of composite glyphs that were added automatically.
    /// </summary>
    public IReadOnlyList<int> Glyphs { get; }
}
