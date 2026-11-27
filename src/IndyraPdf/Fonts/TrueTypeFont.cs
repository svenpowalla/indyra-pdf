// Reads TrueType font files (.ttf).
//
// This file belongs to the lowest layer of IndyraPdf and knows nothing about PDF. It provides
// font metrics (glyph widths, ascender, descender) and the mapping Unicode character → glyph.
// Three consumers use these values:
//   - the PDF writer (PdfFont), which embeds the font,
//   - the layout engine (stage 2), which breaks and aligns lines,
//   - the SkiaSharp preview (stage 4).
// Because all of them use the same metrics, PDF and preview look the same.
//
// Structure of a TrueType file (OpenType specification,
// https://learn.microsoft.com/typography/opentype/spec/):
// The file starts with a directory of "tables". Each table has a four-letter name (e.g. "head",
// "cmap") and holds one part of the font. All numbers are stored big-endian.
//
// Tables read here:
//   head  header: units per em, bounding box of all glyphs, style (bold/italic)
//   maxp  number of glyphs
//   hhea  horizontal header: ascender, descender, line gap
//   hmtx  advance width and left side bearing of every glyph
//   loca  position of every glyph inside the "glyf" table
//   glyf  glyph outlines
//   cmap  mapping Unicode character → glyph number
//   OS/2  further metrics (cap height), weight, embedding permission
//   post  italic angle, fixed pitch
//   name  font names (family, PostScript name)
//   CBLC, CBDT  color bitmaps (only in color emoji fonts such as Noto Color Emoji, which have
//               no "glyf" outlines; see ColorBitmapTable)
//   GSUB  ligatures, read on first use (see Ligatures/LigatureReader)

using System.Text;
using IndyraPdf.Fonts.Ligatures;
using IndyraPdf.Internal;

namespace IndyraPdf.Fonts;

/// <summary>
/// A TrueType font (glyphs with "glyf" outlines) that has been read into memory.
/// Provides metrics and the character mapping independent of any output format, so that the
/// layout engine, the PDF writer and the preview all compute with the same values.
/// </summary>
/// <remarks>
/// All metrics are given in font units (see <see cref="UnitsPerEm"/>). To convert them to points,
/// divide by <see cref="UnitsPerEm"/> and multiply by the font size. Example: a width of 1366
/// units at 2048 units per em and a 10 pt font is 6.67 pt.
/// <para>
/// Color bitmap fonts (CBDT/CBLC, e.g. Noto Color Emoji) are read as well. They have no outlines
/// (<see cref="HasOutlines"/> is false) and can only serve as fallback fonts for emoji
/// (<c>FontCollection.AddFallback</c>).
/// </para>
/// <para>
/// OpenType fonts with CFF outlines (.otf) and font collections (.ttc) are not supported yet.
/// </para>
/// </remarks>
public sealed class TrueTypeFont
{
    /// <summary>The complete bytes of the font file.</summary>
    readonly byte[] _data;

    /// <summary>Table directory: table name → position and length inside <see cref="_data"/>.</summary>
    readonly Dictionary<string, (int Offset, int Length)> _tables;

    /// <summary>Mapping Unicode code point → glyph number, read from the "cmap" table.</summary>
    readonly Dictionary<int, int> _characterMap;

    /// <summary>Advance width of every glyph in font units; index = glyph number.</summary>
    readonly ushort[] _advanceWidths;

    /// <summary>Distance from the glyph origin to the left edge of its outline, per glyph.</summary>
    readonly short[] _leftSideBearings;

    /// <summary>
    /// Start of every glyph inside the "glyf" table, measured from the table start. The array has
    /// one entry more than there are glyphs: glyph n spans from entry n to entry n + 1.
    /// Empty for fonts without outlines.
    /// </summary>
    readonly int[] _glyphOffsets;

    /// <summary>Color bitmaps of the glyphs, or null if the font has none.</summary>
    readonly ColorBitmapTable? _colorBitmaps;

    /// <summary>Ligature rules, read on first use (only fallback fonts for emoji need them).</summary>
    LigatureTable? _ligatures;

