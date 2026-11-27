// Tests for PDF/A-3, XMP metadata, embedded files and Factur-X / ZUGFeRD.
// The structural tests always run; the veraPDF tests run where veraPDF is installed.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IndyraPdf.Drawing;
using IndyraPdf.FacturX;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class PdfATests
{
    static readonly DateTimeOffset FixedDate = new(2026, 11, 22, 10, 30, 0, TimeSpan.FromHours(1));

    static readonly byte[] InvoiceXml = File.ReadAllBytes(TestAssets.Invoice("factur-x-en16931.xml"));

    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"))
        .Add(TestAssets.Font("LiberationSans-Bold.ttf"));

    /// <summary>A small document with text, a table, a transparent image and document information.</summary>
    static PdfDocument CreateDocument(PdfConformance conformance)
    {
        var style = TextStyle.Default with { FontSize = 10 };
        var table = new Table { HeaderRowCount = 1 }.AddColumn(TableColumn.Relative(3)).AddColumn(TableColumn.Relative(1));
        table.AddRow(new TableRow()
            .Add(new Frame { Background = PdfColor.Gray(0.9), Borders = BoxBorders.All(LineStyle.Thin) }.Add(new Paragraph(style with { Bold = true }).Add("Leistung")))
            .Add(new Frame { Background = PdfColor.Gray(0.9), Borders = BoxBorders.All(LineStyle.Thin) }.Add(new Paragraph(style with { Bold = true }).Add("Betrag"))));
        table.AddRow(new TableRow()
            .Add(new Frame { Borders = BoxBorders.All(LineStyle.Thin) }.Add(new Paragraph(style).Add("Beratung")))
            .Add(new Frame { Borders = BoxBorders.All(LineStyle.Thin) }.Add(new Paragraph(style) { Alignment = TextAlignment.Right }.Add("480,00 €"))));

        var layout = new LayoutDocument(new PageTemplate(PageSize.A4, new Box(50, 50, 495, 740)))
            .Add(new Paragraph(style with { FontSize = 16, Bold = true, Color = PdfColor.Rgb(0.12, 0.37, 0.66) }).Add("Rechnung RE-2026-0042"))
            .Add(new Paragraph(style) { SpaceBefore = 10, SpaceAfter = 10 }.Add("Grüße & „Anführungszeichen“ – mit Unicode."))
            .Add(table)
            .Add(new ImageBlock(ImageSource.FromFile(TestAssets.Image("sample-rgba.png"))) { Width = 120 });

        var document = new PdfDocument { Conformance = conformance };
        document.Info.Title = "Rechnung <RE-2026-0042> & „Test“";
        document.Info.Author = "Indyra Software";
        document.Info.Subject = "Testrechnung";
        document.Info.Keywords = "ZUGFeRD, Factur-X";
        document.Info.Creator = "IndyraPdf.Tests";
        document.Info.CreationDate = FixedDate;
        layout.Layout(Fonts).AppendTo(document);
        return document;
    }

    static string Latin1(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    /// <summary>Extracts the XMP packet from the (uncompressed) metadata stream.</summary>
    static XDocument Xmp(byte[] pdf)
    {
        var text = Encoding.UTF8.GetString(pdf);
        int start = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal);
        int end = text.IndexOf("</x:xmpmeta>", StringComparison.Ordinal) + "</x:xmpmeta>".Length;
        return XDocument.Parse(text[start..end]);
    }

    static string? XmpValue(XDocument xmp, string ns, string name)
        => xmp.Descendants(XName.Get(name, ns)).FirstOrDefault()?.Value;

    [Fact]
    public void Metadata_IsWrittenAndMatchesInfo()
    {
        var pdf = CreateDocument(PdfConformance.PdfA3B).ToArray();
        var xmp = Xmp(pdf);

        Assert.Equal("3", XmpValue(xmp, "http://www.aiim.org/pdfa/ns/id/", "part"));
        Assert.Equal("B", XmpValue(xmp, "http://www.aiim.org/pdfa/ns/id/", "conformance"));
        Assert.Equal("Rechnung <RE-2026-0042> & „Test“", XmpValue(xmp, "http://purl.org/dc/elements/1.1/", "title"));
        Assert.Equal("Indyra Software", XmpValue(xmp, "http://purl.org/dc/elements/1.1/", "creator"));
        Assert.Equal("2026-11-22T10:30:00+01:00", XmpValue(xmp, "http://ns.adobe.com/xap/1.0/", "CreateDate"));
        Assert.Equal("IndyraPdf", XmpValue(xmp, "http://ns.adobe.com/pdf/1.3/", "Producer"));
        Assert.Contains("/CreationDate(D:20261122103000+01'00')", Latin1(pdf));
        Assert.Matches(@"/Type/Metadata/Subtype/XML/Length \d+>>", Latin1(pdf)); // not compressed
    }

    [Fact]
    public void PdfA_AddsOutputIntentWithSrgbProfile()
    {
        var text = Latin1(CreateDocument(PdfConformance.PdfA3B).ToArray());
        Assert.Contains("/OutputIntents[", text);
        Assert.Contains("/S/GTS_PDFA1/OutputConditionIdentifier(sRGB IEC61966-2.1)", text);
        Assert.Matches(@"/N 3/Filter/FlateDecode/Length \d+", text);

        Assert.DoesNotContain("/OutputIntents", Latin1(CreateDocument(PdfConformance.None).ToArray()));
    }

    [Fact]
    public void PdfA_RejectsCmyk()
    {
        var colour = new PdfDocument { Conformance = PdfConformance.PdfA3B };
        colour.AddPage().Canvas.SetFillColor(PdfColor.Cmyk(0, 1, 1, 0)).DrawRectangle(0, 0, 10, 10, PdfPaint.Fill);
        Assert.Throws<InvalidOperationException>(() => colour.ToArray());

        var image = new PdfDocument { Conformance = PdfConformance.PdfA3B };
        image.AddPage().Canvas.DrawImage(image.AddImage(TestAssets.Image("sample-cmyk.jpg")), 0, 0, 10, 10);
        Assert.Throws<InvalidOperationException>(() => image.ToArray());
    }

    [Fact]
    public void Attachment_IsAssociatedAndListed()
    {
        var document = CreateDocument(PdfConformance.PdfA3B);
        document.Compress = false;
        document.Attach(new PdfAttachment("daten.xml", "<a/>"u8.ToArray(), "text/xml")
        {
            Relationship = AttachmentRelationship.Data,
            Description = "Daten",
            ModificationDate = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        });
        var text = Latin1(document.ToArray());

        Assert.Matches(@"/AF\[\d+ 0 R\]", text);
        Assert.Matches(@"/Names<</EmbeddedFiles<</Names\[\(daten\.xml\) \d+ 0 R\]>>>>", text);
        Assert.Contains("/Type/Filespec/F(daten.xml)/UF(daten.xml)/Desc(Daten)/AFRelationship/Data/EF<</F ", text);
        Assert.Contains("/Type/EmbeddedFile/Subtype/text#2Fxml/Params<</ModDate(D:20261101080000Z)/Size 4/CheckSum<", text);
        Assert.Contains("stream\n<a/>\nendstream", text);
    }

    [Fact]
    public void Attachment_NamesMustBeUnique()
    {
        var document = new PdfDocument();
        document.Attach(new PdfAttachment("a.xml", [], "text/xml"));
        Assert.Throws<ArgumentException>(() => document.Attach(new PdfAttachment("a.xml", [], "text/xml")));
    }

    [Theory]
    [InlineData(FacturXProfile.En16931, "factur-x.xml", "EN 16931", "Alternative")]
    [InlineData(FacturXProfile.Extended, "factur-x.xml", "EXTENDED", "Alternative")]
    [InlineData(FacturXProfile.BasicWl, "factur-x.xml", "BASIC WL", "Data")]
    [InlineData(FacturXProfile.Minimum, "factur-x.xml", "MINIMUM", "Data")]
    [InlineData(FacturXProfile.XRechnung, "xrechnung.xml", "XRECHNUNG", "Alternative")]
    public void FacturX_EmbedsXmlAndMetadata(FacturXProfile profile, string fileName, string level, string relationship)
    {
        var document = CreateDocument(PdfConformance.None);
        FacturXInvoice.Attach(document, InvoiceXml, profile);
        var pdf = document.ToArray();
        var text = Latin1(pdf);
        var xmp = Xmp(pdf);

        Assert.Equal(PdfConformance.PdfA3B, document.Conformance);
        Assert.Contains($"/F({fileName})", text);
        Assert.Contains($"/AFRelationship/{relationship}", text);
        Assert.Contains("/Subtype/text#2Fxml", text);
        Assert.Equal("INVOICE", XmpValue(xmp, FacturXInvoice.XmpNamespace, "DocumentType"));
        Assert.Equal(fileName, XmpValue(xmp, FacturXInvoice.XmpNamespace, "DocumentFileName"));
        Assert.Equal("1.0", XmpValue(xmp, FacturXInvoice.XmpNamespace, "Version"));
        Assert.Equal(level, XmpValue(xmp, FacturXInvoice.XmpNamespace, "ConformanceLevel"));

        // The extension schema is described for PDF/A, with all four properties.
        Assert.Equal(FacturXInvoice.XmpNamespace, XmpValue(xmp, "http://www.aiim.org/pdfa/ns/schema#", "namespaceURI"));
        Assert.Equal(["DocumentFileName", "DocumentType", "Version", "ConformanceLevel"],
            xmp.Descendants(XName.Get("name", "http://www.aiim.org/pdfa/ns/property#")).Select(e => e.Value));
    }

    [Fact]
    public void FacturX_RejectsInvalidXml()
    {
        var document = new PdfDocument();
        Assert.Throws<ArgumentException>(() => FacturXInvoice.Attach(document, "<Invoice/>"u8.ToArray(), FacturXProfile.En16931));
        Assert.Throws<ArgumentException>(() => FacturXInvoice.Attach(document, "<rsm:CrossIndustryInvoice"u8.ToArray(), FacturXProfile.En16931));
    }

    [Fact]
    public void FacturX_KeepsPdfA3U()
    {
        var document = new PdfDocument { Conformance = PdfConformance.PdfA3U };
        FacturXInvoice.Attach(document, InvoiceXml, FacturXProfile.En16931);
        Assert.Equal(PdfConformance.PdfA3U, document.Conformance);
    }

    // ----- Validation with veraPDF -----

    static void AssertValid(byte[] pdf, string flavour)
    {
        var (passed, report) = VeraPdf.Validate(pdf, flavour);
        Assert.True(passed, $"veraPDF ({flavour}) failed:\n{report}");
    }

    [VeraPdfFact]
    public void VeraPdf_PdfA3B() => AssertValid(CreateDocument(PdfConformance.PdfA3B).ToArray(), "3b");

    [VeraPdfFact]
    public void VeraPdf_PdfA3U() => AssertValid(CreateDocument(PdfConformance.PdfA3U).ToArray(), "3u");

    [VeraPdfFact]
    public void VeraPdf_FacturXInvoice()
    {
        var document = CreateDocument(PdfConformance.PdfA3B);
        FacturXInvoice.Attach(document, InvoiceXml, FacturXProfile.En16931);
        AssertValid(document.ToArray(), "3b");
    }

    [MustangFact]
    public void Mustang_FacturXInvoiceIsValid()
    {
        var document = CreateDocument(PdfConformance.PdfA3B);
        FacturXInvoice.Attach(document, InvoiceXml, FacturXProfile.En16931);
        var (valid, report) = Mustang.Validate(document.ToArray());
        Assert.True(valid, $"Mustang failed:\n{report}");
    }

    [MustangFact]
    public void Mustang_DetectsMissingInvoice()
    {
        // A PDF/A without embedded invoice is no ZUGFeRD document – proves that the check validates.
        var (valid, _) = Mustang.Validate(CreateDocument(PdfConformance.PdfA3B).ToArray());
        Assert.False(valid);
    }

    [VeraPdfFact]
    public void VeraPdf_DetectsViolations()
    {
        // Plain PDF without output intent must fail – proves that the check really validates.
        var (passed, _) = VeraPdf.Validate(CreateDocument(PdfConformance.None).ToArray(), "3b");
        Assert.False(passed);
    }
}
