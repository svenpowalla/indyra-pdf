# IndyraPdf

**PDF-Bibliothek für .NET mit eigener Satzlogik: von Absätzen und Tabellen bis zu
E-Rechnungen als PDF/A-3 (ZUGFeRD / Factur-X) und barrierefreien, getaggten PDFs.
MIT-Lizenz, keine Abhängigkeiten.**

[English version](README.md)

- **Satzlogik:** Absätze mit gemischten Zeichenformaten, Listen, Tabellen mit wiederholter
  Kopfzeile und verbundenen Zellen, Rahmen, Bilder, Seitenvorlagen mit festen Feldern,
  Seitenzahlen, Seitenumbruch mit Schutz vor einzelnen Zeilen.
- **Schriften:** TrueType mit vollem Unicode, Teilschriften (nur die benutzten Glyphen werden
  eingebettet), Kerning nach OpenType, farbige Emoji als Ersatzschrift. Text im PDF lässt sich
  kopieren und durchsuchen.
- **Normen:** PDF/A-3b, -3u und -3a, ZUGFeRD 2.x / Factur-X (alle Profile, XRechnung),
  PDF/UA-1. Die Tests prüfen die Ausgabe mit [veraPDF](https://verapdf.org) und dem
  ZUGFeRD-Prüfer [Mustang](https://www.mustangproject.org).
- **Vorschau:** Das Zusatzpaket `IndyraPdf.Skia` zeichnet dasselbe gesetzte Dokument als PNG,
  mit identischen Zeilen- und Seitenumbrüchen, z. B. für einen Dokument-Designer im Browser.
- **Reproduzierbar:** Gleiche Eingabe ergibt eine byte-gleiche Datei.

## Pakete

| Paket | Inhalt | Abhängigkeiten |
|---|---|---|
| `IndyraPdf` | PDF-Schreiber, Satzlogik, PDF/A, ZUGFeRD, Tagging | keine |
| `IndyraPdf.Skia` | PNG-Vorschau | [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) |

Benötigt .NET 10. Die Pakete sind noch nicht auf nuget.org; bis dahin die Projekte unter `src/`
direkt einbinden.

## Schnellstart

IndyraPdf nutzt keine Systemschriften. Die TrueType-Dateien gibt die Anwendung vor, z. B. die
[Liberation-Schriften](https://github.com/liberationfonts/liberation-fonts) (SIL OFL) aus
`assets/fonts`.

```csharp
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

var fonts = new FontCollection()
    .Add("LiberationSans-Regular.ttf")       // die erste Familie ist die Standardschrift
    .Add("LiberationSans-Bold.ttf");

var basis = TextStyle.Default with { FontSize = 10.5 };
double mm = PdfUnits.FromMillimeters(1);

// Seitengröße und Fließbereich (x, y, Breite, Höhe in Punkt, Ursprung oben links).
var seite = new PageTemplate(PageSize.A4, new Box(25 * mm, 20 * mm, 165 * mm, 250 * mm));
seite.AddFixed(new FixedBlock(
    new Box(25 * mm, 280 * mm, 165 * mm, 6 * mm),
    new Paragraph(basis) { Alignment = TextAlignment.Right }
        .Add("Seite ").Add(TextSpan.PageNumber(basis)).Add(" von ").Add(TextSpan.TotalPages(basis)),
    IsArtifact: true));                       // wiederholt sich, Vorleseprogramme überspringen es

var tabelle = new Table { HeaderRowCount = 1 }  // Kopfzeile wiederholt sich auf jeder Seite
    .AddColumn(TableColumn.Relative(3))
    .AddColumn(TableColumn.Relative(1));
tabelle.AddRow(new TableRow()
    .Add(new Frame().Add(new Paragraph(basis with { Bold = true }).Add("Leistung")))
    .Add(new Frame().Add(new Paragraph(basis with { Bold = true }).Add("Betrag"))));
tabelle.AddRow(new TableRow()
    .Add(new Frame().Add(new Paragraph(basis).Add("Beratung")))
    .Add(new Frame().Add(new Paragraph(basis).Add("1.200,00 €"))));

var dokument = new LayoutDocument(seite)
    .Add(new Paragraph(basis with { FontSize = 16, Bold = true }) { Role = StructureRole.Heading1 }.Add("Angebot"))
    .Add(new Paragraph(basis) { Alignment = TextAlignment.Justify, SpaceAfter = 8 }
        .Add("Vielen Dank für Ihre Anfrage. ")
        .Add(new TextSpan("Preise zuzüglich Umsatzsteuer.", basis with { Bold = true })))
    .Add(tabelle);

LaidOutDocument gesetzt = dokument.Layout(fonts);  // hier stehen Zeilen- und Seitenumbrüche fest
gesetzt.ToPdf().Save("angebot.pdf");
```

### E-Rechnung als PDF/A-3 (ZUGFeRD / Factur-X), getaggt und barrierefrei

```csharp
using IndyraPdf.FacturX;

var pdf = new PdfDocument
{
    Conformance = PdfConformance.PdfA3A,   // PDF/A-3a (getaggt) …
    UniversalAccessibility = true,         // … und zugleich PDF/UA-1
    Language = "de-DE",
};
pdf.Info.Title = "Rechnung 2026-0042";
gesetzt.AppendTo(pdf);
FacturXInvoice.Attach(pdf, File.ReadAllBytes("factur-x.xml"), FacturXProfile.En16931);
pdf.Save("rechnung.pdf");
```

Die Rechnungs-XML selbst (Inhalt und Prüfung nach EN 16931) erzeugt die Anwendung. IndyraPdf
bettet sie ein, schreibt die nötigen XMP-Metadaten und macht die Datei PDF/A-3-konform.

### PNG-Vorschau

```csharp
using IndyraPdf.Skia;

IReadOnlyList<byte[]> pngs = PngPreview.RenderPages(gesetzt, dpi: 96);
```

### Direkt zeichnen

Unter der Satzlogik liegt eine Zeichenfläche für genaue Positionierung:

```csharp
var pdf = new PdfDocument();
var schrift = pdf.AddFont("LiberationSans-Regular.ttf");
var canvas = pdf.AddPage(PageSize.A4).Canvas;
canvas.SetFillColor(PdfColor.FromHex("#1F5FA8"))
      .DrawText(schrift, 14, PdfUnits.FromMillimeters(20), PdfUnits.FromMillimeters(30), "Grüße aus IndyraPdf €")
      .DrawRectangle(50, 120, 200, 80, PdfPaint.Stroke);
pdf.Save("hallo.pdf");
```

Koordinaten in Punkt (1/72 Zoll), Ursprung oben links. Bei Text ist y die Grundlinie.

## Leistungsumfang

**PDF-Schreiber**
- Seiten in beliebiger Größe (A3, A4, A5, Letter, Legal, eigene Maße)
- Linien (Breite, Enden, Ecken, Strichelung), Rechtecke, Ellipsen, freie Pfade, Beschneidung
- Farben in Grau, RGB und CMYK, mit Deckkraft (`#AARRGGBB`)
- Bilder: PNG (alle Farbtypen, 1–16 Bit, Transparenz, Zeilensprungverfahren) und JPEG (Graustufen, RGB, CMYK)
- Flate-Kompression

**Satzlogik**
- Zeichenformate: Schriftfamilie, fett, kursiv, Größe, Farbe, Markierung, unter- und durchgestrichen
- Ausrichtung links, zentriert, rechts und Blocksatz; Einzüge, Abstände, Zeilenabstand, Listenzeichen
- Ersatzschriften je Zeichen, farbige Emoji (Hautfarben, Flaggen, ZWJ-Folgen) über Noto Color
  Emoji; eingebettet als Type3-Schrift, Emoji bleiben beim Kopieren Text
- Tabellen mit relativen und festen Spalten, wiederholten Kopfzeilen, Zellen über mehrere Spalten
- Rahmen mit Innenabstand, Rahmenlinien, Hintergrund, Mindesthöhe und fester Breite
- Seitenvorlagen für erste Seite und Folgeseiten, feste Blöcke vor oder hinter dem Text
- Höhe beliebiger Blöcke vorab messen (`Block.Measure`), Überlauf erkennen (`LaidOutDocument.Overflows`)
- Mehrere Dokumente zu einer PDF zusammenfügen, jedes mit eigener Seitenzählung

**PDF/A und E-Rechnung**
- PDF/A-3b, PDF/A-3u, PDF/A-3a mit sRGB-OutputIntent und XMP-Metadaten
- Eingebettete Dateien mit MIME-Typ, Änderungsdatum und `AFRelationship`
- Factur-X / ZUGFeRD-Profile MINIMUM bis EXTENDED und XRECHNUNG

**Barrierefreiheit (getaggtes PDF)**

Die Satzlogik erzeugt die logische Struktur selbst:

| Inhalt | Struktur |
|---|---|
| `Paragraph` | P, mit `Role` auch H1–H6 |
| `ListBlock` mit Absätzen mit `Marker` | L → LI → Lbl + LBody |
| `Table` | Table → TR → TH/TD (mit Scope und Spaltenzahl); wiederholte Kopfzeilen sind Artefakte |
| `ImageBlock` mit `AlternativeText` | Figure mit /Alt (ohne Text: Artefakt) |
| Linien, Flächen, `FixedBlock` mit `IsArtifact` | Artefakt |

Absätze und Tabellen, die über Seiten laufen, bleiben ein Element. Die Lesereihenfolge folgt
Seite und Position. Direkt auf die Zeichenfläche Gezeichnetes wird nicht getaggt.

## Grenzen

- Keine Ligaturen und keine komplexen Schriftsysteme (Arabisch, indische Schriften), kein Text
  von rechts nach links.
- Ersatzschriften sind für Emoji gedacht; Chinesisch, Japanisch, Koreanisch sind nicht getestet.
- Die Fließbereiche von erster Seite und Folgeseiten müssen gleich breit sein.
- Keine Verschlüsselung, keine Formularfelder (AcroForm), kein Lesen oder Ändern vorhandener PDFs.

## Bauen und testen

```
dotnet build
dotnet test
dotnet run --project samples/IndyraPdf.Sample   # erzeugt out/sample.pdf, letter.pdf, invoice-zugferd.pdf
```

Einige Tests prüfen die Ausgabe mit veraPDF und Mustang. Sie laufen, wenn die Werkzeuge
installiert sind, sonst werden sie übersprungen. Die Pfade kommen aus den Umgebungsvariablen
`INDYRAPDF_VERAPDF` (Startskript von veraPDF) und `INDYRAPDF_MUSTANG` (`Mustang-CLI.jar`), Java
aus `JAVA_HOME`.

## Aufbau

Der Quelltext ist englisch und ausführlich kommentiert, sodass man ihn ohne Vorwissen über PDF,
TrueType oder PNG lesen kann. Jeder Typ steht in einer eigenen Datei.

| Ordner | Inhalt |
|---|---|
| `src/IndyraPdf/Fonts` | TrueType/OpenType lesen: Maße, Teilschriften, Kerning, Ligaturen, Farbbilder |
| `src/IndyraPdf/Writer` | PDF-Dokument, Seiten, Zeichenfläche, Schriften, Bilder, Farben |
| `src/IndyraPdf/Writer/Objects` | PDF-Objektmodell und Dateiausgabe |
| `src/IndyraPdf/Writer/Metadata` | XMP, OutputIntent, eingebettete Dateien (PDF/A-3) |
| `src/IndyraPdf/Writer/Tagging` | Strukturbaum für getaggte PDFs |
| `src/IndyraPdf/FacturX` | ZUGFeRD / Factur-X |
| `src/IndyraPdf/Drawing` | Abstrakte Zeichenfläche zwischen Satzlogik und Ausgabe |
| `src/IndyraPdf/Layout` | Satzlogik: Absätze, Tabellen, Rahmen, Bilder, Seitenvorlagen |
| `src/IndyraPdf/Internal` | Gemeinsame Hilfen |
| `src/IndyraPdf.Skia` | PNG-Vorschau mit SkiaSharp |
| `tests/IndyraPdf.Tests` | Tests (xUnit) |
| `samples/IndyraPdf.Sample` | Beispielprogramm |

## Herkunft und Lizenz

IndyraPdf ist eigenständig in C# geschrieben. Für einzelne Bausteine wurden die Lösungen von
[FPDF](http://www.fpdf.org) und [PDFsharp](https://github.com/empira/PDFsharp) 6.2.4 (letzte
Version unter MIT-Lizenz) verglichen und der jeweils bessere Ansatz übernommen; die Stellen sind
im Quelltext vermerkt. Einzelheiten und Lizenztexte: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Lizenz: [MIT](LICENSE). Die Schriften in `assets/fonts` dienen nur Tests und Beispiel und stehen
unter der SIL Open Font License.
