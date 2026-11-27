// All kerning data of a font, combined from the GPOS lookups of the "kern" feature or from
// the legacy "kern" table.

namespace IndyraPdf.Fonts.Kerning;

/// <summary>Kerning of a font: adjustments between neighbouring glyphs.</summary>
internal sealed class KerningTable
{
    /// <summary>
    /// Pair adjustments grouped by OpenType lookup. Within a lookup the first subtable that
    /// covers the pair decides; the results of different lookups add up.
    /// </summary>
    readonly IReadOnlyList<IReadOnlyList<IPairAdjustment>> _lookups;

    /// <summary>Creates the table.</summary>
    public KerningTable(IReadOnlyList<IReadOnlyList<IPairAdjustment>> lookups) => _lookups = lookups;

    /// <summary>A table without kerning.</summary>
    public static KerningTable Empty { get; } = new([]);

    /// <summary>Whether the font has kerning at all.</summary>
    public bool IsEmpty => _lookups.Count == 0;

    /// <summary>Adjustment in font units between two glyphs (0 if none).</summary>
    public int GetAdjustment(int left, int right)
    {
        int total = 0;
        foreach (var lookup in _lookups)
        {
            foreach (var subtable in lookup)
            {
                if (subtable.TryGetAdjustment(left, right, out int value))
                {
                    total += value;
                    break;
                }
            }
        }
        return total;
    }
}
