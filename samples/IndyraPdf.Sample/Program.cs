// Sample program for stage 1 (PDF writer).
// Creates out/sample.pdf with text, lines, shapes, images and a simple table.

using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Sample;

/// <summary>Entry point of the sample program.</summary>
public static class Program
{
    /// <summary>Accent color used for headings and lines.</summary>
    static readonly PdfColor Accent = PdfColor.FromHex("#1F5FA8");

    /// <summary>Creates the sample document and saves it.</summary>
    public static void Main()
    {
        var assets = new SampleAssets(FindRepositoryRoot());
        var document = new PdfDocument();
        document.Info.Title = "IndyraPdf – sample";
        document.Info.Author = "Indyra Software";
        document.Info.Creator = "IndyraPdf.Sample";

        // Every font file is loaded once. The writer pages (1, 2) and the layout engine (page 3)
        // use the same font objects, so each font is embedded only once.
        var collection = new FontCollection()
            .Add(assets.Font("LiberationSans-Regular.ttf"))
            .Add(assets.Font("LiberationSans-Bold.ttf"))
            .Add(assets.Font("LiberationSerif-Italic.ttf"));
        var fonts = new SampleFonts(
            document.AddFont(collection.Resolve("Liberation Sans", bold: false, italic: false)),
            document.AddFont(collection.Resolve("Liberation Sans", bold: true, italic: false)),
            document.AddFont(collection.Resolve("Liberation Serif", bold: false, italic: true)));

        DrawTextAndGraphicsPage(document, fonts, assets);
        DrawTablePage(document, fonts);
        DrawParagraphPage(document, collection);

        Directory.CreateDirectory(assets.OutputFolder);
        var output = Path.Combine(assets.OutputFolder, "sample.pdf");
        document.Save(output);
        Console.WriteLine($"Saved: {output} ({new FileInfo(output).Length / 1024.0:0.0} KB)");

        // Second file: a letter laid out by the layout engine.
        var letter = LetterSample.Create(collection, Drawing.ImageSource.FromFile(assets.Image("sample-rgba.png")));
        var letterOutput = Path.Combine(assets.OutputFolder, "letter.pdf");
        letter.ToPdf().Save(letterOutput);
        Console.WriteLine($"Saved: {letterOutput} ({letter.PageCount} pages, {new FileInfo(letterOutput).Length / 1024.0:0.0} KB)");

        // Third file: the same letter as ZUGFeRD / Factur-X e-invoice with embedded XML, tagged
        // for accessibility: PDF/A-3a and PDF/UA-1 at the same time.
        var invoice = new PdfDocument { Conformance = PdfConformance.PdfA3A, UniversalAccessibility = true, Language = "de-DE" };
        invoice.Info.Title = "Rechnung RE-2026-0042";
        invoice.Info.Author = "Indyra Software";
        invoice.Info.Creator = "IndyraPdf.Sample";
        letter.AppendTo(invoice);
        FacturX.FacturXInvoice.Attach(invoice, File.ReadAllBytes(assets.Invoice("factur-x-en16931.xml")), FacturX.FacturXProfile.En16931);
        var invoiceOutput = Path.Combine(assets.OutputFolder, "invoice-zugferd.pdf");
        invoice.Save(invoiceOutput);
        Console.WriteLine($"Saved: {invoiceOutput} ({invoice.Conformance}, {new FileInfo(invoiceOutput).Length / 1024.0:0.0} KB)");

        // Stage 4: PNG preview of the letter, drawn from the same layout as the PDF.
        var previews = Skia.PngPreview.RenderPages(letter);
        for (int i = 0; i < previews.Count; i++)
        {
            var previewOutput = Path.Combine(assets.OutputFolder, $"letter-page{i + 1}.png");
            File.WriteAllBytes(previewOutput, previews[i]);
            Console.WriteLine($"Saved: {previewOutput} ({previews[i].Length / 1024.0:0.0} KB)");
        }
    }