    /// <summary>Reads all required tables. Format errors raise exceptions.</summary>
    /// <param name="data">Bytes of the font file.</param>
    /// <param name="requireCharacterMap">
    /// Whether the "cmap" table must be present. Subset fonts embedded in PDF files have none.
    /// </param>
    TrueTypeFont(byte[] data, bool requireCharacterMap)
    {
        _data = data;
        _tables = ReadTableDirectory(data);

        // Table "head". Byte positions according to the OpenType specification:
        //   18 unitsPerEm (u16), 36–42 xMin/yMin/xMax/yMax (i16 each),
        //   44 macStyle (u16, bit 0 = bold, bit 1 = italic),
        //   50 indexToLocFormat (i16, 0 = short, 1 = long entries in "loca")
        var head = Table("head");
        UnitsPerEm = BigEndian.U16(head, 18);
        BoundingBox = new FontBoundingBox(BigEndian.I16(head, 36), BigEndian.I16(head, 38),
                                          BigEndian.I16(head, 40), BigEndian.I16(head, 42));
        var macStyle = BigEndian.U16(head, 44);
        IndexToLocFormat = BigEndian.I16(head, 50);

        // Table "maxp": byte 4 = numGlyphs (u16).
        GlyphCount = BigEndian.U16(Table("maxp"), 4);

        // Table "hhea": 4 ascender, 6 descender, 8 lineGap (i16 each),
        // 34 numberOfHMetrics (u16, number of complete entries in "hmtx").
        var hhea = Table("hhea");
        Ascender = BigEndian.I16(hhea, 4);
        Descender = BigEndian.I16(hhea, 6);
        LineGap = BigEndian.I16(hhea, 8);
        int numberOfHMetrics = BigEndian.U16(hhea, 34);

        (_advanceWidths, _leftSideBearings) = ReadHorizontalMetrics(Table("hmtx"), numberOfHMetrics, GlyphCount);
        HasOutlines = _tables.ContainsKey("glyf") && _tables.ContainsKey("loca");
        _glyphOffsets = HasOutlines ? ReadGlyphOffsets(Table("loca"), IndexToLocFormat, GlyphCount) : [];
        if (TryTable("CBLC", out var cblc) && _tables.TryGetValue("CBDT", out var cbdt))
            _colorBitmaps = new ColorBitmapTable(cblc, data, cbdt.Offset);
        _characterMap = requireCharacterMap || TryTable("cmap", out _) ? ReadCharacterMap(Table("cmap")) : [];

        // Table "OS/2" (optional). Byte positions:
        //   0 version, 4 usWeightClass, 8 fsType (embedding permission),
        //   26 yStrikeoutSize, 28 yStrikeoutPosition,
        //   from version 2 on: 86 sxHeight, 88 sCapHeight.
        // 78 bytes is the minimum length of the oldest version (version 0).
        WeightClass = 400;
        if (TryTable("OS/2", out var os2) && os2.Length >= 78)
        {
            WeightClass = BigEndian.U16(os2, 4);
            EmbeddingFlags = BigEndian.U16(os2, 8);
            StrikeoutThickness = BigEndian.I16(os2, 26);
            StrikeoutPosition = BigEndian.I16(os2, 28);
            var version = BigEndian.U16(os2, 0);
            if (version >= 2 && os2.Length >= 90)
            {
                XHeight = BigEndian.I16(os2, 86);
                CapHeight = BigEndian.I16(os2, 88);
            }
        }

        // Table "post" (optional): 4 italicAngle (fixed-point 16.16), 8 underlinePosition (i16),
        // 10 underlineThickness (i16), 12 isFixedPitch (u32).
        if (TryTable("post", out var post) && post.Length >= 16)
        {
            ItalicAngle = BigEndian.I32(post, 4) / 65536.0;
            UnderlinePosition = BigEndian.I16(post, 8);
            UnderlineThickness = BigEndian.I16(post, 10);
            IsFixedPitch = BigEndian.U32(post, 12) != 0;
        }

        // Fallbacks for fonts without these values: common proportions of the em.
        if (UnderlineThickness <= 0)
            UnderlineThickness = UnitsPerEm / 20;
        if (UnderlinePosition == 0)
            UnderlinePosition = -UnitsPerEm / 10;
        if (StrikeoutThickness <= 0)
            StrikeoutThickness = UnderlineThickness;

        IsBold = (macStyle & 1) != 0 || WeightClass >= 600;
        IsItalic = (macStyle & 2) != 0 || ItalicAngle != 0;

        // Older fonts lack these values: take them from the outlines of "H" and "x".
        if (CapHeight == 0)
            CapHeight = EstimateHeight('H', Ascender);
        if (XHeight == 0)
            XHeight = EstimateHeight('x', Ascender / 2);
        if (StrikeoutPosition == 0)
            StrikeoutPosition = XHeight / 2; // middle of the lowercase letters

        // Name IDs: 1 = family name, 6 = PostScript name.
        var names = ReadNames(TryTable("name", out var name) ? name : default);
        FamilyName = names.GetValueOrDefault(1) ?? "Unknown";
        PostScriptName = SanitizePostScriptName(names.GetValueOrDefault(6) ?? FamilyName);
    }

