// Second sample: a multi-page letter built with the layout engine (stage 2).
// Shows page templates, fixed fields, flowing text, a list, a table with repeated header,
// an input line, a checkbox and page numbers.

using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Sample;

/// <summary>Builds the letter sample.</summary>
internal static class LetterSample
{
    /// <summary>Accent color for headings and the table header.</summary>
    static readonly PdfColor Accent = PdfColor.FromHex("#1F5FA8");

    /// <summary>Lays out the letter and returns it ready for drawing.</summary>
    public static LaidOutDocument Create(FontCollection fonts, ImageSource logo)
    {
        var body = TextStyle.Default with { FontSize = 10.5 };
        var small = body with { FontSize = 8, Color = PdfColor.Gray(0.35) };
        var page = PageSize.A4;
        double left = Mm(25), width = page.Width - Mm(45);

        // First page: logo, address field, content below the address.
        var first = new PageTemplate(page, new Box(left, Mm(100), width, page.Height - Mm(100) - Mm(25)));
        first.AddFixed(new FixedBlock(new Box(page.Width - Mm(60), Mm(15), Mm(40), Mm(20)), new ImageBlock(logo) { Height = Mm(20), Alignment = TextAlignment.Right }));
        first.AddFixed(new FixedBlock(new Box(left, Mm(45), Mm(85), Mm(40)), new Frame()
            .Add(new Paragraph(small).Add("Indyra Software · Musterweg 1 · 12345 Musterstadt"))
            .Add(new Paragraph(body) { SpaceBefore = 6 }.Add("Firma Beispiel GmbH\nFrau Erika Mustermann\nHauptstraße 5\n54321 Beispielstadt"))));

        // Following pages: content from the top.
        var following = new PageTemplate(page, new Box(left, Mm(20), width, page.Height - Mm(20) - Mm(25)));

        foreach (var template in new[] { first, following })
        {
            var footer = new Paragraph(small) { Alignment = TextAlignment.Right }
                .Add("Seite ").Add(TextSpan.PageNumber(small)).Add(" von ").Add(TextSpan.TotalPages(small));
            // Page numbers are repetitions: artifacts in tagged PDFs, skipped by screen readers.
            template.AddFixed(new FixedBlock(new Box(left, page.Height - Mm(15), width, Mm(6)), footer, IsArtifact: true));
        }

        var document = new LayoutDocument(first, following);
        document.Add(new Paragraph(body with { FontSize = 14, Bold = true, Color = Accent }) { Role = StructureRole.Heading1, SpaceAfter = 10 }.Add("Angebot Nr. 2026-042"));
        document.Add(new Paragraph(body) { SpaceAfter = 8 }.Add("Sehr geehrte Frau Mustermann,"));
        document.Add(new Paragraph(body) { Alignment = TextAlignment.Justify, SpaceAfter = 8 }.Add(
            "vielen Dank für Ihre Anfrage. Gerne bieten wir Ihnen die folgenden Leistungen an. Die Preise verstehen sich "
            + "zuzüglich der gesetzlichen Umsatzsteuer. Das Angebot ist vier Wochen gültig. Die Tabelle unten läuft über "
            + "mehrere Seiten; ihre Kopfzeile wiederholt sich auf jeder Seite."));

        var list = new ListBlock();
        foreach (var text in new[] { "Einrichtung und Schulung vor Ort", "Übernahme der bestehenden Daten", "Ein Jahr Wartung inklusive" })
            list.Add(new Paragraph(body) { Marker = "•", LeftIndent = 22, SpaceAfter = 2 }.Add(text));
        document.Add(list);

        document.Add(new Paragraph(body) { SpaceAfter = 10 });
        document.Add(OfferTable(body));

        document.Add(new Paragraph(body) { SpaceBefore = 14, SpaceAfter = 20 }.Add("Mit freundlichen Grüßen\n\nIndyra Software"));

        // Input line and checkbox, as typical for forms to be filled in by hand.
        document.Add(new Frame { Width = Mm(80), Borders = new BoxBorders { Bottom = LineStyle.Thin }, MinHeight = Mm(12) });
        document.Add(new Paragraph(small) { SpaceAfter = 10 }.Add("Ort, Datum, Unterschrift"));
        // A table cell always fills its column, so the 4 × 4 mm box is a frame inside the cell.
        var box = new Frame { Width = Mm(4), MinHeight = Mm(4), Borders = BoxBorders.All(LineStyle.Thin) };
        var checkbox = new Table().AddColumn(TableColumn.Fixed(Mm(6))).AddColumn(TableColumn.Relative());
        checkbox.AddRow(new TableRow()
            .Add(new Frame().Add(box))
            .Add(new Frame().Add(new Paragraph(body).Add("Ich nehme das Angebot an."))));
        document.Add(checkbox);

        return document.Layout(fonts);
    }

    /// <summary>A long table with a repeated header row.</summary>
    static Table OfferTable(TextStyle body)
    {
        var line = new LineStyle(0.5, PdfColor.Gray(0.6));
        Frame Cell(string text, TextStyle style, TextAlignment alignment = TextAlignment.Left, PdfColor? background = null)
            => new Frame { Padding = new Thickness(4, 3, 4, 3), Borders = BoxBorders.All(line), Background = background }
                .Add(new Paragraph(style) { Alignment = alignment }.Add(text));

        var header = body with { Bold = true, Color = PdfColor.White };
        var table = new Table { HeaderRowCount = 1 }
            .AddColumn(TableColumn.Fixed(Mm(12)))
            .AddColumn(TableColumn.Relative(4))
            .AddColumn(TableColumn.Relative(1))
            .AddColumn(TableColumn.Relative(1.5));
        table.AddRow(new TableRow()
            .Add(Cell("Pos.", header, background: Accent))
            .Add(Cell("Leistung", header, background: Accent))
            .Add(Cell("Menge", header, TextAlignment.Right, Accent))
            .Add(Cell("Betrag", header, TextAlignment.Right, Accent)));
        for (int i = 1; i <= 45; i++)
        {
            string description = i % 7 == 0
                ? $"Leistung {i} mit einer längeren Beschreibung, die in der Zelle umbricht und die Zeile höher macht"
                : $"Leistung {i}";
            table.AddRow(new TableRow()
                .Add(Cell(i.ToString(), body))
                .Add(Cell(description, body))
                .Add(Cell((i % 5 + 1).ToString(), body, TextAlignment.Right))
                .Add(Cell(Euro(i * 12.5), body, TextAlignment.Right)));
        }
        return table;
    }

    /// <summary>
    /// German currency format with decimal comma, e.g. "1.234,50 €". Formatted by hand because the
    /// project runs with invariant globalization (no culture data).
    /// </summary>
    static string Euro(double amount)
        => amount.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture)
                 .Replace(',', '\'').Replace('.', ',').Replace('\'', '.') + " €";

    /// <summary>Millimeters to points.</summary>
    static double Mm(double mm) => PdfUnits.FromMillimeters(mm);
}