    /// <summary>Page 1 (A4): text in several scripts, alignment, line styles, shapes and images.</summary>
    static void DrawTextAndGraphicsPage(PdfDocument document, SampleFonts fonts, SampleAssets assets)
    {
        var page = document.AddPage(PageSize.A4);
        var canvas = page.Canvas;
        double left = Mm(20), right = page.Width - Mm(20);

        // Heading with rule
        canvas.SetFillColor(Accent)
              .DrawText(fonts.Bold, 20, left, Mm(30), "IndyraPdf – stage 1 sample")
              .SetStrokeColor(Accent).SetLineWidth(1.5)
              .DrawLine(left, Mm(34), right, Mm(34));

        // Text with special characters (German sample text on purpose)
        canvas.SetFillColor(PdfColor.Black);
        string[] lines =
        [
            "Umlaute und Sonderzeichen: ÄÖÜ äöü ß € „Anführungszeichen“ – Gedankenstrich …",
            "Greek: αβγδ ΩΣΠ · Cyrillic: Добрый день · Numbers: 1.234,56 €",
            "Kerning and ligatures follow in stage 2 (layout engine).",
        ];
        double y = Mm(46);
        foreach (var line in lines)
        {
            canvas.DrawText(fonts.Regular, 11, left, y, line);
            y += fonts.Regular.GetLineHeight(11);
        }

        // Alignment based on measured text width
        y += Mm(4);
        const string centered = "Centered";
        canvas.DrawText(fonts.SerifItalic, 14, (page.Width - fonts.SerifItalic.MeasureText(centered, 14)) / 2, y, centered);
        y += fonts.SerifItalic.GetLineHeight(14);
        const string rightAligned = "Right aligned: 99,90 €";
        canvas.DrawText(fonts.Regular, 11, right - fonts.Regular.MeasureText(rightAligned, 11), y, rightAligned);

        // Line styles: thin, dashed, thick with round caps
        y += Mm(10);
        canvas.SetStrokeColor(PdfColor.Gray(0.3)).SetLineWidth(0.5).DrawLine(left, y, right, y);
        canvas.SetDashPattern([4, 2]).SetLineWidth(1).DrawLine(left, y + Mm(4), right, y + Mm(4));
        canvas.SetSolidLine().SetLineWidth(4).SetLineCap(PdfLineCap.Round).SetStrokeColor(PdfColor.Rgb(0.8, 0.2, 0.1))
              .DrawLine(left, y + Mm(9), left + Mm(60), y + Mm(9));
        canvas.SetLineCap(PdfLineCap.Butt).SetLineWidth(1);

        // Shapes in RGB and CMYK
        y += Mm(16);
        canvas.SetFillColor(PdfColor.Rgb(0.9, 0.94, 1)).SetStrokeColor(Accent)
              .DrawRectangle(left, y, Mm(50), Mm(25), PdfPaint.FillAndStroke);
        canvas.SetFillColor(PdfColor.Cmyk(0, 0.6, 1, 0))
              .DrawRectangle(left + Mm(60), y, Mm(50), Mm(25), PdfPaint.Fill);
        canvas.SetStrokeColor(PdfColor.Black)
              .DrawEllipse(left + Mm(120), y, Mm(50), Mm(25), PdfPaint.Stroke);

        // Images
        y += Mm(35);
        canvas.SetFillColor(PdfColor.Black)
              .DrawText(fonts.Bold, 11, left, y, "Images: PNG with alpha, interlaced PNG, JPEG RGB, JPEG CMYK");
        y += Mm(4);
        DrawCheckerboard(canvas, left, y, Mm(80), Mm(55));
        DrawImageWithWidth(canvas, document.AddImage(assets.Image("sample-rgba.png")), left, y, Mm(40));
        DrawImageWithWidth(canvas, document.AddImage(assets.Image("sample-rgba-interlaced.png")), left + Mm(40), y, Mm(40));
        DrawImageWithWidth(canvas, document.AddImage(assets.Image("sample-rgb.jpg")), left + Mm(90), y, Mm(40));
        DrawImageWithWidth(canvas, document.AddImage(assets.Image("sample-cmyk.jpg")), left + Mm(132), y, Mm(38));

        DrawFooter(canvas, fonts, page, "Page 1 of 3");
    }

