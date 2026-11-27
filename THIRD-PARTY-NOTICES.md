# Herkunft und Lizenzen Dritter

IndyraPdf ist eigenständig in C# geschrieben. Für einzelne Bausteine wurden die Lösungen
zweier freier Bibliotheken verglichen und der jeweils bessere Ansatz übernommen. Die
betroffenen Stellen sind im Quelltext vermerkt.

## FPDF

- Quelle: https://www.fpdf.org/ (Version 1.9, PHP), Autor: Olivier Plathey
- Übernommene Ansätze: Subsetting von TrueType-Schriften (Neunummerierung der Glyphen,
  rekursive Auflösung zusammengesetzter Glyphen, Neuaufbau von hmtx/loca/maxp/hhea, Prüfsumme),
  Übernahme der PNG-Bilddaten ohne Neukomprimierung samt Aufteilen des Alphakanals,
  Umrechnung der Koordinaten je Zeichenbefehl.
- Lizenz:

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software to use, copy, modify, distribute, sublicense, and/or sell
copies of the software, and to permit persons to whom the software is furnished
to do so.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
```

## PDFsharp 6.2.4

- Quelle: https://github.com/empira/PDFsharp, Version 6.2.4 (letzte Version unter MIT-Lizenz;
  spätere Versionen stehen unter AGPL und werden nicht verwendet)
- Übernommene Ansätze: PDF-Objektmodell mit automatischen Verweisen auf indirekte Objekte,
  Ausgabe von Schriften als CID-Schrift (Type0/Identity-H) mit ToUnicode-Tabelle,
  Behandlung invertierter CMYK-JPEG-Bilder mit Adobe-Kennung.
- Lizenz:

```
Copyright (c) 2001-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany

MIT License

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

Ebenfalls von PDFsharp übernommen: der Aufbau des PDF/A-OutputIntents mit dem sRGB-Profil des ICC.

## sRGB-Farbprofil des ICC

- Datei: `src/IndyraPdf/Resources/sRGB2014.icc`, unverändert eingebettet als PDF/A-OutputIntent
- Quelle: https://registry.color.org/rgb-registry/profiles/sRGB2014.icc
- Copyright International Color Consortium, 2015
- Lizenz: „This profile is made available by the International Color Consortium, and may be
  copied, distributed, embedded, made, used, and sold without restriction. Altered versions of
  this profile shall have the original identification and copyright information removed and
  shall not be misrepresented as the original profile." Siehe auch
  `src/IndyraPdf/Resources/sRGB2014.icc.LICENSE.txt`.

## SkiaSharp (Abhängigkeit von IndyraPdf.Skia)

- Pakete: `SkiaSharp` und `SkiaSharp.NativeAssets.Linux.NoDependencies`, Version 4.157.0
- Quelle: https://github.com/mono/SkiaSharp
- Lizenz: MIT, Copyright (c) 2015-2016 Xamarin, Inc., Copyright (c) 2017-2018 Microsoft Corporation.
  Die enthaltene Skia-Grafikbibliothek von Google steht unter der BSD-3-Clause-Lizenz.
- Nur das Paket `IndyraPdf.Skia` hängt davon ab, die Kernbibliothek nicht.

## Eigene Umsetzungen nach Norm (ohne Vorlage)

Weder FPDF noch PDFsharp 6.2.4 können Kerning, Ligaturen oder farbige Emoji. Diese Teile sind
direkt nach den Normen geschrieben: Kerning (GPOS/kern) und Ligaturen (GSUB, Lookup-Typ 4) nach
der OpenType-Spezifikation, Farbbilder (CBLC/CBDT) nach der OpenType-Spezifikation,
Emoji als Type3-Schrift nach ISO 32000-2, Abschnitt 9.6.4, Deckkraft über ExtGState nach
ISO 32000-2, Abschnitt 8.4.5.

## Liberation-Schriften (nur Beispiel und Tests)

Die Schriften in `assets/fonts` stehen unter der SIL Open Font License 1.1, siehe
`assets/fonts/LICENSE-Liberation.txt`. Sie sind nicht Teil der Bibliothek.

## Noto Color Emoji (nur Tests)

- Datei: `assets/fonts/NotoColorEmoji.ttf`, Version 2.051, unverändert
- Quelle: https://github.com/googlefonts/noto-emoji (Tag `v2.051`, Ordner `fonts`)
- Copyright 2022 Google Inc.
- Lizenz: SIL Open Font License 1.1, siehe `assets/fonts/LICENSE-NotoColorEmoji.txt`.
- Nicht Teil der Bibliothek. Anwendungen geben die Schrift selbst als Ersatzschrift an
  (`FontCollection.AddFallback`); in PDFs landen nur die verwendeten Emoji-Bilder.

## Eigene Testdaten (keine Dritten)

- `assets/images/*`: Testbilder (Kreis über Farbverlauf, 240 × 160 Pixel) in verschiedenen
  Formaten (PNG mit Alphakanal, PNG mit Zeilensprungverfahren, JPEG in RGB und CMYK). Für dieses
  Projekt per Programm erzeugt, ohne eingebettete Farbprofile, Lizenz wie das Projekt (MIT).
- `assets/invoices/factur-x-en16931.xml`: Beispielrechnung nach EN 16931 mit erfundenen Daten
  (Musteranschriften, Muster-USt-IdNr.), für dieses Projekt geschrieben, Lizenz wie das Projekt (MIT).
