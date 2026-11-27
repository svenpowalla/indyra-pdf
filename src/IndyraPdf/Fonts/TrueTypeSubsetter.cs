// Creates subset fonts for embedding in PDF.
//
// A font file such as Liberation Sans contains more than 2,600 glyphs and is about 400 KB in
// size. A letter may need 80 of them. To keep the PDF small, a new, valid TrueType file is built
// that contains only these glyphs. The glyphs get new consecutive numbers (0, 1, 2, …). The PDF
// writer (PdfFont) writes exactly these new numbers into the page content.
//
// Composite glyphs: some glyphs have no outline of their own but are assembled from other glyphs,
// e.g. "Ä" from "A" and the two dots. These components must be included in the subset, and the
// references to them must carry the new numbers. Components may themselves be composite.
//
// The approach follows FPDF (makefont/ttfparser.php). Unlike PDFsharp 6.2.4 it renumbers the
// glyphs (smaller files) and resolves composite glyphs recursively (PDFsharp only one level deep).

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts;

/// <summary>
/// Creates a subset of a TrueType font that contains only the requested glyphs.
/// </summary>
/// <remarks>
/// The glyphs are renumbered consecutively, components of composite glyphs are added
/// recursively and their references are rewritten, "hmtx", "loca", "maxp" and "hhea" are rebuilt
/// and the font checksum is set. Unlike FPDF, "cmap" and "name" are left out because the PDF
/// addresses glyphs by number (CID font) and states the font name itself.
/// </remarks>
internal static class TrueTypeSubsetter
{
    /// <summary>
    /// Tables copied unchanged into the subset: "cvt ", "fpgm" and "prep" hold the hinting
    /// instructions that make small sizes render cleanly, "OS/2" holds metrics and the embedding
    /// permission. The other required tables are rebuilt.
    /// </summary>
    static readonly string[] CopiedTables = ["cvt ", "fpgm", "prep", "OS/2"];

    /// <summary>Builds the subset font.</summary>
    /// <param name="font">Source font.</param>
    /// <param name="glyphs">
    /// Glyph numbers of the source font in the order of their new numbers. The list must start
    /// with glyph 0 (.notdef). The list itself is not modified.
    /// </param>
    /// <returns>
    /// The font program and the final glyph list, which additionally contains the components of
    /// composite glyphs at the end.
    /// </returns>
    public static TrueTypeSubset Build(TrueTypeFont font, IReadOnlyList<int> glyphs)
    {
        if (glyphs.Count == 0 || glyphs[0] != 0)
            throw new ArgumentException("The glyph list must start with glyph 0.", nameof(glyphs));

        var allGlyphs = new List<int>(glyphs);

        // Old glyph number → new glyph number (= position in the list).
        var newIds = new Dictionary<int, int>();
        for (int i = 0; i < allGlyphs.Count; i++)
            newIds.TryAdd(allGlyphs[i], i);

        // Add the components of composite glyphs. The list grows during the loop, so components
        // that are composite themselves are processed as well.
        for (int i = 0; i < allGlyphs.Count; i++)
        {
            foreach (var component in CompositeGlyph.GetComponents(font.GetGlyphData(allGlyphs[i])))
            {
                if (newIds.TryAdd(component.GlyphId, allGlyphs.Count))
                    allGlyphs.Add(component.GlyphId);
            }
        }

        // Tables of the new file, sorted by name. The specification requires this order in the
        // table directory (uppercase before lowercase, hence ordinal comparison).
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var tag in CopiedTables)
        {
            if (font.TryTable(tag, out var data))
                tables[tag] = data.ToArray();
        }

        var (glyf, loca) = BuildGlyfAndLoca(font, allGlyphs, newIds);
        tables["glyf"] = glyf;
        tables["loca"] = loca;
        tables["hmtx"] = BuildHmtx(font, allGlyphs);

        // "hhea" byte 34 = numberOfHMetrics: in the new "hmtx" every glyph has a complete pair.
        var hhea = font.Table("hhea").ToArray();
        BigEndian.WriteU16(hhea, 34, allGlyphs.Count);
        tables["hhea"] = hhea;

        // "maxp" byte 4 = numGlyphs.
        var maxp = font.Table("maxp").ToArray();
        BigEndian.WriteU16(maxp, 4, allGlyphs.Count);
        tables["maxp"] = maxp;

        // "head" byte 8 = checkSumAdjustment. It must be 0 while the checksum is computed; the
        // correct value is written in Assemble.
        var head = font.Table("head").ToArray();
        BigEndian.WriteU32(head, 8, 0);
        tables["head"] = head;