    /// <summary>Page 2 (A5 landscape): a simple table with right-aligned number columns.</summary>
    static void DrawTablePage(PdfDocument document, SampleFonts fonts)
    {
        var page = document.AddPage(PageSize.A5.Landscape);
        var canvas = page.Canvas;
        double x0 = Mm(15), top = Mm(25), rowHeight = Mm(8);
        double[] columns = [Mm(15), Mm(90), Mm(30), Mm(45)];
        string[] header = ["Pos.", "Description", "Quantity", "Amount"];
        string[][] rows =
        [
            ["1", "Consulting (hours)", "4", "480,00 €"],
            ["2", "IndyraPdf license – free", "1", "0,00 €"],
            ["3", "Training „PDF/A-3“", "2", "300,00 €"],
        ];
        double tableWidth = columns.Sum();

        canvas.SetFillColor(Accent).DrawText(fonts.Bold, 14, x0, Mm(15), "Table with measured column widths");

        // Header background
        canvas.SetFillColor(PdfColor.Rgb(0.9, 0.94, 1)).DrawRectangle(x0, top, tableWidth, rowHeight, PdfPaint.Fill);

        // Cell texts: columns 0–1 left aligned, 2–3 (numbers) right aligned
        canvas.SetFillColor(PdfColor.Black);
        for (int r = 0; r <= rows.Length; r++)
        {
            var cells = r == 0 ? header : rows[r - 1];
            var font = r == 0 ? fonts.Bold : fonts.Regular;
            double cellX = x0;
            for (int i = 0; i < columns.Length; i++)
            {
                double textX = i >= 2
                    ? cellX + columns[i] - Mm(2) - font.MeasureText(cells[i], 10)
                    : cellX + Mm(2);
                canvas.DrawText(font, 10, textX, top + r * rowHeight + Mm(5.5), cells[i]);
                cellX += columns[i];
            }
        }

        // Grid lines
        canvas.SetStrokeColor(PdfColor.Gray(0.5)).SetLineWidth(0.5);
        double tableHeight = (rows.Length + 1) * rowHeight;
        for (int r = 0; r <= rows.Length + 1; r++)
            canvas.DrawLine(x0, top + r * rowHeight, x0 + tableWidth, top + r * rowHeight);
        double lineX = x0;
        canvas.DrawLine(lineX, top, lineX, top + tableHeight);
        foreach (var width in columns)
        {
            lineX += width;
            canvas.DrawLine(lineX, top, lineX, top + tableHeight);
        }

        DrawFooter(canvas, fonts, page, "Page 2 of 3");
    }

