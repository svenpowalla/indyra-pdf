// A source of kerning values for glyph pairs.

namespace IndyraPdf.Fonts.Kerning;

/// <summary>Returns the horizontal adjustment between two neighbouring glyphs.</summary>
internal interface IPairAdjustment
{
    /// <summary>
    /// Looks up the adjustment of the advance of <paramref name="left"/> when it is followed by
    /// <paramref name="right"/>, in font units (negative = closer together).
    /// </summary>
    bool TryGetAdjustment(int left, int right, out int adjustment);
}
