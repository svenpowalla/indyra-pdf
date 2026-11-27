// Common structures of the OpenType layout tables (GPOS, GSUB): coverage, class definitions,
// value records. See the OpenType specification, chapter "OpenType Layout Common Table Formats".

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts;

/// <summary>Readers for structures shared by the OpenType layout tables.</summary>
internal static class OpenTypeCommon
{
    /// <summary>
    /// Lookup type "extension" (GSUB type 7, GPOS type 9): a subtable that only holds the real
    /// lookup type and a 32-bit offset, so large fonts can place subtables far away.
    /// </summary>
    public const int GsubExtension = 7;

    /// <summary>GPOS lookup type "extension", see <see cref="GsubExtension"/>.</summary>
    public const int GposExtension = 9;

    /// <summary>
    /// Returns the indices of all lookups that a feature (e.g. "kern" or "ccmp") uses, in
    /// ascending order, which is the order in which they must be applied.
    /// </summary>
    /// <remarks>
    /// GSUB and GPOS share the header: version (4), scriptListOffset (2), featureListOffset (2),
    /// lookupListOffset (2). The feature list holds a count (2), then per feature its tag (4)
    /// and offset (2). A feature consists of params offset (2), lookup count (2) and the lookup
    /// indices (2 each). Scripts and languages are not evaluated: every feature with the tag counts.
    /// </remarks>
    public static SortedSet<int> ReadFeatureLookups(ReadOnlySpan<byte> table, string featureTag)
    {
        int featureList = BigEndian.U16(table, 6);
        var tag = System.Text.Encoding.ASCII.GetBytes(featureTag);
        var lookupIndices = new SortedSet<int>();
        int featureCount = BigEndian.U16(table, featureList);
        for (int i = 0; i < featureCount; i++)
        {
            int record = featureList + 2 + 6 * i;
            if (!table.Slice(record, 4).SequenceEqual(tag))
                continue;
            int feature = featureList + BigEndian.U16(table, record + 4);
            int count = BigEndian.U16(table, feature + 2);
            for (int j = 0; j < count; j++)
                lookupIndices.Add(BigEndian.U16(table, feature + 4 + 2 * j));
        }
        return lookupIndices;
    }

    /// <summary>
    /// Returns the subtables of a lookup as (lookup type, position in <paramref name="table"/>).
    /// Extension subtables are resolved to the subtable they point to.
    /// </summary>
    /// <remarks>
    /// Lookup list (at lookupListOffset, byte 8 of the header): count (2), offsets (2 each).
    /// A lookup: type (2), flag (2), subtable count (2), subtable offsets (2 each). An extension
    /// subtable: format (2), real lookup type (2), offset (4) from the extension subtable.
    /// </remarks>
    /// <param name="table">The GSUB or GPOS table.</param>
    /// <param name="lookupIndex">Index of the lookup.</param>
    /// <param name="extensionType"><see cref="GsubExtension"/> or <see cref="GposExtension"/>.</param>
    public static List<(int Type, int Offset)> ReadLookupSubtables(ReadOnlySpan<byte> table, int lookupIndex, int extensionType)
    {
        var result = new List<(int, int)>();
        int lookupList = BigEndian.U16(table, 8);
        if (lookupIndex >= BigEndian.U16(table, lookupList))
            return result;
        int lookup = lookupList + BigEndian.U16(table, lookupList + 2 + 2 * lookupIndex);
        int type = BigEndian.U16(table, lookup);
        int subtableCount = BigEndian.U16(table, lookup + 4);
        for (int s = 0; s < subtableCount; s++)
        {
            int subtable = lookup + BigEndian.U16(table, lookup + 6 + 2 * s);
            int subtableType = type;
            if (type == extensionType)
            {
                subtableType = BigEndian.U16(table, subtable + 2);
                subtable += checked((int)BigEndian.U32(table, subtable + 4));
            }
            result.Add((subtableType, subtable));
        }
        return result;
    }

    /// <summary>
    /// Reads a coverage table: the list of glyphs a subtable applies to, in coverage order.
    /// Format 1 lists the glyphs; format 2 lists ranges (start, end, index of the first glyph).
    /// </summary>
    public static List<int> ReadCoverage(ReadOnlySpan<byte> table, int offset)
    {
        var glyphs = new List<int>();
        int format = BigEndian.U16(table, offset);
        int count = BigEndian.U16(table, offset + 2);
        for (int i = 0; i < count; i++)
        {
            if (format == 1)
            {
                glyphs.Add(BigEndian.U16(table, offset + 4 + 2 * i));
            }
            else
            {
                int record = offset + 4 + 6 * i;
                int start = BigEndian.U16(table, record);
                int end = BigEndian.U16(table, record + 2);
                for (int g = start; g <= end; g++)
                    glyphs.Add(g);
            }
        }
        return glyphs;
    }

    /// <summary>
    /// Reads a class definition: glyph → class. Format 1 gives classes for a run of glyphs
    /// starting at startGlyphID; format 2 gives ranges with one class each. Unlisted glyphs are class 0.
    /// </summary>
    public static Dictionary<int, int> ReadClassDefinition(ReadOnlySpan<byte> table, int offset)
    {
        var classes = new Dictionary<int, int>();
        int format = BigEndian.U16(table, offset);
        if (format == 1)
        {
            int start = BigEndian.U16(table, offset + 2);
            int count = BigEndian.U16(table, offset + 4);
            for (int i = 0; i < count; i++)
                classes[start + i] = BigEndian.U16(table, offset + 6 + 2 * i);
        }
        else if (format == 2)
        {
            int count = BigEndian.U16(table, offset + 2);
            for (int i = 0; i < count; i++)
            {
                int record = offset + 4 + 6 * i;
                int start = BigEndian.U16(table, record);
                int end = BigEndian.U16(table, record + 2);
                int value = BigEndian.U16(table, record + 4);
                for (int g = start; g <= end; g++)
                    classes[g] = value;
            }
        }
        return classes;
    }

    /// <summary>
    /// Size in bytes of a value record: every set bit of the value format adds one 16-bit field
    /// (XPlacement, YPlacement, XAdvance, YAdvance and four device table offsets).
    /// </summary>
    public static int ValueRecordSize(int valueFormat) => 2 * System.Numerics.BitOperations.PopCount((uint)(valueFormat & 0xFF));

    /// <summary>
    /// Reads the XAdvance field of a value record, if present. Bit 0x0004 is XAdvance; it comes
    /// after XPlacement (0x0001) and YPlacement (0x0002) if those are present.
    /// </summary>
    public static int ReadXAdvance(ReadOnlySpan<byte> table, int offset, int valueFormat)
    {
        const int xPlacement = 0x0001, yPlacement = 0x0002, xAdvance = 0x0004;
        if ((valueFormat & xAdvance) == 0)
            return 0;
        int position = offset;
        if ((valueFormat & xPlacement) != 0)
            position += 2;
        if ((valueFormat & yPlacement) != 0)
            position += 2;
        return BigEndian.I16(table, position);
    }
}