    /// <summary>Page 3 (A4): paragraphs set by the layout engine (stage 2).</summary>
    static void DrawParagraphPage(PdfDocument document, FontCollection fonts)
    {
        var page = document.AddPage(PageSize.A4);
        var surface = new PdfDrawingSurface(document, page);
        var body = TextStyle.Default with { FontSize = 10.5 };
        var heading = body with { FontSize = 16, Bold = true, Color = Accent };
        const string text = "Die Satzlogik bricht Absätze in Zeilen um, wählt für jedes Zeichenformat die passende Schrift "
                          + "und richtet die Zeilen aus. Beim Blocksatz werden die Wortabstände so verbreitert, dass jede Zeile "
                          + "bis auf die letzte die volle Breite füllt. Lange Wörter wie Donaudampfschifffahrtsgesellschaft "
                          + "und Rechnungs-nummern werden sinnvoll getrennt.";

        double x = Mm(20), y = Mm(20), width = page.Width - Mm(40);
        void Place(Paragraph paragraph)
        {
            var formatted = paragraph.Format(fonts, width, new FieldValues(3, 3));
            formatted.Draw(surface, x, y);
            y += formatted.Height;
        }

        Place(new Paragraph(heading) { SpaceAfter = 8 }.Add("Stage 2: paragraphs"));
        foreach (var alignment in Enum.GetValues<TextAlignment>())
        {
            Place(new Paragraph(body with { Bold = true }) { SpaceAfter = 2 }.Add(alignment.ToString()));
            Place(new Paragraph(body) { Alignment = alignment, SpaceAfter = 10 }.Add(text));
        }

        Place(new Paragraph(body) { SpaceAfter = 10 }
            .Add("Zeichenformate: ")
            .Add("fett", body with { Bold = true })
            .Add(", ")
            .Add("kursiv in Serif", body with { FontFamily = "Liberation Serif", Italic = true })
            .Add(", ")
            .Add("unterstrichen", body with { Underline = true })
            .Add(", ")
            .Add("durchgestrichen", body with { Strikethrough = true })
            .Add(", ")
            .Add("farbig", body with { Color = PdfColor.Rgb(0.8, 0.1, 0.1) })
            .Add(", ")
            .Add("markiert", body with { Background = PdfColor.Rgb(1, 0.93, 0.4) })
            .Add(", ")
            .Add("groß", body with { FontSize = 16 })
            .Add(" und klein."));

        Place(new Paragraph(body) { LeftIndent = Mm(10), FirstLineIndent = -Mm(5), SpaceAfter = 10 }
            .Add("Hängender Einzug: Die erste Zeile beginnt 5 mm weiter links als die folgenden Zeilen, "
               + "so wie bei Aufzählungen. Der linke Einzug beträgt 10 mm."));

        Place(new Paragraph(body with { FontSize = 9, Color = PdfColor.Gray(0.4) }) { Alignment = TextAlignment.Right }
            .Add("Seite ").Add(TextSpan.PageNumber(body with { FontSize = 9, Color = PdfColor.Gray(0.4) }))
            .Add(" von ").Add(TextSpan.TotalPages(body with { FontSize = 9, Color = PdfColor.Gray(0.4) })));
    }

    /// <summary>Gray checkerboard as background, so that transparency becomes visible.</summary>
    static void DrawCheckerboard(PdfCanvas canvas, double x, double y, double width, double height)
    {
        double cell = Mm(5);
        canvas.SaveState().ClipRectangle(x, y, width, height).SetFillColor(PdfColor.Gray(0.85));
        for (int row = 0; row * cell < height; row++)
            for (int col = 0; col * cell < width; col++)
                if ((row + col) % 2 == 0)
                    canvas.DrawRectangle(x + col * cell, y + row * cell, cell, cell, PdfPaint.Fill);
        canvas.RestoreState();
    }

    /// <summary>Draws an image with the given width; the height follows from the aspect ratio.</summary>
    static void DrawImageWithWidth(PdfCanvas canvas, PdfImage image, double x, double y, double width)
        => canvas.DrawImage(image, x, y, width, width * image.PixelHeight / image.PixelWidth);

    /// <summary>Right-aligned page number at the bottom of the page.</summary>
    static void DrawFooter(PdfCanvas canvas, SampleFonts fonts, PdfPage page, string text)
    {
        canvas.SetFillColor(PdfColor.Gray(0.4))
              .DrawText(fonts.Regular, 9, page.Width - Mm(15) - fonts.Regular.MeasureText(text, 9), page.Height - Mm(10), text);
    }

    /// <summary>Shorthand for millimeters to points.</summary>
    static double Mm(double mm) => PdfUnits.FromMillimeters(mm);

    /// <summary>Finds the repository root (the folder containing IndyraPdf.slnx).</summary>
    static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IndyraPdf.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository folder containing IndyraPdf.slnx not found.");
    }
}