    /// <summary>Reads a font from a file.</summary>
    public static TrueTypeFont Load(string path) => Load(File.ReadAllBytes(path));

    /// <summary>Reads a font from the bytes of a .ttf file.</summary>
    /// <exception cref="FontFormatException">The data is not a supported TrueType font.</exception>
    public static TrueTypeFont Load(byte[] data) => Load(data, requireCharacterMap: true);

    /// <summary>
    /// Reads a font. Without <paramref name="requireCharacterMap"/> the character map may be
    /// missing, as in the subset fonts embedded in PDF files.
    /// </summary>
    internal static TrueTypeFont Load(byte[] data, bool requireCharacterMap)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return new TrueTypeFont(data, requireCharacterMap);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or KeyNotFoundException)
        {
            // Reading past the end of the data means the file is damaged. Instead of a technical
            // error, the caller gets an understandable message.
            throw new FontFormatException("The font file is damaged or incomplete.", ex);
        }
    }

    /// <summary>Family name, e.g. "Liberation Sans".</summary>
    public string FamilyName { get; }

    /// <summary>PostScript name, e.g. "LiberationSans-Bold". Used as the font name in PDF.</summary>
    public string PostScriptName { get; }

    /// <summary>
    /// Font units per em (usually 1000 or 2048). The em is the font size: with a 12 pt font,
    /// <see cref="UnitsPerEm"/> units equal 12 pt.
    /// </summary>
    public int UnitsPerEm { get; }

    /// <summary>Ascender from the "hhea" table: distance from the baseline upwards (positive).</summary>
    public int Ascender { get; }

    /// <summary>Descender from the "hhea" table: distance from the baseline downwards (negative).</summary>
    public int Descender { get; }

    /// <summary>Additional space between two lines from the "hhea" table.</summary>
    public int LineGap { get; }

    /// <summary>Height of capital letters above the baseline.</summary>
    public int CapHeight { get; }

    /// <summary>Height of lowercase letters without ascender (height of "x").</summary>
    public int XHeight { get; }

    /// <summary>Position of the underline relative to the baseline (negative = below).</summary>
    public int UnderlinePosition { get; }

    /// <summary>Thickness of the underline.</summary>
    public int UnderlineThickness { get; }

    /// <summary>Position of the strikeout line above the baseline.</summary>
    public int StrikeoutPosition { get; }

    /// <summary>Thickness of the strikeout line.</summary>
    public int StrikeoutThickness { get; }

    /// <summary>Italic angle in degrees (negative for fonts slanted to the right).</summary>
    public double ItalicAngle { get; }

    /// <summary>Whether all glyphs have the same width (monospaced font).</summary>
    public bool IsFixedPitch { get; }

    /// <summary>Weight according to OS/2 (400 = regular, 700 = bold).</summary>
    public int WeightClass { get; }

    /// <summary>Whether the font is bold.</summary>
    public bool IsBold { get; }

    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic { get; }

    /// <summary>Smallest rectangle enclosing the outlines of all glyphs.</summary>
    public FontBoundingBox BoundingBox { get; }

    /// <summary>Number of glyphs in the font.</summary>
    public int GlyphCount { get; }

    /// <summary>
    /// Whether the font license allows embedding. In the OS/2 field fsType, the value 2 in the
    /// lower four bits means "Restricted License": embedding is not allowed.
    /// </summary>
    public bool EmbeddingAllowed => (EmbeddingFlags & 0x000F) != 0x0002;

    /// <summary>
    /// Whether the glyphs have outlines ("glyf" table). Only such fonts can be embedded as a
    /// font in PDF; color bitmap fonts are drawn as pictures instead.
    /// </summary>
    public bool HasOutlines { get; }

    /// <summary>Whether the glyphs are color pictures (CBDT/CBLC), as in color emoji fonts.</summary>
    public bool HasColorBitmaps => _colorBitmaps is not null;

    /// <summary>Ligature rules of the "ccmp" feature, read on first use.</summary>
    internal LigatureTable Ligatures => _ligatures ??= LigatureReader.Read(this);

    /// <summary>The color picture of a glyph, or null if the font or the glyph has none.</summary>
    internal ColorBitmap? GetColorBitmap(int glyphId) => _colorBitmaps?.Get(glyphId);

    /// <summary>The complete font file, e.g. for other renderers such as the SkiaSharp preview.</summary>
    internal ReadOnlyMemory<byte> FileData => _data;

    /// <summary>Raw value of the OS/2 field fsType (0 if the table is missing).</summary>
    internal int EmbeddingFlags { get; }

    /// <summary>Format of the "loca" table: 0 = 16-bit entries (half offsets), 1 = 32-bit entries.</summary>
    internal int IndexToLocFormat { get; }

    /// <summary>Line height according to "hhea": ascender − descender + line gap.</summary>
    public int LineHeight => Ascender - Descender + LineGap;

    /// <summary>
    /// Returns the glyph number for a Unicode character, or 0 if the font does not contain it.
    /// Glyph 0 is called ".notdef" and is usually drawn as an empty box.
    /// </summary>
    public int GetGlyphId(int codePoint) => _characterMap.GetValueOrDefault(codePoint);

    /// <summary>Whether the font contains a Unicode character.</summary>
    public bool HasCharacter(int codePoint) => _characterMap.ContainsKey(codePoint);

    /// <summary>
    /// Advance width of a glyph in font units: the amount by which the writing position moves on
    /// after this glyph.
    /// </summary>
    public int GetAdvanceWidth(int glyphId) => _advanceWidths[glyphId];

    /// <summary>Distance from the glyph origin to the left edge of its outline.</summary>
    internal int GetLeftSideBearing(int glyphId) => _leftSideBearings[glyphId];

    /// <summary>Kerning data, read on first use (most documents use only a few fonts).</summary>
    Kerning.KerningTable? _kerning;

    /// <summary>
    /// Kerning between two neighbouring glyphs in font units: added to the advance of the left
    /// glyph (negative = closer together, e.g. "AV"). 0 if the font defines no adjustment.
    /// </summary>
    public int GetKerning(int leftGlyphId, int rightGlyphId)
    {
        _kerning ??= Kerning.KerningReader.Read(this);
        return _kerning.GetAdjustment(leftGlyphId, rightGlyphId);
    }

    /// <summary>
    /// Returns the raw data of a glyph from the "glyf" table. Empty glyphs (such as the space)
    /// have no data.
    /// </summary>
    internal ReadOnlySpan<byte> GetGlyphData(int glyphId)
    {
        if (!HasOutlines)
            return ReadOnlySpan<byte>.Empty;
        var (offset, _) = _tables["glyf"];
        int start = _glyphOffsets[glyphId];
        int end = _glyphOffsets[glyphId + 1];
        return end > start ? _data.AsSpan(offset + start, end - start) : ReadOnlySpan<byte>.Empty;
    }

    /// <summary>Returns the bytes of a table if it exists.</summary>
    internal bool TryTable(string tag, out ReadOnlySpan<byte> table)
    {
        if (_tables.TryGetValue(tag, out var entry))
        {
            table = _data.AsSpan(entry.Offset, entry.Length);
            return true;
        }
        table = default;
        return false;
    }

    /// <summary>Returns the bytes of a table that must exist.</summary>
    /// <exception cref="FontFormatException">The table is missing.</exception>
    internal ReadOnlySpan<byte> Table(string tag)
    {
        if (!TryTable(tag, out var table))
            throw new FontFormatException($"The font has no \"{tag}\" table.");
        return table;
    }

    /// <summary>
    /// Determines a height from the outline of a glyph. Every glyph in "glyf" starts with
    /// numberOfContours, xMin, yMin, xMax, yMax (i16 each), so yMax is at byte 8.
    /// </summary>
    int EstimateHeight(char c, int fallback)
    {
        var glyph = GetGlyphData(GetGlyphId(c));
        return glyph.Length >= 10 ? BigEndian.I16(glyph, 8) : fallback;
    }

    /// <summary>
    /// Reads the table directory at the start of the file.
    /// Layout: 4 bytes signature, 2 bytes number of tables, 6 bytes search helpers, then
    /// 16 bytes per table: name (4), checksum (4), offset (4), length (4).
    /// </summary>
    static Dictionary<string, (int, int)> ReadTableDirectory(byte[] data)
    {
        var span = data.AsSpan();
        if (span.Length < 12)
            throw new FontFormatException("The file is too short for a TrueType font.");

        // The signature at the start of the file tells the kind of font.
        uint version = BigEndian.U32(span, 0);
        if (version == 0x74746366) // "ttcf": collection of several fonts
            throw new FontFormatException("Font collections (.ttc) are not supported yet.");
        if (version == 0x4F54544F) // "OTTO": OpenType with PostScript outlines (CFF)
            throw new FontFormatException("OpenType fonts with CFF outlines are not supported yet.");
        if (version != 0x00010000 && version != 0x74727565) // 1.0 or "true" (older Apple fonts)
            throw new FontFormatException("The file is not a TrueType font.");

        int count = BigEndian.U16(span, 4);
        var tables = new Dictionary<string, (int, int)>(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            int record = 12 + 16 * i;
            string tag = Encoding.ASCII.GetString(span.Slice(record, 4));
            int offset = checked((int)BigEndian.U32(span, record + 8));
            int length = checked((int)BigEndian.U32(span, record + 12));
            if (offset < 0 || length < 0 || offset + length > span.Length)
                throw new FontFormatException($"The \"{tag}\" table lies outside the file.");
            tables[tag] = (offset, length);
        }

        // Without these tables no metrics can be determined.
        foreach (var required in new[] { "head", "hhea", "maxp", "hmtx" })
        {
            if (!tables.ContainsKey(required))
                throw new FontFormatException($"The font has no \"{required}\" table.");
        }
        // Glyphs are either outlines (loca + glyf) or color pictures (CBLC + CBDT).
        bool outlines = tables.ContainsKey("loca") && tables.ContainsKey("glyf");
        bool bitmaps = tables.ContainsKey("CBLC") && tables.ContainsKey("CBDT");
        if (!outlines && !bitmaps)
            throw new FontFormatException(tables.ContainsKey("glyf")
                ? "The font has no \"loca\" table."
                : "The font has no \"glyf\" table (and no color bitmaps).");
        return tables;
    }

    /// <summary>
    /// Reads the "hmtx" table. It starts with <paramref name="numberOfHMetrics"/> pairs of advance
    /// width (u16) and left side bearing (i16). After that only left side bearings follow; these
    /// glyphs all share the width of the last pair. This saves space in monospaced fonts.
    /// </summary>
    static (ushort[], short[]) ReadHorizontalMetrics(ReadOnlySpan<byte> hmtx, int numberOfHMetrics, int glyphCount)
    {
        var advances = new ushort[glyphCount];
        var bearings = new short[glyphCount];
        ushort lastAdvance = 0;
        for (int i = 0; i < glyphCount; i++)
        {
            if (i < numberOfHMetrics)
            {
                lastAdvance = BigEndian.U16(hmtx, 4 * i);
                bearings[i] = BigEndian.I16(hmtx, 4 * i + 2);
            }
            else
            {
                int offset = 4 * numberOfHMetrics + 2 * (i - numberOfHMetrics);
                bearings[i] = offset + 2 <= hmtx.Length ? BigEndian.I16(hmtx, offset) : (short)0;
            }
            advances[i] = lastAdvance;
        }
        return (advances, bearings);
    }

    /// <summary>
    /// Reads the "loca" table with the position of every glyph inside "glyf". The short format
    /// (format 0) stores half the offset as u16, the long format the offset as u32.
    /// </summary>
    static int[] ReadGlyphOffsets(ReadOnlySpan<byte> loca, int format, int glyphCount)
    {
        var offsets = new int[glyphCount + 1];
        for (int i = 0; i <= glyphCount; i++)
        {
            offsets[i] = format == 0
                ? BigEndian.U16(loca, 2 * i) * 2
                : checked((int)BigEndian.U32(loca, 4 * i));
        }
        return offsets;
    }

    /// <summary>
    /// Reads the character mapping from the "cmap" table. A font may contain several mappings for
    /// different platforms. Table header: version (2), count (2), then 8 bytes per mapping:
    /// platform (2), encoding (2), offset (4).
    /// </summary>
    static Dictionary<int, int> ReadCharacterMap(ReadOnlySpan<byte> cmap)
    {
        // Preference: Unicode for all planes (format 12, includes emoji and rare scripts) before
        // Unicode for the basic plane up to U+FFFF only (format 4).
        // Platform 3 = Windows (encoding 10 = full Unicode, 1 = basic plane), 0 = Unicode.
        int count = BigEndian.U16(cmap, 2);
        int best = -1, bestScore = 0;
        for (int i = 0; i < count; i++)
        {
            int record = 4 + 8 * i;
            int platform = BigEndian.U16(cmap, record);
            int encoding = BigEndian.U16(cmap, record + 2);
            int offset = checked((int)BigEndian.U32(cmap, record + 4));
            int format = BigEndian.U16(cmap, offset);

            int score = (platform, encoding, format) switch
            {
                (3, 10, 12) => 5,
                (0, 4, 12) or (0, 6, 12) => 4,
                (3, 1, 4) => 3,
                (0, _, 4) => 2,
                _ => 0,
            };
            if (score > bestScore)
            {
                bestScore = score;
                best = offset;
            }
        }
        if (best < 0)
            throw new FontFormatException("The font contains no Unicode character map.");

        var map = new Dictionary<int, int>();
        var table = cmap[best..];
        if (BigEndian.U16(table, 0) == 12)
            ReadCmapFormat12(table, map);
        else
            ReadCmapFormat4(table, map);
        return map;
    }

    /// <summary>
    /// Reads a mapping in format 4. Characters are split into "segments" of consecutive codes.
    /// Each segment has four values: end code, start code, delta (idDelta) and a reference into a
    /// glyph array (idRangeOffset).
    /// If idRangeOffset is 0, glyph = character + idDelta. Otherwise idRangeOffset, measured from
    /// its own position, points into an array of glyph numbers.
    /// </summary>
    static void ReadCmapFormat4(ReadOnlySpan<byte> table, Dictionary<int, int> map)
    {
        // Byte 6: segCountX2 = twice the number of segments. The four arrays follow each other
        // from byte 14; 2 padding bytes lie between end codes and start codes.
        int segCount = BigEndian.U16(table, 6) / 2;
        int endCodes = 14;
        int startCodes = endCodes + 2 * segCount + 2;
        int idDeltas = startCodes + 2 * segCount;
        int idRangeOffsets = idDeltas + 2 * segCount;

        for (int s = 0; s < segCount; s++)
        {
            int end = BigEndian.U16(table, endCodes + 2 * s);
            int start = BigEndian.U16(table, startCodes + 2 * s);
            int delta = BigEndian.I16(table, idDeltas + 2 * s);
            int rangeOffset = BigEndian.U16(table, idRangeOffsets + 2 * s);
            if (start == 0xFFFF)
                continue; // final segment without characters

            for (int c = start; c <= end; c++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (c + delta) & 0xFFFF; // arithmetic modulo 65536 as the specification requires
                }
                else
                {
                    int glyphOffset = idRangeOffsets + 2 * s + rangeOffset + 2 * (c - start);
                    if (glyphOffset + 2 > table.Length)
                        continue;
                    glyph = BigEndian.U16(table, glyphOffset);
                    if (glyph != 0)
                        glyph = (glyph + delta) & 0xFFFF;
                }
                if (glyph != 0)
                    map.TryAdd(c, glyph);
            }
        }
    }

    /// <summary>
    /// Reads a mapping in format 12: a list of groups, each with start character, end character
    /// and the glyph number of the start character (u32 each, from byte 16 on).
    /// The glyph numbers within a group are consecutive.
    /// </summary>
    static void ReadCmapFormat12(ReadOnlySpan<byte> table, Dictionary<int, int> map)
    {
        int groups = checked((int)BigEndian.U32(table, 12));
        for (int g = 0; g < groups; g++)
        {
            int record = 16 + 12 * g;
            int start = checked((int)BigEndian.U32(table, record));
            int end = checked((int)BigEndian.U32(table, record + 4));
            int glyph = checked((int)BigEndian.U32(table, record + 8));
            for (int c = start; c <= end; c++, glyph++)
            {
                if (glyph != 0)
                    map.TryAdd(c, glyph);
            }
        }
    }

    /// <summary>
    /// Reads the "name" table. Header: format (2), count (2), start of the string storage (2),
    /// then 12 bytes per record: platform, encoding, language, name ID, length, offset.
    /// Windows records (UTF-16BE, English preferred) take precedence over Mac records.
    /// </summary>
    /// <returns>Name ID → text, e.g. 1 = family, 6 = PostScript name.</returns>
    static Dictionary<int, string> ReadNames(ReadOnlySpan<byte> name)
    {
        var result = new Dictionary<int, string>();
        if (name.Length < 6)
            return result;

        int count = BigEndian.U16(name, 2);
        int storage = BigEndian.U16(name, 4);
        var priority = new Dictionary<int, int>(); // name ID → rank of the record chosen so far
        for (int i = 0; i < count; i++)
        {
            int record = 6 + 12 * i;
            if (record + 12 > name.Length)
                break;
            int platform = BigEndian.U16(name, record);
            int encoding = BigEndian.U16(name, record + 2);
            int language = BigEndian.U16(name, record + 4);
            int nameId = BigEndian.U16(name, record + 6);
            int length = BigEndian.U16(name, record + 8);
            int offset = storage + BigEndian.U16(name, record + 10);
            if (offset + length > name.Length)
                continue;

            int rank;
            string value;
            if (platform == 3 && encoding is 0 or 1)
            {
                // Windows; language 0x0409 = English (United States)
                rank = language == 0x0409 ? 3 : 2;
                value = Encoding.BigEndianUnicode.GetString(name.Slice(offset, length));
            }
            else if (platform == 1 && encoding == 0)
            {
                // Macintosh, Mac Roman encoding; Latin-1 is sufficient for ASCII names.
                rank = 1;
                value = Encoding.Latin1.GetString(name.Slice(offset, length));
            }
            else
            {
                continue;
            }

            if (rank > priority.GetValueOrDefault(nameId))
            {
                priority[nameId] = rank;
                result[nameId] = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Removes characters that must not appear in a PDF font name (spaces, characters outside
    /// ASCII and the PDF delimiters).
    /// </summary>
    static string SanitizePostScriptName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c > 32 && c < 127 && "()<>[]{}/%".IndexOf(c) < 0)
                sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : "Font";
    }
}
