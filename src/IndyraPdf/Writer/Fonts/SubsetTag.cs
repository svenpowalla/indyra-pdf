// Prefix that marks embedded subset fonts, e.g. "XPQSJJ+LiberationSans".

namespace IndyraPdf.Writer.Fonts;

/// <summary>
/// Creates the six-letter tag that precedes the name of a subset font (ISO 32000-2, 9.9.2).
/// Different subsets of the same font get different tags, so PDF readers do not mix them up.
/// </summary>
internal static class SubsetTag
{
    /// <summary>Number of letters in the tag.</summary>
    const int Length = 6;

    /// <summary>
    /// Derives the tag from the glyph list (FNV-1a hash), so the same subset always gets the same
    /// tag and the output stays reproducible.
    /// </summary>
    public static string Create(IEnumerable<int> glyphs)
    {
        const uint fnvOffsetBasis = 2166136261;
        const uint fnvPrime = 16777619;

        uint hash = fnvOffsetBasis;
        foreach (var glyph in glyphs)
            hash = unchecked((hash ^ (uint)glyph) * fnvPrime);

        var tag = new char[Length];
        for (int i = 0; i < Length; i++)
        {
            tag[i] = (char)('A' + hash % 26);
            hash /= 26;
        }
        return new string(tag);
    }
}
