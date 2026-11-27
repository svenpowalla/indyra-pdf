// Reads the color bitmaps of a font from the tables CBLC and CBDT.
//
// CBLC ("color bitmap location") says where the picture of each glyph lies; CBDT ("color bitmap
// data") contains the pictures. Both follow the OpenType specification (chapters "CBLC – Color
// Bitmap Location Table" and "CBDT – Color Bitmap Data Table"); the structure is the same as in
// the older monochrome tables EBLC/EBDT.
//
// CBLC: version (4), numSizes (4), then one "BitmapSize" record of 48 bytes per size ("strike"):
//    0 indexSubTableArrayOffset (4, from the start of CBLC)
//    8 numberOfIndexSubTables (4)
//   40 startGlyphIndex (2), 42 endGlyphIndex (2), 44 ppemX (1), 45 ppemY (1), 46 bitDepth (1)
// The index subtable array holds per range: firstGlyphIndex (2), lastGlyphIndex (2),
// additionalOffsetToIndexSubtable (4, from the start of the array). Each index subtable starts
// with indexFormat (2), imageFormat (2), imageDataOffset (4, into CBDT).
//
// Supported, as used by Noto Color Emoji and most other color bitmap fonts:
//   index formats 1 (offsets as u32) and 3 (offsets as u16), one entry more than glyphs,
//   image formats 17 (small metrics + PNG) and 18 (big metrics + PNG).
// Fonts with other formats are rejected with a clear message.

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts;

/// <summary>Finds and reads the color bitmap of a glyph; every bitmap is read only once.</summary>
internal sealed class ColorBitmapTable
{
    /// <summary>Image format 17: small glyph metrics (5 bytes), data length (4), PNG.</summary>
    const int SmallMetricsPng = 17;

    /// <summary>Image format 18: big glyph metrics (8 bytes), data length (4), PNG.</summary>
    const int BigMetricsPng = 18;

    /// <summary>The complete font file (shared with the font, not copied: the pictures are large).</summary>
    readonly byte[] _file;

    /// <summary>Start of the CBDT table in <see cref="_file"/>.</summary>
    readonly int _cbdtOffset;

    /// <summary>Pixels per em of the strike used.</summary>
    readonly int _pixelsPerEm;

    /// <summary>Position of every glyph picture in the CBDT table: glyph → (image format, start, end).</summary>
    readonly Dictionary<int, (int Format, int Start, int End)> _locations = [];

    /// <summary>Bitmaps read so far.</summary>
    readonly Dictionary<int, ColorBitmap?> _cache = [];

    /// <summary>Reads the location table. Only <see cref="TrueTypeFont"/> calls this.</summary>
    /// <param name="cblc">The CBLC table.</param>
    /// <param name="file">The complete font file.</param>
    /// <param name="cbdtOffset">Start of the CBDT table in <paramref name="file"/>.</param>
    /// <exception cref="FontFormatException">The tables use a format that is not supported.</exception>
    public ColorBitmapTable(ReadOnlySpan<byte> cblc, byte[] file, int cbdtOffset)
    {
        _file = file;
        _cbdtOffset = cbdtOffset;
        int sizeCount = checked((int)BigEndian.U32(cblc, 4));
        if (sizeCount == 0)
            throw new FontFormatException("The color bitmap font contains no bitmaps.");

        // Several strikes are possible; the largest gives the sharpest pictures.
        int best = 8;
        for (int i = 1; i < sizeCount; i++)
        {
            int record = 8 + 48 * i;
            if (cblc[record + 45] > cblc[best + 45])
                best = record;
        }
        _pixelsPerEm = cblc[best + 45];

        int array = checked((int)BigEndian.U32(cblc, best));
        int subtableCount = checked((int)BigEndian.U32(cblc, best + 8));
        for (int i = 0; i < subtableCount; i++)
        {
            int entry = array + 8 * i;
            int first = BigEndian.U16(cblc, entry);
            int last = BigEndian.U16(cblc, entry + 2);
            int subtable = array + checked((int)BigEndian.U32(cblc, entry + 4));
            ReadIndexSubtable(cblc, subtable, first, last);
        }
    }

    /// <summary>Returns the bitmap of a glyph, or null if the glyph has none (e.g. the space).</summary>
    public ColorBitmap? Get(int glyphId)
    {
        if (!_cache.TryGetValue(glyphId, out var bitmap))
        {
            bitmap = _locations.TryGetValue(glyphId, out var location) ? ReadBitmap(location) : null;
            _cache[glyphId] = bitmap;
        }
        return bitmap;
    }

    /// <summary>Records the picture positions of the glyphs <paramref name="first"/> to <paramref name="last"/>.</summary>
    void ReadIndexSubtable(ReadOnlySpan<byte> cblc, int subtable, int first, int last)
    {
        int indexFormat = BigEndian.U16(cblc, subtable);
        int imageFormat = BigEndian.U16(cblc, subtable + 2);
        int imageData = checked((int)BigEndian.U32(cblc, subtable + 4));
        if (imageFormat is not (SmallMetricsPng or BigMetricsPng))
            throw new FontFormatException($"Color bitmaps in image format {imageFormat} are not supported (only 17 and 18).");
        if (indexFormat is not (1 or 3))
            throw new FontFormatException($"Color bitmap index format {indexFormat} is not supported (only 1 and 3).");

        for (int glyph = first; glyph <= last; glyph++)
        {
            int start = ReadOffset(cblc, subtable, indexFormat, glyph - first);
            int end = ReadOffset(cblc, subtable, indexFormat, glyph - first + 1);
            if (end > start)
                _locations[glyph] = (imageFormat, imageData + start, imageData + end);
        }
    }

    /// <summary>
    /// Reads entry <paramref name="index"/> of the offset array that follows the 8-byte header of
    /// an index subtable: u32 values in format 1, u16 values in format 3. The array has one entry
    /// more than glyphs: glyph n spans from offset n to offset n + 1 (relative to imageDataOffset).
    /// </summary>
    static int ReadOffset(ReadOnlySpan<byte> cblc, int subtable, int indexFormat, int index) => indexFormat == 1
        ? checked((int)BigEndian.U32(cblc, subtable + 8 + 4 * index))
        : BigEndian.U16(cblc, subtable + 8 + 2 * index);

    /// <summary>Reads the metrics and the PNG of one glyph.</summary>
    ColorBitmap ReadBitmap((int Format, int Start, int End) location)
    {
        var data = _file.AsSpan(_cbdtOffset + location.Start, location.End - location.Start);
        // Small metrics: height (1), width (1), bearingX (i8), bearingY (i8), advance (1).
        // Big metrics: the same for horizontal layout, followed by 3 bytes for vertical layout.
        int metricsLength = location.Format == SmallMetricsPng ? 5 : 8;
        int height = data[0], width = data[1];
        int bearingX = (sbyte)data[2], bearingY = (sbyte)data[3];
        int length = checked((int)BigEndian.U32(data, metricsLength));
        var png = data.Slice(metricsLength + 4, length).ToArray();
        return new ColorBitmap(png, width, height, bearingX, bearingY, _pixelsPerEm);
    }
}
