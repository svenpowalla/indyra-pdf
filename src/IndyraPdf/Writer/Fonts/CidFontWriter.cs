// Builds the PDF objects for an embedded TrueType font with Unicode support.
//
// PDF offers several font types. For full Unicode, IndyraPdf uses a "composite font" (Type0),
// like PDFsharp 6.2.4 does; FPDF only supports 8-bit encodings. Object structure
// (ISO 32000-2, 9.7):
//
//   Type0 font  ── /Encoding /Identity-H: every 2 bytes in the page content are one code (CID)
//    │          ── /ToUnicode: table code → Unicode, so text can be copied and searched
//    └ CIDFontType2 ── /W: width of every code
//       │            ── /CIDToGIDMap /Identity: code = glyph number in the embedded font
//       └ FontDescriptor ── metrics, flags
//          └ FontFile2   ── the embedded subset font (TrueType file)

using IndyraPdf.Fonts;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Fonts;

/// <summary>Writes a font as CID font (Type0 / CIDFontType2) with subset and ToUnicode table.</summary>
internal sealed class CidFontWriter
{
    /// <summary>PDF expresses glyph metrics in units of 1/1000 of the font size.</summary>
    const double PdfGlyphUnits = 1000;

    /// <summary>The source font.</summary>
    readonly TrueTypeFont _font;

    /// <summary>The glyphs to embed.</summary>
    readonly GlyphSubset _subset;

    /// <summary>Whether streams are compressed.</summary>
    readonly bool _compress;

    /// <summary>Factor from font units to PDF glyph units.</summary>
    readonly double _scale;

    /// <summary>Creates the writer.</summary>
    public CidFontWriter(TrueTypeFont font, GlyphSubset subset, bool compress)
    {
        _font = font;
        _subset = subset;
        _compress = compress;
        _scale = PdfGlyphUnits / font.UnitsPerEm;
    }

    /// <summary>Registers all font objects in the table and returns the Type0 font dictionary.</summary>
    public PdfDictionary Write(PdfObjectTable table)
    {
        var subsetFont = TrueTypeSubsetter.Build(_font, _subset.Glyphs);
        string baseFont = SubsetTag.Create(subsetFont.Glyphs) + "+" + _font.PostScriptName;

        var fontFile = table.Add(new PdfStream(subsetFont.FontProgram) { Compress = _compress });
        fontFile["Length1"] = new PdfNumber(subsetFont.FontProgram.Length); // uncompressed length

        var descriptor = table.Add(BuildDescriptor(baseFont, fontFile));

        var cidFont = table.Add(new PdfDictionary("Font"));
        cidFont["Subtype"] = new PdfName("CIDFontType2");
        cidFont["BaseFont"] = new PdfName(baseFont);
        cidFont["CIDSystemInfo"] = BuildCidSystemInfo();
        cidFont["FontDescriptor"] = descriptor;
        cidFont["W"] = BuildWidths();
        cidFont["CIDToGIDMap"] = new PdfName("Identity");

        var toUnicode = table.Add(new PdfStream(ToUnicodeCMap.Build(_subset.UnicodeByCode)) { Compress = _compress });

        var type0 = table.Add(new PdfDictionary("Font"));
        type0["Subtype"] = new PdfName("Type0");
        type0["BaseFont"] = new PdfName(baseFont);
        type0["Encoding"] = new PdfName("Identity-H");
        type0["DescendantFonts"] = new PdfArray().Add(cidFont);
        type0["ToUnicode"] = toUnicode;
        return type0;
    }

    /// <summary>Font descriptor with metrics (ISO 32000-2, 9.8).</summary>
    PdfDictionary BuildDescriptor(string baseFont, PdfStream fontFile)
    {
        var bbox = _font.BoundingBox;
        var descriptor = new PdfDictionary("FontDescriptor");
        descriptor["FontName"] = new PdfName(baseFont);
        descriptor["Flags"] = new PdfNumber(DescriptorFlags());
        descriptor["FontBBox"] = new PdfArray(bbox.XMin * _scale, bbox.YMin * _scale, bbox.XMax * _scale, bbox.YMax * _scale);
        descriptor["ItalicAngle"] = new PdfNumber(_font.ItalicAngle);
        descriptor["Ascent"] = new PdfNumber(_font.Ascender * _scale);
        descriptor["Descent"] = new PdfNumber(_font.Descender * _scale);
        descriptor["CapHeight"] = new PdfNumber(_font.CapHeight * _scale);
        descriptor["StemV"] = new PdfNumber(EstimateStemWidth());
        descriptor["FontFile2"] = fontFile;
        return descriptor;
    }

    /// <summary>
    /// Character collection of the font. "Adobe-Identity-0" means: the codes have no predefined
    /// meaning; their meaning comes from the font itself and the ToUnicode table.
    /// </summary>
    static PdfDictionary BuildCidSystemInfo()
    {
        var info = new PdfDictionary();
        info["Registry"] = PdfString.FromAscii("Adobe");
        info["Ordering"] = PdfString.FromAscii("Identity");
        info["Supplement"] = new PdfNumber(0);
        return info;
    }

    /// <summary>
    /// Width array /W in the form [firstCode [w0 w1 w2 …]]: the widths of all codes starting at
    /// code 0, in 1/1000 of the font size (ISO 32000-2, 9.7.4.3).
    /// </summary>
    PdfArray BuildWidths()
    {
        var widths = new PdfArray();
        foreach (var glyph in _subset.Glyphs)
            widths.Add(new PdfNumber(_font.GetAdvanceWidth(glyph) * _scale));
        return new PdfArray().Add(new PdfNumber(0)).Add(widths);
    }

    /// <summary>
    /// Flags of the font descriptor (ISO 32000-2, table 121): FixedPitch = 1, Symbolic = 4,
    /// Italic = 64. CID fonts with their own glyph mapping are marked symbolic.
    /// </summary>
    int DescriptorFlags()
    {
        const int fixedPitch = 1, symbolic = 4, italic = 64;
        int flags = symbolic;
        if (_font.IsFixedPitch)
            flags |= fixedPitch;
        if (_font.IsItalic)
            flags |= italic;
        return flags;
    }

    /// <summary>
    /// Estimates the vertical stem width (StemV) from the weight. TrueType fonts do not store this
    /// value; the formula is a common approximation (weight 400 → 86, weight 700 → 159).
    /// </summary>
    double EstimateStemWidth() => Math.Round(10 + 220 * (_font.WeightClass - 50) / 900.0);
}
