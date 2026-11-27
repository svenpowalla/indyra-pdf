# IndyraPdf

**A PDF library for .NET with its own layout engine: from paragraphs and tables to PDF/A-3
e-invoices (ZUGFeRD / Factur-X) and accessible, tagged PDFs. MIT-licensed, no dependencies.**

[Deutsche Fassung](README.de.md)

- **Layout engine:** paragraphs with mixed formatting, lists, tables with repeated header rows and
  merged cells, frames, images, page templates with fixed fields, page numbers, page breaks with
  widow control.
- **Fonts:** TrueType with full Unicode, subsetting (only the glyphs in use are embedded), OpenType
  kerning, colour emoji as fallback font. Text in the PDF can be copied and searched.
- **Standards:** PDF/A-3b, -3u and -3a, ZUGFeRD 2.x / Factur-X (all profiles, XRechnung),
  PDF/UA-1. The test suite checks the output with [veraPDF](https://verapdf.org) and the
  ZUGFeRD validator [Mustang](https://www.mustangproject.org).
- **Preview:** the optional package `IndyraPdf.Skia` renders the same laid-out document as PNG,
  with identical line and page breaks, e.g. for a document designer in a web application.
- **Reproducible:** the same input gives a byte-identical file.

## Packages

| Package | Contents | Dependencies |
|---|---|---|
| `IndyraPdf` | PDF writer, layout engine, PDF/A, ZUGFeRD, tagging | none |
| `IndyraPdf.Skia` | PNG preview | [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) |

Requires .NET 10. The packages are not on nuget.org yet; until then, reference the projects in
`src/` directly.

## Quick start

IndyraPdf does not use system fonts. You provide the TrueType files, for example the
[Liberation fonts](https://github.com/liberationfonts/liberation-fonts) (SIL OFL) found in
`assets/fonts`.

```csharp
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

var fonts = new FontCollection()
    .Add("LiberationSans-Regular.ttf")       // the first family is the default
    .Add("LiberationSans-Bold.ttf");

var body = TextStyle.Default with { FontSize = 10.5 };
double mm = PdfUnits.FromMillimeters(1);

// Page size and the area the content flows into (x, y, width, height in points, origin top left).
var page = new PageTemplate(PageSize.A4, new Box(25 * mm, 20 * mm, 165 * mm, 250 * mm));
page.AddFixed(new FixedBlock(
    new Box(25 * mm, 280 * mm, 165 * mm, 6 * mm),
    new Paragraph(body) { Alignment = TextAlignment.Right }
        .Add("Page ").Add(TextSpan.PageNumber(body)).Add(" of ").Add(TextSpan.TotalPages(body)),
    IsArtifact: true));                       // repeated on every page, skipped by screen readers

var table = new Table { HeaderRowCount = 1 }  // header row repeats on every page
    .AddColumn(TableColumn.Relative(3))
    .AddColumn(TableColumn.Relative(1));
table.AddRow(new TableRow()
    .Add(new Frame().Add(new Paragraph(body with { Bold = true }).Add("Item")))
    .Add(new Frame().Add(new Paragraph(body with { Bold = true }).Add("Amount"))));
table.AddRow(new TableRow()
    .Add(new Frame().Add(new Paragraph(body).Add("Consulting")))
    .Add(new Frame().Add(new Paragraph(body).Add("€ 1,200.00"))));

var document = new LayoutDocument(page)
    .Add(new Paragraph(body with { FontSize = 16, Bold = true }) { Role = StructureRole.Heading1 }.Add("Offer"))
    .Add(new Paragraph(body) { Alignment = TextAlignment.Justify, SpaceAfter = 8 }
        .Add("Thank you for your enquiry. ")
        .Add(new TextSpan("Prices exclude VAT.", body with { Bold = true })))
    .Add(table);

LaidOutDocument laidOut = document.Layout(fonts);   // line and page breaks are fixed here
laidOut.ToPdf().Save("offer.pdf");
```

### PDF/A-3 e-invoice (ZUGFeRD / Factur-X), tagged and accessible

```csharp
using IndyraPdf.FacturX;

var pdf = new PdfDocument
{
    Conformance = PdfConformance.PdfA3A,   // PDF/A-3a (tagged) …
    UniversalAccessibility = true,         // … and PDF/UA-1 at the same time
    Language = "en-GB",
};
pdf.Info.Title = "Invoice 2026-0042";
laidOut.AppendTo(pdf);
FacturXInvoice.Attach(pdf, File.ReadAllBytes("factur-x.xml"), FacturXProfile.En16931);
pdf.Save("invoice.pdf");
```

The invoice XML itself (content and validation against EN 16931) comes from your application.
IndyraPdf embeds it, writes the required XMP metadata and makes the file PDF/A-3 compliant.

### PNG preview

```csharp
using IndyraPdf.Skia;

IReadOnlyList<byte[]> pngs = PngPreview.RenderPages(laidOut, dpi: 96);
```

### Drawing directly

Below the layout engine there is a canvas for exact positioning:

```csharp
var pdf = new PdfDocument();
var font = pdf.AddFont("LiberationSans-Regular.ttf");
var canvas = pdf.AddPage(PageSize.A4).Canvas;
canvas.SetFillColor(PdfColor.FromHex("#1F5FA8"))
      .DrawText(font, 14, PdfUnits.FromMillimeters(20), PdfUnits.FromMillimeters(30), "Hello, world €")
      .DrawRectangle(50, 120, 200, 80, PdfPaint.Stroke);
pdf.Save("hello.pdf");
```

Coordinates are in points (1/72 inch) with the origin at the top left; for text, y is the baseline.

## Features in detail

**PDF writer**
- Any page size (A3, A4, A5, Letter, Legal, custom)
- Lines (width, caps, joins, dashes), rectangles, ellipses, paths, clipping
- Colours in grey, RGB and CMYK, with opacity (`#AARRGGBB`)
- Images: PNG (all colour types, 1–16 bit, transparency, interlacing) and JPEG (grey, RGB, CMYK)
- Flate compression

**Layout engine**
- Character formats: font family, bold, italic, size, colour, highlight, underline, strike-through
- Alignment left, centre, right and justified; indents, spacing, line height, list markers
- Fallback fonts per character, colour emoji (skin tones, flags, ZWJ sequences) via
  Noto Color Emoji; embedded as Type 3 font, so emoji stay text when copied
- Tables with relative and fixed columns, repeated header rows, cells spanning several columns
- Frames with padding, borders, background, minimum height and fixed width
- Page templates for the first and following pages, fixed blocks in front of or behind the text
- Measuring any block in advance (`Block.Measure`), detecting overflow (`LaidOutDocument.Overflows`)
- Merging several documents into one PDF, each with its own page numbering

**PDF/A and e-invoicing**
- PDF/A-3b, PDF/A-3u, PDF/A-3a with sRGB output intent and XMP metadata
- Embedded files with MIME type, modification date and `AFRelationship`
- Factur-X / ZUGFeRD profiles MINIMUM to EXTENDED and XRECHNUNG

**Accessibility (tagged PDF)**

The layout engine creates the logical structure itself:

| Content | Structure |
|---|---|
| `Paragraph` | P, with `Role` also H1–H6 |
| `ListBlock` with paragraphs with `Marker` | L → LI → Lbl + LBody |
| `Table` | Table → TR → TH/TD (with scope and column span); repeated header rows are artifacts |
| `ImageBlock` with `AlternativeText` | Figure with /Alt (without text: artifact) |
| Lines, fills, `FixedBlock` with `IsArtifact` | artifact |

Paragraphs and tables that continue on the next page stay one element. The reading order follows
page and position. Content drawn directly on the canvas is not tagged.

## Limitations

- No ligatures and no complex scripts (Arabic, Indic scripts); no right-to-left text.
- Fallback fonts are meant for emoji; CJK has not been tested.
- The content areas of the first and following pages must have the same width.
- No encryption, no forms (AcroForm), no reading or editing of existing PDFs.

## Building and testing

```
dotnet build
dotnet test
dotnet run --project samples/IndyraPdf.Sample   # writes out/sample.pdf, letter.pdf, invoice-zugferd.pdf
```

Some tests validate the output with veraPDF and Mustang. They run when the tools are installed
and are skipped otherwise. Paths are taken from the environment variables `INDYRAPDF_VERAPDF`
(veraPDF launcher) and `INDYRAPDF_MUSTANG` (`Mustang-CLI.jar`); Java from `JAVA_HOME`.

## Source layout

The source code is in English and commented in detail, so that it can be read without prior
knowledge of PDF, TrueType or PNG. Each type has its own file.

| Folder | Contents |
|---|---|
| `src/IndyraPdf/Fonts` | Reading TrueType/OpenType: metrics, subsetting, kerning, ligatures, colour bitmaps |
| `src/IndyraPdf/Writer` | PDF document, pages, canvas, fonts, images, colours |
| `src/IndyraPdf/Writer/Objects` | PDF object model and file output |
| `src/IndyraPdf/Writer/Metadata` | XMP, output intent, embedded files (PDF/A-3) |
| `src/IndyraPdf/Writer/Tagging` | Structure tree for tagged PDF |
| `src/IndyraPdf/FacturX` | ZUGFeRD / Factur-X |
| `src/IndyraPdf/Drawing` | Abstract drawing surface between layout engine and output |
| `src/IndyraPdf/Layout` | Layout engine: paragraphs, tables, frames, images, page templates |
| `src/IndyraPdf/Internal` | Shared helpers |
| `src/IndyraPdf.Skia` | PNG preview with SkiaSharp |
| `tests/IndyraPdf.Tests` | Tests (xUnit) |
| `samples/IndyraPdf.Sample` | Sample program |

## Origin and licence

IndyraPdf is written from scratch in C#. For some building blocks the approaches of
[FPDF](http://www.fpdf.org) and [PDFsharp](https://github.com/empira/PDFsharp) 6.2.4 (the last
MIT-licensed version) were compared and the better one adopted; the places are marked in the
source code. Details and licence texts: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Licensed under the [MIT licence](LICENSE). The fonts in `assets/fonts` are only used by the
tests and the sample and are under the SIL Open Font License.
