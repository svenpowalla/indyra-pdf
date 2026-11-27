// Tests for the layout engine: font selection, line breaking, alignment, decorations, fields.

using System.Text;
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class ParagraphTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"))
        .Add(TestAssets.Font("LiberationSans-Bold.ttf"))
        .Add(TestAssets.Font("LiberationSerif-Italic.ttf"));

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 10 };

    /// <summary>Width of a text in the regular test style.</summary>
    static double Measure(string text) => new PdfDocument().AddFont(Fonts.Resolve(null, false, false)).MeasureText(text, 10);

    static (FormattedParagraph Formatted, RecordingSurface Surface) Layout(Paragraph paragraph, double width)
    {
        var formatted = paragraph.Format(Fonts, width, FieldValues.Estimate);
        var surface = new RecordingSurface();
        formatted.Draw(surface, 50, 100);
        return (formatted, surface);
    }

    /// <summary>Groups the drawn runs by baseline and returns the text of each line.</summary>
    static List<string> Lines(RecordingSurface surface) => surface.Runs
        .GroupBy(r => r.Baseline)
        .OrderBy(g => g.Key)
        .Select(g => string.Concat(g.SelectMany(r => r.Glyphs).Select(x => x.Text)))
        .ToList();

    [Fact]
    public void FontCollection_ResolvesStylesAndFallsBack()
    {
        Assert.Equal("Liberation Sans", Fonts.DefaultFamily);
        Assert.True(Fonts.Resolve("Liberation Sans", bold: true, italic: false).IsBold);
        Assert.False(Fonts.Resolve("Liberation Sans", bold: false, italic: false).IsBold);
        Assert.True(Fonts.Resolve("Liberation Serif", bold: false, italic: true).IsItalic);
        // Unknown family → default family
        Assert.Equal("Liberation Sans", Fonts.Resolve("Comic Sans", false, false).FamilyName);
        // Missing style (Sans italic) → closest match within the family
        Assert.Equal("Liberation Sans", Fonts.Resolve("Liberation Sans", false, true).FamilyName);
    }

    [Fact]
    public void Break_WrapsAtSpacesAndDropsThem()
    {
        var p = new Paragraph(Style).Add("one two three four");
        double width = Measure("three four") + 1; // "one two" fits as well, "one two three" does not

        var (formatted, surface) = Layout(p, width);

        Assert.Equal(2, formatted.LineCount);
        Assert.Equal(["one two", "three four"], Lines(surface));
    }

    [Fact]
    public void Break_LinesNeverExceedTheWidth()
    {
        var text = string.Join(' ', Enumerable.Repeat("Rechnung über 1.234,56 € netto", 20));
        var (_, surface) = Layout(new Paragraph(Style).Add(text), 200);

        Assert.All(surface.Runs, run => Assert.True(RecordingSurface.RightEdge(run) <= 50 + 200 + 0.001));
    }

    [Fact]
    public void Break_SplitsOverlongWords()
    {
        var (formatted, surface) = Layout(new Paragraph(Style).Add("Donaudampfschifffahrtsgesellschaft"), Measure("Donau") + 0.5);

        Assert.True(formatted.LineCount > 5);
        Assert.Equal("Donaudampfschifffahrtsgesellschaft", string.Concat(Lines(surface)));
        Assert.Equal("Donau", Lines(surface)[0]);
    }

    [Fact]
    public void Break_AfterHyphen()
    {
        var p = new Paragraph(Style).Add("Rechnungs-nummer");
        var (_, surface) = Layout(p, Measure("Rechnungs-") + 1);

        Assert.Equal(["Rechnungs-", "nummer"], Lines(surface));
    }

    [Fact]
    public void Break_NoBreakSpaceKeepsWordsTogether()
    {
        var p = new Paragraph(Style).Add("Summe 10 €");
        var (_, surface) = Layout(p, Measure("Summe 10") + 1);

        Assert.Equal(["Summe", "10 €"], Lines(surface));
    }

    [Fact]
    public void Break_FormatChangeInsideWordIsNoBreakOpportunity()
    {
        var p = new Paragraph(Style).Add("ab ").Add("fett", Style with { Bold = true }).Add("er");
        var (formatted, surface) = Layout(p, Measure("ab fetter") - 1);

        Assert.Equal(2, formatted.LineCount);
        Assert.Equal(["ab", "fetter"], Lines(surface));
    }

    [Fact]
    public void Break_ForcedLineBreak()
    {
        var (formatted, surface) = Layout(new Paragraph(Style).Add("first\nsecond"), 500);

        Assert.Equal(2, formatted.LineCount);
        Assert.Equal(["first", "second"], Lines(surface));
    }

    [Fact]
    public void EmptyParagraph_HasOneLineOfItsStyle()
    {
        var formatted = new Paragraph(Style).Format(Fonts, 300, FieldValues.Estimate);
        var font = Fonts.Resolve(null, false, false);

        Assert.Equal(1, formatted.LineCount);
        Assert.Equal(font.LineHeight * 10.0 / font.UnitsPerEm, formatted.Height, 6);
    }

    [Fact]
    public void Height_IncludesSpacingAndLineSpacing()
    {
        var single = new Paragraph(Style).Add("x").Format(Fonts, 300, FieldValues.Estimate).Height;
        var p = new Paragraph(Style) { SpaceBefore = 5, SpaceAfter = 7, LineSpacing = 1.5 }.Add("x");

        Assert.Equal(5 + single * 1.5 + 7, p.Format(Fonts, 300, FieldValues.Estimate).Height, 6);
    }

    [Theory]
    [InlineData(TextAlignment.Left)]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.Right)]
    public void Alignment_PositionsTheLine(TextAlignment alignment)
    {
        var p = new Paragraph(Style) { Alignment = alignment }.Add("Hello");
        var (_, surface) = Layout(p, 300);
        double width = Measure("Hello");
        double expectedStart = alignment switch
        {
            TextAlignment.Center => 50 + (300 - width) / 2,
            TextAlignment.Right => 50 + 300 - width,
            _ => 50,
        };

        Assert.Equal(expectedStart, surface.Runs[0].X, 6);
    }

    [Fact]
    public void Justify_StretchesAllButTheLastLine()
    {
        var text = string.Join(' ', Enumerable.Repeat("Blocksatz verteilt den Platz", 6));
        var p = new Paragraph(Style) { Alignment = TextAlignment.Justify }.Add(text);
        var (formatted, surface) = Layout(p, 220);

        var lines = surface.Runs.GroupBy(r => r.Baseline).OrderBy(g => g.Key).ToList();
        Assert.True(formatted.LineCount >= 3);
        foreach (var line in lines.Take(lines.Count - 1))
            Assert.Equal(50 + 220, line.Max(RecordingSurface.RightEdge), 3);
        Assert.True(lines[^1].Max(RecordingSurface.RightEdge) < 50 + 220 - 1);
    }

    [Fact]
    public void Indents_ShiftLines()
    {
        var p = new Paragraph(Style) { LeftIndent = 20, FirstLineIndent = 15 }.Add("one two three four five six");
        var (_, surface) = Layout(p, Measure("one two three") + 35 + 1);
        var starts = surface.Runs.GroupBy(r => r.Baseline).OrderBy(g => g.Key).Select(g => g.Min(r => r.X)).ToList();

        Assert.Equal(50 + 20 + 15, starts[0], 6);
        Assert.All(starts.Skip(1), s => Assert.Equal(50 + 20, s, 6));
    }

    [Fact]
    public void Styles_ChooseFontsAndColors()
    {
        var red = PdfColor.Rgb(1, 0, 0);
        var p = new Paragraph(Style).Add("plain ").Add("bold", Style with { Bold = true, Color = red });
        var (_, surface) = Layout(p, 400);

        Assert.Equal(2, surface.Runs.Count);
        Assert.False(surface.Runs[0].Font.IsBold);
        Assert.True(surface.Runs[1].Font.IsBold);
        Assert.Equal(red, surface.Runs[1].Color);
    }

    [Fact]
    public void Decorations_AndBackground_AreDrawn()
    {
        var yellow = PdfColor.Rgb(1, 1, 0);
        var p = new Paragraph(Style)
            .Add("under", Style with { Underline = true })
            .Add(" ")
            .Add("strike", Style with { Strikethrough = true, Background = yellow });
        var (_, surface) = Layout(p, 400);

        Assert.Equal(3, surface.Fills.Count); // underline, background, strikeout
        var background = surface.Fills.Single(f => f.Color == yellow);
        Assert.Equal(Measure("strike"), background.Box.Width, 6);
        var underline = surface.Fills.First(f => f.Color == PdfColor.Black && f.Box.Width == Measure("under"));
        Assert.True(underline.Box.Y > surface.Runs[0].Baseline); // below the baseline
        var strike = surface.Fills.First(f => f.Color == PdfColor.Black && f.Box.Width == Measure("strike"));
        Assert.True(strike.Box.Y < surface.Runs[0].Baseline - 2); // through the lowercase letters
    }

    [Fact]
    public void PageFields_AreResolved()
    {
        var p = new Paragraph(Style).Add("Seite ").Add(TextSpan.PageNumber(Style)).Add(" von ").Add(TextSpan.TotalPages(Style));
        var surface = new RecordingSurface();
        p.Format(Fonts, 400, new FieldValues(3, 12)).Draw(surface, 0, 0);

        Assert.Equal("Seite 3 von 12", surface.Text);
    }

    [Fact]
    public void Pdf_JustifiedTextUsesTjAdjustments()
    {
        var document = new PdfDocument { Compress = false };
        var page = document.AddPage();
        var p = new Paragraph(Style) { Alignment = TextAlignment.Justify }
            .Add(string.Join(' ', Enumerable.Repeat("Blocksatz im PDF", 8)));
        p.Format(Fonts, 200, FieldValues.Estimate).Draw(new PdfDrawingSurface(document, page), 50, 50);
        var pdf = Encoding.Latin1.GetString(document.ToArray());

        Assert.Contains("] TJ", pdf);
        Assert.Matches(@"> -\d+(\.\d+)? <", pdf); // negative numbers move glyphs to the right
    }
}
