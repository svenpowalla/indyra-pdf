// Tests for the page flow of the layout engine: page breaks, templates, tables, frames,
// images, list markers, page numbers and merging documents.

using System.Text;
using System.Text.RegularExpressions;
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class LayoutTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"))
        .Add(TestAssets.Font("LiberationSans-Bold.ttf"));

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 10 };

    /// <summary>A4 template with a content area 50 pt from every edge.</summary>
    static PageTemplate A4(double top = 50, double bottom = 50)
        => new(PageSize.A4, new Box(50, top, PageSize.A4.Width - 100, PageSize.A4.Height - top - bottom));

    /// <summary>Draws every page onto its own recording surface.</summary>
    static List<RecordingSurface> DrawAll(LaidOutDocument document)
    {
        var surfaces = new List<RecordingSurface>();
        for (int i = 0; i < document.PageCount; i++)
        {
            var surface = new RecordingSurface();
            document.DrawPage(i, surface);
            surfaces.Add(surface);
        }
        return surfaces;
    }

    static Paragraph Numbered(int n) => new Paragraph(Style).Add($"Absatz {n:000} mit etwas Text, damit die Seite voll wird.");

    [Fact]
    public void Content_FlowsAcrossPagesInOrder()
    {
        var document = new LayoutDocument(A4());
        for (int i = 1; i <= 200; i++)
            document.Add(Numbered(i));

        var laidOut = document.Layout(Fonts);
        var pages = DrawAll(laidOut);

        Assert.True(laidOut.PageCount >= 3);
        var numbers = Regex.Matches(string.Concat(pages.Select(p => p.Text)), @"Absatz (\d{3})").Select(m => int.Parse(m.Groups[1].Value));
        Assert.Equal(Enumerable.Range(1, 200), numbers);
        // No line below the content area
        Assert.All(pages.SelectMany(p => p.Runs), r => Assert.True(r.Baseline <= PageSize.A4.Height - 50));
    }

    [Fact]
    public void LongParagraph_IsSplitBetweenLines()
    {
        var text = string.Join(' ', Enumerable.Repeat("Ein sehr langer Absatz läuft über mehrere Seiten.", 400));
        var laidOut = new LayoutDocument(A4()).Add(new Paragraph(Style).Add(text)).Layout(Fonts);
        var pages = DrawAll(laidOut);

        Assert.True(laidOut.PageCount >= 2);
        Assert.Equal(text.Replace(" ", ""), string.Concat(pages.Select(p => p.Text)).Replace(" ", ""));
    }

    [Fact]
    public void FirstAndFollowingTemplates_HaveDifferentAreas()
    {
        var document = new LayoutDocument(A4(top: 300), A4(top: 60));
        for (int i = 1; i <= 120; i++)
            document.Add(Numbered(i));
        var pages = DrawAll(document.Layout(Fonts));

        Assert.True(pages[0].Runs.Min(r => r.Baseline) > 300);
        Assert.True(pages[1].Runs.Min(r => r.Baseline) < 80);
    }

    [Fact]
    public void FixedFooter_ShowsPageNumbers()
    {
        var footer = new Paragraph(Style) { Alignment = TextAlignment.Right }
            .Add("Seite ").Add(TextSpan.PageNumber(Style)).Add(" von ").Add(TextSpan.TotalPages(Style));
        var template = A4(bottom: 80).AddFixed(new FixedBlock(new Box(50, 800, 495, 20), footer));
        var document = new LayoutDocument(template);
        for (int i = 1; i <= 150; i++)
            document.Add(Numbered(i));

        var laidOut = document.Layout(Fonts);
        var pages = DrawAll(laidOut);

        for (int i = 0; i < pages.Count; i++)
            Assert.EndsWith($"Seite {i + 1} von {laidOut.PageCount}", pages[i].Text);
    }

    [Fact]
    public void PageNumberInFlowingText_UsesTheRealTotal()
    {
        var document = new LayoutDocument(A4());
        document.Add(new Paragraph(Style).Add("Dieses Dokument hat ").Add(TextSpan.TotalPages(Style)).Add(" Seiten."));
        for (int i = 1; i <= 150; i++)
            document.Add(Numbered(i));

        var laidOut = document.Layout(Fonts);

        Assert.StartsWith($"Dieses Dokument hat {laidOut.PageCount} Seiten.", DrawAll(laidOut)[0].Text);
    }

    [Fact]
    public void MinimumPageCount_AddsEmptyPages()
    {
        var laidOut = new LayoutDocument(A4(), A4()) { MinimumPageCount = 2 }.Layout(Fonts);
        Assert.Equal(2, laidOut.PageCount);
    }

    [Fact]
    public void Validation_RejectsDifferentContentWidths()
    {
        var first = new PageTemplate(PageSize.A4, new Box(50, 50, 400, 700));
        var following = new PageTemplate(PageSize.A4, new Box(50, 50, 300, 700));
        var document = new LayoutDocument(first, following).Add(Numbered(1));
        Assert.Throws<InvalidOperationException>(() => document.Layout(Fonts));
    }

    static Frame Cell(string text, bool bold = false) => new Frame { Padding = Thickness.Uniform(3), Borders = BoxBorders.All(LineStyle.Thin) }
        .Add(new Paragraph(bold ? Style with { Bold = true } : Style).Add(text));

    [Fact]
    public void Table_RepeatsHeaderAndKeepsRowsWhole()
    {
        var table = new Table { HeaderRowCount = 1 }
            .AddColumn(TableColumn.Fixed(40))
            .AddColumn(TableColumn.Relative(3))
            .AddColumn(TableColumn.Relative(1));
        table.AddRow(new TableRow().Add(Cell("Pos", true)).Add(Cell("Beschreibung", true)).Add(Cell("Betrag", true)));
        for (int i = 1; i <= 120; i++)
            table.AddRow(new TableRow().Add(Cell(i.ToString())).Add(Cell($"Zeile {i:000}")).Add(Cell("1,00 €")));

        var laidOut = new LayoutDocument(A4()).Add(table).Layout(Fonts);
        var pages = DrawAll(laidOut);

        Assert.True(laidOut.PageCount >= 2);
        Assert.All(pages, p => Assert.StartsWith("PosBeschreibungBetrag", p.Text));
        var rows = Regex.Matches(string.Concat(pages.Select(p => p.Text)), @"Zeile (\d{3})").Select(m => int.Parse(m.Groups[1].Value));
        Assert.Equal(Enumerable.Range(1, 120), rows);
    }

    [Fact]
    public void Table_ColumnWidths()
    {
        var widths = TableColumn.ComputeWidths([TableColumn.Fixed(40), TableColumn.Relative(3), TableColumn.Relative(1)], 440);
        Assert.Equal([40, 300, 100], widths);
    }

    [Fact]
    public void Table_CellsOfARowShareTheHeight()
    {
        var table = new Table().AddColumn(TableColumn.Relative()).AddColumn(TableColumn.Relative());
        var yellow = PdfColor.Rgb(1, 1, 0);
        var shortCell = new Frame { Background = yellow }.Add(new Paragraph(Style).Add("kurz"));
        var longCell = new Frame().Add(new Paragraph(Style).Add("lang\nlang\nlang"));
        table.AddRow(new TableRow().Add(shortCell).Add(longCell));

        var surface = DrawAll(new LayoutDocument(A4()).Add(table).Layout(Fonts))[0];
        var background = surface.Fills.Single(f => f.Color == yellow);
        double lineHeight = new Paragraph(Style).Add("x").Format(Fonts, 100, FieldValues.Estimate).Height;

        Assert.Equal(3 * lineHeight, background.Box.Height, 3);
    }

    [Fact]
    public void Frame_AppliesPaddingMinHeightAndBackground()
    {
        var blue = PdfColor.Rgb(0, 0, 1);
        var frame = new Frame { Padding = new Thickness(10, 5, 10, 5), MinHeight = 80, Background = blue }
            .Add(new Paragraph(Style).Add("Inhalt"));
        var surface = DrawAll(new LayoutDocument(A4()).Add(frame).Layout(Fonts))[0];

        var background = surface.Fills.Single(f => f.Color == blue).Box;
        Assert.Equal(new Box(50, 50, PageSize.A4.Width - 100, 80), background);
        Assert.Equal(60, surface.Runs[0].X, 6);
    }

    [Fact]
    public void ImageBlock_KeepsAspectRatio()
    {
        var image = ImageSource.FromFile(TestAssets.Image("sample-rgb.jpg")); // 240 × 160 px
        Assert.Equal((300, 200), new ImageBlock(image) { Width = 300 }.ComputeSize(500));
        Assert.Equal((150, 100), new ImageBlock(image) { Width = 300, Height = 100 }.ComputeSize(500));
        Assert.Equal((180, 120), new ImageBlock(image).ComputeSize(500)); // 96 dpi
        Assert.Equal((90, 60), new ImageBlock(image).ComputeSize(90));
    }

    [Fact]
    public void ListMarker_IsDrawnLeftOfTheText()
    {
        var item = new Paragraph(Style) { Marker = "•", LeftIndent = 22, MarkerWidth = 16 }.Add("Punkt");
        var surface = DrawAll(new LayoutDocument(A4()).Add(item).Layout(Fonts))[0];

        Assert.Equal("•Punkt", surface.Text);
        Assert.Equal(50 + 6, surface.Runs[0].X, 6);
        Assert.Equal(50 + 22, surface.Runs[1].X, 6);
        Assert.Equal(surface.Runs[0].Baseline, surface.Runs[1].Baseline);
    }

    [Fact]
    public void FixedBlock_CanOverflowOrBeClipped()
    {
        var text = new Paragraph(Style).Add(string.Join(' ', Enumerable.Repeat("Überlauf", 50)));
        var template = new PageTemplate(PageSize.A4, null).AddFixed(new FixedBlock(new Box(50, 50, 100, 20), text));
        var surface = DrawAll(new LayoutDocument(template).Layout(Fonts))[0];

        Assert.True(surface.Runs.Max(r => r.Baseline) > 70); // text runs beyond the field
    }

    /// <summary>Height of one line in the test style.</summary>
    static double LineHeight => new Paragraph(Style).Add("x").Format(Fonts, 100, FieldValues.Estimate).Height;

    /// <summary>
    /// A page whose area has room for <paramref name="freeLines"/> lines after a filler, followed
    /// by a paragraph of five forced lines. Returns the number of paragraph lines on page 1.
    /// </summary>
    static int LinesOnFirstPage(int freeLines, bool widowControl)
    {
        double h = LineHeight;
        var template = new PageTemplate(PageSize.A4, new Box(50, 50, 400, 10 * h + 0.01));
        var document = new LayoutDocument(template)
            .Add(new Frame { MinHeight = (10 - freeLines) * h })
            .Add(new Paragraph(Style) { WidowControl = widowControl }.Add("L1\nL2\nL3\nL4\nL5"));
        var pages = DrawAll(document.Layout(Fonts));
        return pages[0].Runs.Count;
    }

    [Theory]
    [InlineData(1, 0)] // a single first line would stand alone at the bottom → paragraph moves
    [InlineData(2, 2)] // two lines here, three on the next page
    [InlineData(4, 3)] // four would fit, but then one line stands alone at the top → three
    [InlineData(5, 5)] // everything fits
    public void WidowControl_KeepsTwoLinesTogether(int freeLines, int expected)
    {
        Assert.Equal(expected, LinesOnFirstPage(freeLines, widowControl: true));
    }

    [Fact]
    public void WidowControl_CanBeSwitchedOff()
    {
        Assert.Equal(1, LinesOnFirstPage(1, widowControl: false));
        Assert.Equal(4, LinesOnFirstPage(4, widowControl: false));
    }

    [Fact]
    public void Table_CellCanSpanColumns()
    {
        var green = PdfColor.Rgb(0, 1, 0);
        var table = new Table()
            .AddColumn(TableColumn.Fixed(100)).AddColumn(TableColumn.Fixed(50)).AddColumn(TableColumn.Fixed(70));
        table.AddRow(new TableRow()
            .Add(new Frame { Background = green }.Add(new Paragraph(Style).Add("Summe")), columnSpan: 2)
            .Add(new Frame().Add(new Paragraph(Style) { Alignment = TextAlignment.Right }.Add("99,00 €"))));

        var surface = DrawAll(new LayoutDocument(A4()).Add(table).Layout(Fonts))[0];
        var spanned = surface.Fills.Single(f => f.Color == green).Box;

        Assert.Equal(150, spanned.Width, 6);
        Assert.Equal(50 + 220, RecordingSurface.RightEdge(surface.Runs[1]), 3); // last column ends at 220
    }

    [Fact]
    public void TableCell_RejectsInvalidSpan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TableRow().Add(new Frame(), columnSpan: 0));
    }

    [Fact]
    public void Merge_KeepsPageNumbersPerDocument()
    {
        LaidOutDocument Letter(int paragraphs)
        {
            var footer = new Paragraph(Style).Add(TextSpan.PageNumber(Style)).Add("/").Add(TextSpan.TotalPages(Style));
            var document = new LayoutDocument(A4(bottom: 80).AddFixed(new FixedBlock(new Box(50, 800, 100, 20), footer)));
            for (int i = 1; i <= paragraphs; i++)
                document.Add(Numbered(i));
            return document.Layout(Fonts);
        }

        var first = Letter(150);
        var second = Letter(3);
        var pdf = new PdfDocument { Compress = false };
        first.AppendTo(pdf);
        second.AppendTo(pdf);

        Assert.Equal(first.PageCount + 1, pdf.Pages.Count);
        Assert.EndsWith("1/1", DrawAll(second)[0].Text);
        var text = Encoding.Latin1.GetString(pdf.ToArray());
        Assert.Equal(first.PageCount + 1, Regex.Matches(text, "/Type/Page/").Count);
    }
}