        if (font.TryTable("post", out var post) && post.Length >= 32)
        {
            // Version 3.0: no glyph names, which PDF does not need anyway.
            var newPost = post[..32].ToArray();
            BigEndian.WriteU32(newPost, 0, 0x00030000);
            tables["post"] = newPost;
        }

        return new TrueTypeSubset(Assemble(tables), allGlyphs);
    }

    /// <summary>
    /// Builds the tables "glyf" (outlines in the new order) and "loca" (position of every glyph).
    /// References inside composite glyphs are rewritten to the new numbers.
    /// </summary>
    static (byte[] Glyf, byte[] Loca) BuildGlyfAndLoca(TrueTypeFont font, List<int> glyphs, Dictionary<int, int> newIds)
    {
        bool shortFormat = font.IndexToLocFormat == 0;
        var glyf = new MemoryStream();
        var loca = new byte[(glyphs.Count + 1) * (shortFormat ? 2 : 4)];

        // One more loca entry than glyphs: the last one marks the end of the last glyph.
        for (int i = 0; i <= glyphs.Count; i++)
        {
            int offset = (int)glyf.Length;
            if (shortFormat)
                BigEndian.WriteU16(loca, 2 * i, offset / 2);
            else
                BigEndian.WriteU32(loca, 4 * i, (uint)offset);

            if (i == glyphs.Count)
                break;

            var data = font.GetGlyphData(glyphs[i]).ToArray();
            foreach (var component in CompositeGlyph.GetComponents(data))
                BigEndian.WriteU16(data, component.Offset, newIds[component.GlyphId]);
            glyf.Write(data);

            // In the short loca format all offsets must be even.
            if (shortFormat && glyf.Length % 2 != 0)
                glyf.WriteByte(0);
        }
        return (glyf.ToArray(), loca);
    }

    /// <summary>Builds the "hmtx" table: advance width (u16) and left side bearing (i16) per glyph.</summary>
    static byte[] BuildHmtx(TrueTypeFont font, List<int> glyphs)
    {
        var hmtx = new byte[glyphs.Count * 4];
        for (int i = 0; i < glyphs.Count; i++)
        {
            BigEndian.WriteU16(hmtx, 4 * i, font.GetAdvanceWidth(glyphs[i]));
            BigEndian.WriteU16(hmtx, 4 * i + 2, font.GetLeftSideBearing(glyphs[i]));
        }
        return hmtx;
    }

    /// <summary>
    /// Assembles the tables into a font file: header (12 bytes), table directory (16 bytes per
    /// table), then the tables, each padded to a length divisible by 4.
    /// </summary>
    static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        // The header contains helpers for a binary search in the table directory:
        // entrySelector = largest e with 2^e ≤ count, searchRange = 2^e · 16,
        // rangeShift = count · 16 − searchRange.
        int count = tables.Count;
        int entrySelector = 0;
        while ((1 << (entrySelector + 1)) <= count)
            entrySelector++;
        int searchRange = 16 << entrySelector;

        int headerLength = 12 + 16 * count;
        int total = headerLength + tables.Values.Sum(t => FontChecksum.Pad4(t.Length));
        var font = new byte[total];

        BigEndian.WriteU32(font, 0, 0x00010000); // signature: TrueType outlines, version 1.0
        BigEndian.WriteU16(font, 4, count);
        BigEndian.WriteU16(font, 6, searchRange);
        BigEndian.WriteU16(font, 8, entrySelector);
        BigEndian.WriteU16(font, 10, count * 16 - searchRange);

        // One directory entry per table: name, checksum, offset, length.
        // The length is the real length without padding; the next offset includes the padding.
        int record = 12;
        int position = headerLength;
        int headPosition = -1;
        foreach (var (tag, data) in tables)
        {
            for (int i = 0; i < 4; i++)
                font[record + i] = (byte)tag[i];
            BigEndian.WriteU32(font, record + 4, FontChecksum.Compute(data));
            BigEndian.WriteU32(font, record + 8, (uint)position);
            BigEndian.WriteU32(font, record + 12, (uint)data.Length);
            data.CopyTo(font, position);
            if (tag == "head")
                headPosition = position;
            record += 16;
            position += FontChecksum.Pad4(data.Length);
        }

        // checkSumAdjustment = 0xB1B0AFBA − checksum of the whole file (OpenType, table "head").
        uint adjustment = unchecked(0xB1B0AFBA - FontChecksum.Compute(font));
        BigEndian.WriteU32(font, headPosition + 8, adjustment);
        return font;
    }
}
