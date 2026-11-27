// Tests for color emoji through a fallback font (Noto Color Emoji): reading the color bitmaps,
// choosing the font per grapheme cluster, composing sequences through ligatures, the PDF output
// as Type3 font with ToUnicode table, the preview and PDF/A and PDF/UA conformance.

using System.Text;
using System.Text.RegularExpressions;
using IndyraPdf.Drawing;
using IndyraPdf.Fonts;
using IndyraPdf.Layout;
using IndyraPdf.Skia;
using IndyraPdf.Writer;
using SkiaSharp;

namespace IndyraPdf.Tests;

public class EmojiTests
{
    /// <summary>The emoji font, loaded once (about 10 MB).</summary>
    static readonly Lazy<TrueTypeFont> EmojiFont = new(() => TrueTypeFont.Load(TestAssets.EmojiFont));

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 14 };

    static FontCollection Fonts() => new FontCollection().Add(TestAssets.SansRegular).AddFallback(EmojiFont.Value);

    /// <summary>A laid out document with one paragraph.</summary>
    static LaidOutDocument LaidOut(string text, FontCollection? fonts = null)
        => new LayoutDocument(new PageTemplate(PageSize.A4, new Box(50, 50, 495, 740)))
            .Add(new Paragraph(Style).Add(text))
            .Layout(fonts ?? Fonts());

    /// <summary>Lays out one paragraph and returns what was drawn.</summary>
    static RecordingSurface Draw(string text, FontCollection? fonts = null)
    {
        var surface = new RecordingSurface();
        LaidOut(text, fonts).DrawPage(0, surface);
        return surface;
    }

    /// <summary>The glyphs drawn in the emoji font.</summary>
    static List<PositionedGlyph> EmojiGlyphs(RecordingSurface surface)
        => surface.Runs.Where(r => r.Font.HasColorBitmaps).SelectMany(r => r.Glyphs).ToList();

    static PdfDocument Pdf(string text, PdfConformance conformance = PdfConformance.None, bool tagged = false)
    {
        var document = new PdfDocument { Compress = false, Conformance = conformance, Tagged = tagged, UniversalAccessibility = tagged, Language = "de-DE" };
        document.Info.Title = "Emoji";
        LaidOut(text).AppendTo(document);
        return document;
    }

    [EmojiFontFact]
    public void ColorFont_IsReadWithoutOutlines()
    {
        var font = EmojiFont.Value;
        Assert.False(font.HasOutlines);
        Assert.True(font.HasColorBitmaps);
        var bitmap = font.GetColorBitmap(font.GetGlyphId(0x1F600)); // 😀
        Assert.NotNull(bitmap);
        Assert.Equal(109, bitmap.PixelsPerEm);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], bitmap.Png[..4]);
    }

    [EmojiFontFact]
    public void ColorFont_CannotBeAMainFont()
    {
        Assert.Throws<ArgumentException>(() => new FontCollection().Add(EmojiFont.Value));
        Assert.Throws<FontFormatException>(() => new PdfDocument().AddFont(EmojiFont.Value));
    }

    [EmojiFontFact]
    public void MissingCharacters_UseTheFallback_PresentCharactersStay()
    {
        var surface = Draw("Hallo 😀 ©");
        Assert.Equal("😀", Assert.Single(EmojiGlyphs(surface)).Text);
        Assert.Equal("Hallo 😀 ©", surface.Text);
    }

    [EmojiFontFact]
    public void WithoutFallback_EmojiBecomeQuestionMarks()
        => Assert.Equal("Hallo ?", Draw("Hallo 😀", new FontCollection().Add(TestAssets.SansRegular)).Text);

    [EmojiFontTheory]
    [InlineData("👍🏽")]          // skin tone modifier
    [InlineData("🇩🇪")]          // two regional indicators
    [InlineData("👨‍👩‍👧")]   // zero width joiner sequence
    [InlineData("1️⃣")]          // keycap: digit + U+FE0F + U+20E3
    [InlineData("©️")]           // U+FE0F asks for the emoji form although Liberation has ©
    public void Sequences_BecomeOneGlyph_AndKeepTheirText(string sequence)
    {
        var glyph = Assert.Single(EmojiGlyphs(Draw(sequence)));
        Assert.Equal(sequence, glyph.Text);
        Assert.NotNull(EmojiFont.Value.GetColorBitmap(glyph.GlyphId));
    }

    [EmojiFontFact]
    public void Emoji_DoNotChangeTheLineHeight()
    {
        var fonts = Fonts();
        double plain = new Paragraph(Style).Add("Text").Format(fonts, 400, FieldValues.Estimate).Height;
        double withEmoji = new Paragraph(Style).Add("Text 😀").Format(fonts, 400, FieldValues.Estimate).Height;
        Assert.Equal(plain, withEmoji, 6);
    }

    [EmojiFontFact]
    public void Pdf_WritesAType3FontWithToUnicode()
    {
        var text = Encoding.Latin1.GetString(Pdf("A 😀 👍🏽").ToArray());
        Assert.Contains("/Subtype/Type3", text);
        Assert.Contains("/FontMatrix[0.001 0 0 0.001 0 0]", text);
        Assert.Matches(@"/T1 14 Tf\n", text);
        Assert.Matches(@"\d+(\.\d+)? 0 d0\nq [\d.]+ 0 0 [\d.]+ -?[\d.]+ -?[\d.]+ cm /E1 Do Q", text);
        // ToUnicode: code 01 → 😀 (UTF-16BE D83D DE00), code 02 → 👍🏽 (D83D DC4D D83C DFFD).
        Assert.Contains("<01> <D83DDE00>", text);
        Assert.Contains("<02> <D83DDC4DD83CDFFD>", text);
        Assert.Contains("1 begincodespacerange\n<00> <FF>", text);
    }

    [EmojiFontFact]
    public void ManyDifferentEmoji_UseSeveralType3Fonts()
    {
        var font = EmojiFont.Value;
        var emoji = Enumerable.Range(0x1F300, 0x400).Where(font.HasCharacter).Take(300).Select(char.ConvertFromUtf32);
        var text = Encoding.Latin1.GetString(Pdf(string.Join(' ', emoji)).ToArray());
        Assert.Equal(2, Regex.Matches(text, "/Subtype/Type3").Count); // 254 glyphs fit into one Type3 font
    }

    [EmojiFontFact]
    public void Preview_DrawsTheColorPicture()
    {
        using var bitmap = SKBitmap.Decode(PngPreview.RenderPage(LaidOut("😀"), 0, 144));
        // The smiley is yellow: look for a clearly yellow pixel where the emoji is (x 50–70 pt, y 50–70 pt).
        bool yellow = false;
        for (int y = 100; y < 140 && !yellow; y++)
            for (int x = 100; x < 140 && !yellow; x++)
            {
                var p = bitmap.GetPixel(x, y);
                yellow = p.Red > 200 && p.Green > 150 && p.Blue < 100;
            }
        Assert.True(yellow);
    }

    [VeraPdfFact]
    public void VeraPdf_PdfA3B()
    {
        if (!File.Exists(TestAssets.EmojiFont))
            return;
        var (passed, report) = VeraPdf.Validate(Pdf("Grüße 😀 👍🏽 🇩🇪", PdfConformance.PdfA3B).ToArray(), "3b");
        Assert.True(passed, report);
    }

    [VeraPdfFact]
    public void VeraPdf_PdfA3AAndPdfUA1()
    {
        if (!File.Exists(TestAssets.EmojiFont))
            return;
        var pdf = Pdf("Grüße 😀 👍🏽 🇩🇪", PdfConformance.PdfA3A, tagged: true).ToArray();
        foreach (var flavour in new[] { "3a", "ua1" })
        {
            var (passed, report) = VeraPdf.Validate(pdf, flavour);
            Assert.True(passed, $"veraPDF ({flavour}) failed:\n{report}");
        }
    }
}
