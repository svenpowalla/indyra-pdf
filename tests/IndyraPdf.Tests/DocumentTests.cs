// Tests for the PDF writer: file structure, page content, fonts, images and formatting.

using System.Text;
using System.Text.RegularExpressions;
using IndyraPdf.Writer;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Tests;

public class DocumentTests
{
    static readonly DateTimeOffset FixedDate = new(2026, 11, 22, 10, 30, 0, TimeSpan.FromHours(1));

    static PdfDocument CreateSample(bool compress = true)
    {
        var document = new PdfDocument { Compress = compress };
        document.Info.Title = "Prüfung";
        document.Info.CreationDate = FixedDate;
        var font = document.AddFont(TestAssets.SansRegular);
        var page = document.AddPage(PageSize.A4);
        page.Canvas.SetFillColor(PdfColor.Rgb(1, 0, 0))
            .DrawText(font, 12, 72, 72, "Grüße € 😀")
            .DrawRectangle(10, 20, 30, 40, PdfPaint.Fill);
        return document;
    }

    static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Fact]
    public void Save_WritesHeaderBinaryMarkerAndTrailer()
    {
        var pdf = CreateSample().ToArray();
        var text = Latin1(pdf);

        Assert.StartsWith("%PDF-1.7\n%", text);
        Assert.True(pdf[10] > 127 && pdf[11] > 127 && pdf[12] > 127 && pdf[13] > 127);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Matches(@"trailer\n<</Size \d+/Root 1 0 R/Info \d+ 0 R/ID\[<[0-9A-F]{32}> <[0-9A-F]{32}>\]>>", text);
    }

    [Fact]
    public void Save_CrossReferenceOffsetsPointToObjects()
    {
        var text = Latin1(CreateSample().ToArray());

        int startxref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n%%EOF").Groups[1].Value);
        Assert.StartsWith("xref\n0 ", text[startxref..]);

        var header = Regex.Match(text[startxref..], @"xref\n0 (\d+)\n");
        int count = int.Parse(header.Groups[1].Value);
        int entries = startxref + header.Length;
        Assert.Equal("0000000000 65535 f\r\n", text.Substring(entries, 20));
        for (int i = 1; i < count; i++)
        {
            var entry = text.Substring(entries + 20 * i, 20);
            Assert.Matches(@"^\d{10} 00000 n\r\n$", entry);
            int offset = int.Parse(entry[..10]);
            Assert.StartsWith($"{i} 0 obj\n", text[offset..]);
        }
    }

    [Fact]
    public void Save_IsDeterministic()
    {
        Assert.Equal(CreateSample().ToArray(), CreateSample().ToArray());
    }

    [Fact]
    public void Save_CanBeCalledRepeatedly()
    {
        var document = CreateSample();
        var first = document.ToArray();
        Assert.Equal(first, document.ToArray());
    }

    [Fact]
    public void Save_WithoutPages_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocument().ToArray());
    }

    [Fact]
    public void Content_UsesPdfCoordinatesAndOperators()
    {
        var text = Latin1(CreateSample(compress: false).ToArray());

        Assert.Contains("1 0 0 rg\n", text);
        Assert.Contains("/F1 12 Tf\n", text);
        // y = 72 from the top equals 841.8898 − 72 from the bottom.
        Assert.Contains("1 0 0 1 72 769.8898 Tm\n", text);
        // Rectangle: lower left corner at y = 841.8898 − 60.
        Assert.Contains("10 781.8898 30 40 re\nf\n", text);
    }

    [Fact]
    public void Font_IsEmbeddedAsCidFontWithToUnicode()
    {
        var text = Latin1(CreateSample(compress: false).ToArray());

        Assert.Matches(@"/Subtype/Type0/BaseFont/[A-Z]{6}\+LiberationSans/Encoding/Identity-H", text);
        Assert.Contains("/Subtype/CIDFontType2", text);
        Assert.Contains("/CIDToGIDMap/Identity", text);
        Assert.Contains("/FontFile2", text);
        // Glyphs in the order of first use: G r ü ß e space €
        Assert.Contains("<0001> <0047>", text); // G
        Assert.Contains("<0003> <00FC>", text); // ü
        Assert.Contains("<0007> <20AC>", text); // €
        // The emoji is missing in the font and replaced by "?".
        Assert.Contains("<0008> <003F>", text);
        // Page content: G r ü ß e space € space ?
        Assert.Contains("<000100020003000400050006000700060008> Tj", text);
    }

    [Fact]
    public void Font_WidthsAreScaledToThousandUnits()
    {
        var text = Latin1(CreateSample(compress: false).ToArray());
        // Widths of .notdef and "G" in Liberation Sans: 1536 and 1593 of 2048 units.
        Assert.Contains("/W[0 [750 777.832", text);
    }

    [Fact]
    public void UnusedFontsAndImages_AreNotWritten()
    {
        var document = new PdfDocument { Compress = false };
        document.AddFont(TestAssets.SansRegular);
        document.AddImage(TestAssets.Image("sample-rgb.jpg"));
        document.AddPage();
        var text = Latin1(document.ToArray());

        Assert.DoesNotContain("/FontFile2", text);
        Assert.DoesNotContain("/Subtype/Image", text);
    }

    [Fact]
    public void Images_AreSharedBetweenPages()
    {
        var document = new PdfDocument { Compress = false };
        var image = document.AddImage(TestAssets.Image("sample-rgba.png"));
        document.AddPage().Canvas.DrawImage(image, 0, 0, 100, 50);
        document.AddPage().Canvas.DrawImage(image, 0, 0, 100, 50);
        var text = Latin1(document.ToArray());

        Assert.Equal(2, Regex.Matches(text, "/Subtype/Image").Count); // image + soft mask
        Assert.Contains("/SMask ", text);
        Assert.Contains("100 0 0 50 0 791.8898 cm\n/Im1 Do\n", text);
    }

    [Fact]
    public void InfoDictionary_UsesUtf16ForNonAscii()
    {
        var text = Latin1(CreateSample(compress: false).ToArray());
        // "Prüfung" as UTF-16BE with byte order mark
        Assert.Contains("/Title<FEFF0050007200FC00660075006E0067>", text);
        Assert.Contains("/CreationDate(D:20261122103000+01'00')", text);
    }

    [Fact]
    public void UnbalancedSaveState_IsClosedAutomatically()
    {
        var document = new PdfDocument { Compress = false };
        document.AddPage().Canvas.SaveState().SaveState().RestoreState();
        var text = Latin1(document.ToArray());
        Assert.Contains("q\nq\nQ\nQ\n", text);
        Assert.Throws<InvalidOperationException>(() => new PdfDocument().AddPage().Canvas.RestoreState());
    }

    [Fact]
    public void MeasureText_MatchesFontUnits()
    {
        var font = new PdfDocument().AddFont(TestAssets.SansRegular);
        // "A" = 1366 units at 2048 units per em, font size 10 → 6.669921875 pt
        Assert.Equal(1366 * 10 / 2048.0, font.MeasureText("A", 10), 6);
        Assert.Equal(2 * font.MeasureText("A", 10), font.MeasureText("AA", 10), 6);
        Assert.Equal(0, font.MeasureText("\n", 10));
    }

    [Fact]
    public void Number_Formatting()
    {
        Assert.Equal("1", PdfFormat.Number(1.0));
        Assert.Equal("0.5", PdfFormat.Number(0.5));
        Assert.Equal("1.2346", PdfFormat.Number(1.23456));
        Assert.Equal("0", PdfFormat.Number(-0.00001));
        Assert.Equal("-12.25", PdfFormat.Number(-12.25));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormat.Number(double.NaN));
    }

    [Fact]
    public void Name_EscapesSpecialCharacters()
    {
        Assert.Equal("/Type", PdfFormat.Name("Type"));
        Assert.Equal("/A#20B#23C", PdfFormat.Name("A B#C"));
    }

    [Fact]
    public void Date_Formatting()
    {
        Assert.Equal("D:20261122103000+01'00'", PdfFormat.Date(FixedDate));
        Assert.Equal("D:20260101000000Z", PdfFormat.Date(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void PageSizes()
    {
        Assert.Equal(595.2756, PageSize.A4.Width, 4);
        Assert.Equal(841.8898, PageSize.A4.Height, 4);
        Assert.Equal(PageSize.A4.Height, PageSize.A4.Landscape.Width);
        Assert.Equal(PdfColor.FromRgb(0x1F, 0x5F, 0xA8), PdfColor.FromHex("#1F5FA8"));
    }
}
