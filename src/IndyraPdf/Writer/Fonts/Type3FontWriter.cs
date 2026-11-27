// Builds the PDF objects of a Type3 font whose glyphs are the color pictures of an emoji font.
//
// Object structure (ISO 32000-2, 9.6.4 "Type 3 fonts"):
//
//   Type3 font ── /FontMatrix [0.001 0 0 0.001 0 0]: glyph space = 1/1000 of the font size
//    │         ── /Encoding /Differences [1 /g883 2 /g1504 …]: code → glyph name
//    │         ── /FirstChar, /LastChar, /Widths: advance of every code in glyph space
//    │         ── /ToUnicode: code → text (one code may stand for a whole emoji sequence)
//    ├ /CharProcs ── one content stream per glyph name, e.g.
//    │                 "1245 0 d0 q 1245 0 0 1172 0 -229 cm /E1 Do Q"
//    │                 d0 = glyph width, glyph may use colors (unlike d1);
//    │                 cm scales the unit square to the picture, Do draws it.
//    ├ /Resources ── /XObject: the pictures (PNG with transparency → image + soft mask)
//    └ /FontDescriptor ── needed for tagged PDF (ISO 32000-2, table 110)
//
// Type3 fonts are allowed in PDF/A-2/3 and PDF/UA-1: their glyphs are part of the file anyway.

using System.Globalization;
using System.Text;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Fonts;

/// <summary>Writes a <see cref="Type3Font"/> with its glyph programs, pictures and ToUnicode table.</summary>
internal sealed class Type3FontWriter
{
    /// <summary>Glyph space units per font size, as set by the font matrix.</summary>
    const double GlyphUnits = 1000;

    /// <summary>The font to write.</summary>
    readonly Type3Font _font;

    /// <summary>Whether streams are compressed.</summary>
    readonly bool _compress;

    /// <summary>Creates the writer.</summary>
    public Type3FontWriter(Type3Font font, bool compress)
    {
        _font = font;
        _compress = compress;
    }

    /// <summary>Registers all objects in the table and returns the font dictionary.</summary>
    public PdfDictionary Write(PdfObjectTable table)
    {
        var source = _font.Font;
        double scale = GlyphUnits / source.UnitsPerEm;
        var glyphs = _font.GlyphsByCode;
        int firstCode = glyphs.Keys.First(), lastCode = glyphs.Keys.Last();

        var charProcs = new PdfDictionary();
        var pictures = new PdfDictionary();
        var differences = new PdfArray();
        var widths = new double[lastCode - firstCode + 1];
        // Bounding box of all glyphs in glyph space: x from 0, y from descender to ascender at least.
        double minX = 0, minY = source.Descender * scale, maxX = 0, maxY = source.Ascender * scale;

        foreach (var (code, (glyphId, _)) in glyphs)
        {
            string glyphName = "g" + glyphId.ToString(CultureInfo.InvariantCulture);
            double width = source.GetAdvanceWidth(glyphId) * scale;
            widths[code - firstCode] = width;
            differences.Add(new PdfNumber(code)).Add(new PdfName(glyphName));
            maxX = Math.Max(maxX, width);

            // "w 0 d0": the glyph is width w wide and sets its own colors (ISO 32000-2, 9.6.4).
            var program = new StringBuilder();
            program.Append(PdfFormat.Number(width)).Append(" 0 d0\n");
            if (source.GetColorBitmap(glyphId) is { } bitmap)
            {
                // Placement at font size 1000 = position in glyph space.
                var (left, top, w, h) = bitmap.Placement(GlyphUnits);
                string pictureName = "E" + code.ToString(CultureInfo.InvariantCulture);
                pictures[pictureName] = new PdfImage(bitmap.Png, pictureName).Build(table);
                program.Append("q ").Append(PdfFormat.Number(w)).Append(" 0 0 ").Append(PdfFormat.Number(h)).Append(' ')
                       .Append(PdfFormat.Number(left)).Append(' ').Append(PdfFormat.Number(top - h)).Append(" cm ")
                       .Append(PdfFormat.Name(pictureName)).Append(" Do Q\n");
                minX = Math.Min(minX, left);
                maxX = Math.Max(maxX, left + w);
                minY = Math.Min(minY, top - h);
                maxY = Math.Max(maxY, top);
            }
            charProcs[glyphName] = table.Add(new PdfStream(Encoding.ASCII.GetBytes(program.ToString())) { Compress = _compress });
        }

        var encoding = new PdfDictionary("Encoding");
        encoding["Differences"] = differences;

        var resources = new PdfDictionary();
        if (pictures.Count > 0)
            resources["XObject"] = pictures;

        var widthArray = new PdfArray();
        foreach (double width in widths)
            widthArray.Add(new PdfNumber(width));

        var bbox = new PdfArray(minX, minY, maxX, maxY);
        var toUnicode = table.Add(new PdfStream(ToUnicodeCMap.Build(glyphs.ToDictionary(g => g.Key, g => g.Value.Text), codeBytes: 1)) { Compress = _compress });

        var font = table.Add(new PdfDictionary("Font"));
        font["Subtype"] = new PdfName("Type3");
        font["FontBBox"] = bbox;
        font["FontMatrix"] = new PdfArray(1 / GlyphUnits, 0, 0, 1 / GlyphUnits, 0, 0);
        font["CharProcs"] = charProcs;
        font["Encoding"] = encoding;
        font["FirstChar"] = new PdfNumber(firstCode);
        font["LastChar"] = new PdfNumber(lastCode);
        font["Widths"] = widthArray;
        font["Resources"] = resources;
        font["FontDescriptor"] = table.Add(BuildDescriptor(bbox));
        font["ToUnicode"] = toUnicode;
        return font;
    }

    /// <summary>
    /// Font descriptor: name, flags and angle. A Type3 font needs no further metrics; its glyphs
    /// are drawn by their own programs. Flag 4 = symbolic (the codes have no standard meaning).
    /// </summary>
    PdfDictionary BuildDescriptor(PdfArray bbox)
    {
        const int symbolic = 4;
        var descriptor = new PdfDictionary("FontDescriptor");
        descriptor["FontName"] = new PdfName(_font.Font.PostScriptName + "-" + _font.ResourceName);
        descriptor["Flags"] = new PdfNumber(symbolic);
        descriptor["FontBBox"] = bbox;
        descriptor["ItalicAngle"] = new PdfNumber(0);
        return descriptor;
    }
}
