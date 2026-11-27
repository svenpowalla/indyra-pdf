// Tests for kerning: reading GPOS/kern, class-based pairs, and its effect on layout and PDF.
// Reference values were taken from the fonts with fontTools.

using System.Text;
using IndyraPdf.Fonts;
using IndyraPdf.Fonts.Kerning;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class KerningTests
{
    static readonly TrueTypeFont Sans = TrueTypeFont.Load(TestAssets.SansRegular);

    static int Kern(TrueTypeFont font, char left, char right) => font.GetKerning(font.GetGlyphId(left), font.GetGlyphId(right));

    [Theory]
    [InlineData('A', 'V', -152)]
    [InlineData('A', 'T', -152)]
    [InlineData('A', 'W', -76)]
    [InlineData('T', 'o', -227)]
    [InlineData('L', 'T', -152)]
    [InlineData('H', 'H', 0)]
    public void Sans_MatchesFontToolsValues(char left, char right, int expected)
    {
        Assert.Equal(expected, Kern(Sans, left, right));
    }

    [Fact]
    public void SerifItalic_HasItsOwnValues()
    {
        var italic = TrueTypeFont.Load(TestAssets.Font("LiberationSerif-Italic.ttf"));
        Assert.Equal(-102, Kern(italic, 'A', 'V'));
    }

    [Fact]
    public void ClassPairTable_LooksUpClasses()
    {
        // Glyphs 10, 11 are left class 1; glyph 20 is right class 1; everything else class 0.
        var values = new int[2, 2];
        values[1, 1] = -50;
        var table = new ClassPairTable([10, 11, 12], new() { [10] = 1, [11] = 1 }, new() { [20] = 1 }, values);

        Assert.True(table.TryGetAdjustment(11, 20, out int a) && a == -50);
        Assert.True(table.TryGetAdjustment(12, 20, out int b) && b == 0);   // covered, class 0
        Assert.False(table.TryGetAdjustment(13, 20, out _));                // not covered
    }

    [Fact]
    public void Coverage_AndClassDefinition_BothFormats()
    {
        // Coverage format 2: one range 5–7 starting at coverage index 0.
        byte[] coverage = [0, 2, 0, 1, 0, 5, 0, 7, 0, 0];
        Assert.Equal([5, 6, 7], OpenTypeCommon.ReadCoverage(coverage, 0));

        // Class definition format 1: start glyph 3, two glyphs with classes 4 and 9.
        byte[] classes1 = [0, 1, 0, 3, 0, 2, 0, 4, 0, 9];
        Assert.Equal(new Dictionary<int, int> { [3] = 4, [4] = 9 }, OpenTypeCommon.ReadClassDefinition(classes1, 0));

        // Class definition format 2: range 8–9 is class 2.
        byte[] classes2 = [0, 2, 0, 1, 0, 8, 0, 9, 0, 2];
        Assert.Equal(new Dictionary<int, int> { [8] = 2, [9] = 2 }, OpenTypeCommon.ReadClassDefinition(classes2, 0));
    }

    [Fact]
    public void ValueRecord_FindsXAdvanceAfterPlacements()
    {
        // Value format 5 = XPlacement + XAdvance: XPlacement 7, XAdvance −30.
        byte[] record = [0, 7, 0xFF, 0xE2];
        Assert.Equal(4, OpenTypeCommon.ValueRecordSize(5));
        Assert.Equal(-30, OpenTypeCommon.ReadXAdvance(record, 0, 5));
        Assert.Equal(0, OpenTypeCommon.ReadXAdvance(record, 0, 1));
    }

    static readonly FontCollection Fonts = new FontCollection().Add(TestAssets.SansRegular);

    static double LineWidth(string text, bool kerning)
    {
        var style = TextStyle.Default with { FontSize = 10, Kerning = kerning };
        var surface = new RecordingSurface();
        new Paragraph(style) { Alignment = TextAlignment.Right }.Add(text).Format(Fonts, 500, FieldValues.Estimate).Draw(surface, 0, 0);
        return 500 - surface.Runs[0].X; // right aligned: the start reveals the measured width
    }

    [Fact]
    public void Layout_UsesKerningForMeasuring()
    {
        double natural = new PdfDocument().AddFont(Sans).MeasureText("AVATAR", 10);

        Assert.Equal(natural, LineWidth("AVATAR", kerning: false), 6);
        Assert.True(LineWidth("AVATAR", kerning: true) < natural - 2);
    }

    [Fact]
    public void Pdf_WritesKerningAsTjAdjustment()
    {
        var document = new PdfDocument { Compress = false };
        var page = document.AddPage();
        new Paragraph(TextStyle.Default with { FontSize = 10 }).Add("AV")
            .Format(Fonts, 300, FieldValues.Estimate).Draw(new PdfDrawingSurface(document, page), 50, 50);
        var pdf = Encoding.Latin1.GetString(document.ToArray());

        // −152/2048 em closer → the V moves left by 74.2188/1000 em, written as positive number.
        Assert.Contains("[<0001> 74.2188 <0002>] TJ", pdf);
    }
}
