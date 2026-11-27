// Reads the component list of composite glyphs in the TrueType "glyf" table.

namespace IndyraPdf.Fonts;

/// <summary>
/// Finds the components of a composite glyph (a glyph assembled from other glyphs,
/// e.g. "Ä" = "A" + dieresis).
/// </summary>
/// <remarks>
/// Layout of a glyph in "glyf": numberOfContours (i16) and the bounding box (4 × i16),
/// 10 bytes in total. If numberOfContours is negative, a list of components follows. Each one
/// starts with flags (u16) and glyphIndex (u16). Then come the offset (2 × 1 or 2 × 2 bytes) and
/// optionally a scale (2, 4 or 8 bytes). The flags tell which parts are present and whether
/// another component follows.
/// </remarks>
internal static class CompositeGlyph
{
    // Flag bits of a component (OpenType specification, table "glyf").
    const int Arg1And2AreWords = 0x0001;   // offset stored as 2 × 16 bit instead of 2 × 8 bit
    const int WeHaveAScale = 0x0008;       // one scale factor (2 bytes)
    const int MoreComponents = 0x0020;     // another component follows
    const int WeHaveAnXAndYScale = 0x0040; // separate x and y scale (4 bytes)
    const int WeHaveATwoByTwo = 0x0080;    // 2×2 transformation matrix (8 bytes)

    /// <summary>
    /// Returns, for every component, the position of its glyph reference inside the glyph data and
    /// the referenced glyph number. The list is empty for simple glyphs.
    /// </summary>
    public static IReadOnlyList<GlyphComponent> GetComponents(ReadOnlySpan<byte> glyph)
    {
        var result = new List<GlyphComponent>();
        if (glyph.Length < 10 || Internal.BigEndian.I16(glyph, 0) >= 0)
            return result;

        int offset = 10;
        int flags;
        do
        {
            flags = Internal.BigEndian.U16(glyph, offset);
            result.Add(new GlyphComponent(offset + 2, Internal.BigEndian.U16(glyph, offset + 2)));
            offset += 4;                                            // flags + glyphIndex
            offset += (flags & Arg1And2AreWords) != 0 ? 4 : 2;      // x/y offset
            if ((flags & WeHaveAScale) != 0)
                offset += 2;
            else if ((flags & WeHaveAnXAndYScale) != 0)
                offset += 4;
            else if ((flags & WeHaveATwoByTwo) != 0)
                offset += 8;
        }
        while ((flags & MoreComponents) != 0 && offset + 4 <= glyph.Length);
        return result;
    }
}
