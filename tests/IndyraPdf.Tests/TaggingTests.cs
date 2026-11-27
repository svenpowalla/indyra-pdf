// Tests for tagged PDF (stage 5): structure tree, marked content, artifacts, reading order,
// PDF/UA-1 and PDF/A-3a (validated with veraPDF where installed).

using System.Text;
using System.Text.RegularExpressions;
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;
using IndyraPdf.Writer.Tagging;

namespace IndyraPdf.Tests;

public class TaggingTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"))
        .Add(TestAssets.Font("LiberationSans-Bold.ttf"));

    static readonly TextStyle Body = TextStyle.Default with { FontSize = 10 };

    /// <summary>
    /// A letter over several pages: address field (fixed, above the content), heading, list,
    /// long table with repeated header, figure, logo without alt text and footer as artifact.
    /// </summary>
    static LaidOutDocument Letter()
    {
        var first = new PageTemplate(PageSize.A4, new Box(70, 280, 455, 500));
        var following = new PageTemplate(PageSize.A4, new Box(70, 60, 455, 720));
        first.AddFixed(new FixedBlock(new Box(420, 40, 100, 60), new ImageBlock(ImageSource.FromFile(TestAssets.Image("sample-rgba.png"))) { Width = 100 }));
        first.AddFixed(new FixedBlock(new Box(70, 130, 240, 100), new Paragraph(Body).Add("Firma Beispiel GmbH\nHauptstraße 5")));
        foreach (var template in new[] { first, following })
        {
            var footer = new Paragraph(Body).Add("Seite ").Add(TextSpan.PageNumber(Body));
            template.AddFixed(new FixedBlock(new Box(70, 800, 455, 20), footer, IsArtifact: true));
        }

        var list = new ListBlock();
        foreach (var item in new[] { "Beratung", "Schulung" })
            list.Add(new Paragraph(Body) { Marker = "•", LeftIndent = 22 }.Add(item));

        var line = new LineStyle(0.5, PdfColor.Gray(0.5));
        Frame Cell(string text) => new Frame { Padding = Thickness.Uniform(3), Borders = BoxBorders.All(line) }.Add(new Paragraph(Body).Add(text));
        var table = new Table { HeaderRowCount = 1 }.AddColumn(TableColumn.Relative(3)).AddColumn(TableColumn.Relative(1));
        table.AddRow(new TableRow().Add(Cell("Leistung")).Add(Cell("Betrag")));
        for (int i = 1; i <= 60; i++)
            table.AddRow(new TableRow().Add(Cell($"Leistung {i}")).Add(Cell($"{i},00 €")));

        return new LayoutDocument(first, following)
            .Add(new Paragraph(Body with { FontSize = 16, Bold = true }) { Role = StructureRole.Heading1 }.Add("Rechnung"))
            .Add(list)
            .Add(table)
            .Add(new ImageBlock(ImageSource.FromFile(TestAssets.Image("sample-rgb.jpg"))) { Width = 150, AlternativeText = "Diagramm" })
            .Layout(Fonts);
    }

    static PdfDocument Tagged(PdfConformance conformance = PdfConformance.None, bool universalAccessibility = false)
    {
        var document = new PdfDocument { Conformance = conformance, UniversalAccessibility = universalAccessibility, Tagged = true, Language = "de-DE", Compress = false };
        document.Info.Title = "Rechnung";
        Letter().AppendTo(document);
        return document;
    }

    static string Latin1(PdfDocument document) => Encoding.Latin1.GetString(document.ToArray());

    /// <summary>All elements below an element, depth first.</summary>
    static IEnumerable<StructureElement> Descendants(StructureElement element)
        => element.Kids.OfType<StructureElement>().SelectMany(k => new[] { k }.Concat(Descendants(k)));

    [Fact]
    public void UntaggedDocument_HasNoStructure()
    {
        var document = new PdfDocument { Compress = false };
        Letter().AppendTo(document);
        var text = Latin1(document);

        Assert.Null(document.StructureTree);
        Assert.DoesNotContain("/StructTreeRoot", text);
        Assert.DoesNotContain("BDC", text);
    }

    [Fact]
    public void TaggedDocument_WritesCatalogAndPageEntries()
    {
        var text = Latin1(Tagged());

        Assert.Contains("/Lang(de-DE)", text);
        Assert.Contains("/MarkInfo<</Marked true>>", text);
        Assert.Matches(@"/StructTreeRoot \d+ 0 R", text);
        Assert.Matches(@"/StructParents 0/Tabs/S", text);
        Assert.Matches(@"/P <</MCID \d+>> BDC", text);
        Assert.Contains("/Artifact BMC", text);
    }

    [Fact]
    public void Structure_FollowsTheReadingOrder()
    {
        var document = Tagged();
        var tree = document.StructureTree!;
        var topLevel = tree.Root.Kids.OfType<StructureElement>().Select(e => e.Type).ToList();
        document.ToArray(); // writing sorts the top level by page and position

        // The address field is a fixed block above the content area, so it is already drawn
        // before the body text of page 1 …
        Assert.Equal("P", topLevel[0]);
        // … and sorted by position it is read first as well.
        Assert.Equal(["P", "H1", "L", "Table", "Figure"], tree.Root.Kids.OfType<StructureElement>().Select(e => e.Type));
    }

    [Fact]
    public void Structure_ListsTablesAndFigures()
    {
        var tree = Tagged().StructureTree!;
        var all = Descendants(tree.Root).ToList();

        var list = all.Single(e => e.Type == "L");
        Assert.All(list.Kids.OfType<StructureElement>(), li =>
            Assert.Equal(["Lbl", "LBody"], li.Kids.OfType<StructureElement>().Select(k => k.Type)));

        // One table element across pages; the header is tagged once (repetitions are artifacts).
        var table = all.Single(e => e.Type == "Table");
        Assert.Equal(61, table.Kids.OfType<StructureElement>().Count(e => e.Type == "TR"));
        Assert.Equal(2, all.Count(e => e.Type == "TH"));
        var cellPages = Descendants(table).SelectMany(e => e.Kids.OfType<MarkedContentReference>()).Select(r => r.PageIndex).Distinct();
        Assert.True(cellPages.Count() >= 2);

        Assert.Equal("Diagramm", all.Single(e => e.Type == "Figure").AlternativeText);
    }

    /// <summary>
    /// A table like a typical invoice: header row with four cells, a section heading across all
    /// columns and a discount row whose label covers three columns.
    /// </summary>
    static PdfDocument TaggedSpanningTable(PdfConformance conformance = PdfConformance.None)
    {
        Frame Cell(string text) => new Frame { Padding = Thickness.Uniform(3) }.Add(new Paragraph(Body).Add(text));
        var table = new Table { HeaderRowCount = 1 };
        for (int i = 0; i < 4; i++)
            table.AddColumn(TableColumn.Relative(1));
        table.AddRow(new TableRow().Add(Cell("Leistung")).Add(Cell("Menge")).Add(Cell("Preis")).Add(Cell("Betrag")));
        table.AddRow(new TableRow().Add(Cell("Lizenzen"), columnSpan: 4));
        table.AddRow(new TableRow().Add(Cell("Lizenz Basis")).Add(Cell("2")).Add(Cell("10,00 €")).Add(Cell("20,00 €")));
        table.AddRow(new TableRow().Add(Cell("Rabatt 10 %"), columnSpan: 3).Add(Cell("−2,00 €")));
        // A span beyond the last column is cut off and must not be written as 9.
        table.AddRow(new TableRow().Add(Cell("Summe")).Add(Cell("18,00 €"), columnSpan: 9));

        var document = new PdfDocument { Conformance = conformance, UniversalAccessibility = true, Language = "de-DE", Compress = false };
        document.Info.Title = "Rechnung";
        new LayoutDocument(new PageTemplate(PageSize.A4, new Box(70, 60, 455, 720))).Add(table).Layout(Fonts).AppendTo(document);
        return document;
    }

    [Fact]
    public void SpanningCells_WriteColSpan()
    {
        var document = TaggedSpanningTable();
        var cells = Descendants(document.StructureTree!.Root).Where(e => e.Type is "TD" or "TH").ToList();
        var text = Latin1(document);

        Assert.Equal([1, 1, 1, 1, 4, 1, 1, 1, 1, 3, 1, 1, 3], cells.Select(c => c.ColumnSpan));
        Assert.Contains("/A<</O/Table/ColSpan 4>>", text);
        Assert.Contains("/A<</O/Table/ColSpan 3>>", text);
        Assert.DoesNotContain("/ColSpan 9", text);
        Assert.DoesNotContain("/ColSpan 1>", text);
        Assert.Contains("/A<</O/Table/Scope/Column>>", text);
    }

    [Fact]
    public void StructureTag_RejectsColumnSpanBelowOne()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new StructureTag(StructureRole.TableDataCell, new object(), ColumnSpan: 0));

    [Fact]
    public void ArtifactsAndDecorations_AreNotInTheTree()
    {
        var tree = Tagged().StructureTree!;
        // Footer ("Seite n") and the logo without alt text are artifacts.
        var paragraphs = Descendants(tree.Root).Count(e => e.Type == "P");
        Assert.Equal(1 + 122, paragraphs); // address + table cells
        Assert.Single(Descendants(tree.Root), e => e.Type == "Figure");
    }

    [Fact]
    public void Requirements_AreChecked()
    {
        var noLanguage = new PdfDocument { Tagged = true };
        noLanguage.AddPage();
        Assert.Throws<InvalidOperationException>(() => noLanguage.ToArray());

        var noTitle = new PdfDocument { UniversalAccessibility = true, Language = "de-DE" };
        noTitle.AddPage();
        Assert.Throws<InvalidOperationException>(() => noTitle.ToArray());
    }

    [Fact]
    public void Metadata_IdentifiesPdfA3AAndPdfUA()
    {
        var text = Encoding.UTF8.GetString(Tagged(PdfConformance.PdfA3A, universalAccessibility: true).ToArray());

        Assert.Contains("<pdfaid:conformance>A</pdfaid:conformance>", text);
        Assert.Contains("<pdfuaid:part>1</pdfuaid:part>", text);
        Assert.Contains("<pdfaSchema:namespaceURI>http://www.aiim.org/pdfua/ns/id/</pdfaSchema:namespaceURI>", text);
        Assert.Contains("/ViewerPreferences<</DisplayDocTitle true>>", text);
    }

    [Fact]
    public void Conformance_PdfA3A_ImpliesTagging()
    {
        Assert.True(new PdfDocument { Conformance = PdfConformance.PdfA3A }.Tagged);
        Assert.True(new PdfDocument { UniversalAccessibility = true }.Tagged);
        Assert.False(new PdfDocument().Tagged);
    }

    // ----- Validation with veraPDF -----

    static void AssertValid(PdfDocument document, string flavour)
    {
        var (passed, report) = VeraPdf.Validate(document.ToArray(), flavour);
        Assert.True(passed, $"veraPDF ({flavour}) failed:\n{report}");
    }

    [VeraPdfFact]
    public void VeraPdf_PdfUA1() => AssertValid(Tagged(universalAccessibility: true), "ua1");

    [VeraPdfFact]
    public void VeraPdf_PdfA3A() => AssertValid(Tagged(PdfConformance.PdfA3A), "3a");

    [VeraPdfFact]
    public void VeraPdf_PdfA3AWithPdfUA1()
    {
        var document = Tagged(PdfConformance.PdfA3A, universalAccessibility: true);
        AssertValid(document, "3a");
        AssertValid(document, "ua1");
    }

    [VeraPdfFact]
    public void VeraPdf_SpanningCells_PassPdfUA1AndPdfA3A()
    {
        var document = TaggedSpanningTable(PdfConformance.PdfA3A);
        AssertValid(document, "ua1");
        AssertValid(document, "3a");
    }

    [VeraPdfFact]
    public void VeraPdf_UntaggedFailsPdfUA()
    {
        var document = new PdfDocument { Language = "de-DE" };
        document.Info.Title = "Ohne Tags";
        Letter().AppendTo(document);
        Assert.False(VeraPdf.Validate(document.ToArray(), "ua1").Passed);
    }
}
