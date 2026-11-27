// Reads the kerning of a font.
//
// Modern fonts store kerning in the GPOS table ("glyph positioning"), as lookups of type 2
// ("pair adjustment") referenced by the feature "kern". Older fonts only have the "kern" table.
// Liberation has both; GPOS contains all pairs of "kern" plus more (Greek, Cyrillic), so GPOS
// is preferred and "kern" is the fallback.
//
// Neither FPDF nor PDFsharp 6.2.4 support kerning; this implementation follows the OpenType
// specification directly.
//
// Simplifications: lookup flags (e.g. "ignore marks") are not evaluated, only directly
// neighbouring glyphs are kerned, and only the horizontal advance of the first glyph is used.
// That covers kerning in Latin, Greek and Cyrillic text.

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts.Kerning;

/// <summary>Builds the <see cref="KerningTable"/> of a font.</summary>
internal static class KerningReader
{
    /// <summary>GPOS lookup type "pair adjustment".</summary>
    const int PairAdjustment = 2;

    /// <summary>Reads the kerning; fonts without kerning get an empty table.</summary>
    public static KerningTable Read(TrueTypeFont font)
    {
        try
        {
            if (font.TryTable("GPOS", out var gpos))
            {
                var lookups = ReadGpos(gpos);
                if (lookups.Count > 0)
                    return new KerningTable(lookups);
            }
            if (font.TryTable("kern", out var kern))
            {
                var pairs = ReadKernTable(kern);
                if (pairs.Count > 0)
                    return new KerningTable([[pairs]]);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // Damaged kerning data must not prevent using the font; it is simply not kerned.
        }
        return KerningTable.Empty;
    }

    /// <summary>Reads the pair adjustment lookups of the "kern" feature from GPOS.</summary>
    static List<IReadOnlyList<IPairAdjustment>> ReadGpos(ReadOnlySpan<byte> gpos)
    {
        var lookups = new List<IReadOnlyList<IPairAdjustment>>();
        foreach (int index in OpenTypeCommon.ReadFeatureLookups(gpos, "kern"))
        {
            var subtables = new List<IPairAdjustment>();
            foreach (var (type, offset) in OpenTypeCommon.ReadLookupSubtables(gpos, index, OpenTypeCommon.GposExtension))
            {
                if (type == PairAdjustment && ReadPairAdjustment(gpos, offset) is { } table)
                    subtables.Add(table);
            }
            if (subtables.Count > 0)
                lookups.Add(subtables);
        }
        return lookups;
    }

    /// <summary>Reads a pair adjustment subtable in format 1 (glyph pairs) or 2 (class pairs).</summary>
    static IPairAdjustment? ReadPairAdjustment(ReadOnlySpan<byte> gpos, int subtable)
    {
        int format = BigEndian.U16(gpos, subtable);
        var coverage = OpenTypeCommon.ReadCoverage(gpos, subtable + BigEndian.U16(gpos, subtable + 2));
        int valueFormat1 = BigEndian.U16(gpos, subtable + 4);
        int valueFormat2 = BigEndian.U16(gpos, subtable + 6);
        int size1 = OpenTypeCommon.ValueRecordSize(valueFormat1);
        int size2 = OpenTypeCommon.ValueRecordSize(valueFormat2);

        if (format == 1)
        {
            // pairSetCount (2) and one pair set per covered glyph: count (2), then records of
            // secondGlyph (2), value record 1, value record 2.
            var pairs = new GlyphPairTable();
            int pairSetCount = BigEndian.U16(gpos, subtable + 8);
            for (int i = 0; i < pairSetCount && i < coverage.Count; i++)
            {
                int pairSet = subtable + BigEndian.U16(gpos, subtable + 10 + 2 * i);
                int count = BigEndian.U16(gpos, pairSet);
                int recordSize = 2 + size1 + size2;
                for (int j = 0; j < count; j++)
                {
                    int record = pairSet + 2 + recordSize * j;
                    int second = BigEndian.U16(gpos, record);
                    pairs.Add(coverage[i], second, OpenTypeCommon.ReadXAdvance(gpos, record + 2, valueFormat1));
                }
            }
            return pairs;
        }

        if (format == 2)
        {
            // classDef1 (2), classDef2 (2), class1Count (2), class2Count (2), then a matrix of
            // class1Count × class2Count records of value record 1 and value record 2.
            var leftClasses = OpenTypeCommon.ReadClassDefinition(gpos, subtable + BigEndian.U16(gpos, subtable + 8));
            var rightClasses = OpenTypeCommon.ReadClassDefinition(gpos, subtable + BigEndian.U16(gpos, subtable + 10));
            int class1Count = BigEndian.U16(gpos, subtable + 12);
            int class2Count = BigEndian.U16(gpos, subtable + 14);
            var values = new int[class1Count, class2Count];
            int recordSize = size1 + size2;
            for (int c1 = 0; c1 < class1Count; c1++)
            {
                for (int c2 = 0; c2 < class2Count; c2++)
                {
                    int record = subtable + 16 + recordSize * (c1 * class2Count + c2);
                    values[c1, c2] = OpenTypeCommon.ReadXAdvance(gpos, record, valueFormat1);
                }
            }
            return new ClassPairTable([.. coverage], leftClasses, rightClasses, values);
        }
        return null;
    }

    /// <summary>
    /// Reads the legacy "kern" table (Microsoft version 0, subtable format 0).
    /// Header: version (2), number of subtables (2). Subtable: version (2), length (2),
    /// coverage (2; bit 0 = horizontal, high byte = format), nPairs (2), 6 bytes search helpers,
    /// then pairs of left (2), right (2), value (2).
    /// </summary>
    static GlyphPairTable ReadKernTable(ReadOnlySpan<byte> kern)
    {
        var pairs = new GlyphPairTable();
        if (BigEndian.U16(kern, 0) != 0)
            return pairs; // Apple's version 1 format is not supported
        int tableCount = BigEndian.U16(kern, 2);
        int offset = 4;
        for (int t = 0; t < tableCount; t++)
        {
            int length = BigEndian.U16(kern, offset + 2);
            int coverage = BigEndian.U16(kern, offset + 4);
            bool horizontal = (coverage & 1) != 0;
            int format = coverage >> 8;
            if (horizontal && format == 0)
            {
                int count = BigEndian.U16(kern, offset + 6);
                for (int i = 0; i < count; i++)
                {
                    int record = offset + 14 + 6 * i;
                    pairs.Add(BigEndian.U16(kern, record), BigEndian.U16(kern, record + 2), BigEndian.I16(kern, record + 4));
                }
            }
            offset += length;
        }
        return pairs;
    }
}
