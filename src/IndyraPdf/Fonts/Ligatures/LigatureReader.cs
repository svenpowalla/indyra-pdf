// Reads the ligature rules of a font from its GSUB table ("glyph substitution").
//
// Only what emoji sequences need is read: the lookups of the feature "ccmp" ("glyph composition
// and decomposition") of type 4 ("ligature substitution"), also when wrapped in extension lookups
// (type 7). Noto Color Emoji builds all valid emoji sequences this way (over 4000 rules).
//
// Neither FPDF nor PDFsharp 6.2.4 read GSUB; this implementation follows the OpenType
// specification (chapter "GSUB – The Glyph Substitution Table", lookup type 4) directly.
//
// Simplifications: other lookup types are skipped. In Noto Color Emoji they only handle invalid
// flag sequences (e.g. a single regional indicator letter). Lookup flags are not evaluated.

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts.Ligatures;

/// <summary>Builds the <see cref="LigatureTable"/> of a font.</summary>
internal static class LigatureReader
{
    /// <summary>GSUB lookup type "ligature substitution".</summary>
    const int LigatureSubstitution = 4;

    /// <summary>Feature whose ligatures are read.</summary>
    const string CompositionFeature = "ccmp";

    /// <summary>Reads the ligatures; fonts without them get an empty table.</summary>
    public static LigatureTable Read(TrueTypeFont font)
    {
        if (!font.TryTable("GSUB", out var gsub))
            return LigatureTable.Empty;
        try
        {
            var lookups = new List<IReadOnlyDictionary<int, IReadOnlyList<Ligature>>>();
            foreach (int index in OpenTypeCommon.ReadFeatureLookups(gsub, CompositionFeature))
            {
                var rules = new Dictionary<int, IReadOnlyList<Ligature>>();
                foreach (var (type, offset) in OpenTypeCommon.ReadLookupSubtables(gsub, index, OpenTypeCommon.GsubExtension))
                {
                    if (type == LigatureSubstitution)
                        ReadLigatureSubtable(gsub, offset, rules);
                }
                if (rules.Count > 0)
                    lookups.Add(rules);
            }
            return lookups.Count > 0 ? new LigatureTable(lookups) : LigatureTable.Empty;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Damaged substitution data must not prevent using the font; glyphs stay single.
            return LigatureTable.Empty;
        }
    }

    /// <summary>
    /// Reads a ligature substitution subtable (format 1, the only format):
    /// substFormat (2), coverageOffset (2), ligatureSetCount (2), ligatureSetOffsets (2 each).
    /// Ligature set i belongs to the i-th glyph of the coverage table: ligatureCount (2),
    /// ligatureOffsets (2 each, from the set). A ligature: ligatureGlyph (2), componentCount (2),
    /// then componentCount − 1 glyphs (the first component is the covered glyph itself).
    /// </summary>
    static void ReadLigatureSubtable(ReadOnlySpan<byte> gsub, int subtable, Dictionary<int, IReadOnlyList<Ligature>> rules)
    {
        if (BigEndian.U16(gsub, subtable) != 1)
            return;
        var coverage = OpenTypeCommon.ReadCoverage(gsub, subtable + BigEndian.U16(gsub, subtable + 2));
        int setCount = BigEndian.U16(gsub, subtable + 4);
        for (int i = 0; i < setCount && i < coverage.Count; i++)
        {
            int set = subtable + BigEndian.U16(gsub, subtable + 6 + 2 * i);
            int ligatureCount = BigEndian.U16(gsub, set);
            var ligatures = new List<Ligature>(ligatureCount);
            for (int j = 0; j < ligatureCount; j++)
            {
                int ligature = set + BigEndian.U16(gsub, set + 2 + 2 * j);
                int glyph = BigEndian.U16(gsub, ligature);
                int componentCount = BigEndian.U16(gsub, ligature + 2);
                var components = new int[Math.Max(0, componentCount - 1)];
                for (int k = 0; k < components.Length; k++)
                    components[k] = BigEndian.U16(gsub, ligature + 4 + 2 * k);
                ligatures.Add(new Ligature(glyph, components));
            }
            // A glyph covered by several subtables keeps the rules of the first one first.
            rules[coverage[i]] = rules.TryGetValue(coverage[i], out var existing) ? [.. existing, .. ligatures] : ligatures;
        }
    }
}
