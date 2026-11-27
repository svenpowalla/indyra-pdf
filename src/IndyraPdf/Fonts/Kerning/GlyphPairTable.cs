// Kerning values stored per individual glyph pair (GPOS pair adjustment format 1 and the
// legacy "kern" table).

namespace IndyraPdf.Fonts.Kerning;

/// <summary>Kerning values for individual glyph pairs.</summary>
internal sealed class GlyphPairTable : IPairAdjustment
{
    /// <summary>Pair key (left glyph in the upper 16 bits, right glyph in the lower 16 bits) → adjustment.</summary>
    readonly Dictionary<int, int> _pairs = [];

    /// <summary>Number of pairs.</summary>
    public int Count => _pairs.Count;

    /// <summary>Adds a pair; the first value for a pair wins (as in OpenType, the first matching subtable applies).</summary>
    public void Add(int left, int right, int adjustment) => _pairs.TryAdd(Key(left, right), adjustment);

    /// <inheritdoc/>
    public bool TryGetAdjustment(int left, int right, out int adjustment) => _pairs.TryGetValue(Key(left, right), out adjustment);

    /// <summary>Combines two glyph numbers (each below 65536) into one key.</summary>
    static int Key(int left, int right) => (left << 16) | right;
}
