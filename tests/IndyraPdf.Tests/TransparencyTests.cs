// Tests for colors with opacity: parsing, the PDF output (ExtGState with /ca and /CA), the
// preview (blending in Skia) and PDF/A conformance.

using System.Text;
using System.Text.RegularExpressions;
using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Skia;
using IndyraPdf.Writer;
using SkiaSharp;

namespace IndyraPdf.Tests;

public class TransparencyTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"));

    /// <summary>Yellow at 50 % opacity, as web-based editors deliver highlights.</summary>
    static readonly PdfColor HalfYellow = PdfColor.FromHex("#80FFFF00");

    static string Latin1(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    /// <summary>A page with a red area and a half transparent yellow area on top of it.</summary>
    static LaidOutDocument Overlay(PdfColor top)
    {
        var template = new PageTemplate(PageSize.A4, new Box(50, 300, 495, 400))
            .AddFixed(new FixedBlock(new Box(100, 100, 100, 50), new Frame { Background = PdfColor.Rgb(1, 0, 0), MinHeight = 50 }))
            .AddFixed(new FixedBlock(new Box(100, 100, 100, 50), new Frame { Background = top, MinHeight = 50 }));
        return new LayoutDocument(template)
            .Add(new Paragraph(TextStyle.Default with { Color = top }).Add("Markiert"))
            .Layout(Fonts);
    }

    [Fact]
    public void FromHex_ReadsOpacityFromTheFirstTwoDigits()
    {
        Assert.Equal(128 / 255.0, HalfYellow.Alpha, 6);
        Assert.Equal((1.0, 1.0, 0.0), (HalfYellow.C1, HalfYellow.C2, HalfYellow.C3));
        Assert.True(PdfColor.FromHex("#FFFF00").IsOpaque);
        Assert.Equal(PdfColor.FromHex("FFFF00"), PdfColor.FromHex("#FFFFFF00"));
        Assert.Throws<FormatException>(() => PdfColor.FromHex("#FFFF000"));
    }

    [Fact]
    public void WithAlpha_ReturnsANewColorAndClamps()
    {
        var red = PdfColor.Rgb(1, 0, 0);
        var faded = red.WithAlpha(0.25);
        Assert.Equal(1, red.Alpha);
        Assert.Equal(0.25, faded.Alpha);
        Assert.Equal(0, red.WithAlpha(-3).Alpha);
        Assert.NotEqual(red, faded);
    }

    [Fact]
    public void OpaqueColors_WriteNoGraphicsState()
    {
        var document = new PdfDocument { Compress = false };
        document.AddPage(PageSize.A4).Canvas.SetFillColor(PdfColor.Rgb(1, 0, 0)).DrawRectangle(10, 10, 20, 20, PdfPaint.Fill);
        var text = Latin1(document.ToArray());
        Assert.DoesNotContain(" gs\n", text);
        Assert.DoesNotContain("ExtGState", text);
    }

    [Fact]
    public void TransparentFill_WritesGraphicsStateOnlyWhenTheOpacityChanges()
    {
        var document = new PdfDocument { Compress = false };
        document.AddPage(PageSize.A4).Canvas
            .SetFillColor(HalfYellow).DrawRectangle(10, 10, 20, 20, PdfPaint.Fill)
            .SetFillColor(HalfYellow).DrawRectangle(40, 10, 20, 20, PdfPaint.Fill)  // same opacity: no new gs
            .SetFillColor(PdfColor.Black).DrawRectangle(70, 10, 20, 20, PdfPaint.Fill)
            .SetStrokeColor(PdfColor.Black.WithAlpha(0.5)).DrawLine(0, 0, 10, 10);
        var text = Latin1(document.ToArray());

        Assert.Equal(["/GS1", "/GS2", "/GS3"], Regex.Matches(text, @"(/GS\d+) gs\n").Select(m => m.Groups[1].Value));
        Assert.Contains("/ExtGState<</GS1<</Type/ExtGState/ca 0.502/CA 1>>/GS2<</Type/ExtGState/ca 1/CA 1>>/GS3<</Type/ExtGState/ca 1/CA 0.5>>>>", text);
    }

    [Fact]
    public void RestoreState_RestoresTheOpacity()
    {
        var document = new PdfDocument { Compress = false };
        document.AddPage(PageSize.A4).Canvas
            .SaveState().SetFillColor(HalfYellow).DrawRectangle(10, 10, 20, 20, PdfPaint.Fill).RestoreState()
            .SetFillColor(HalfYellow).DrawRectangle(40, 10, 20, 20, PdfPaint.Fill);
        // After "Q" the viewer is opaque again, so the second rectangle needs "gs" once more.
        Assert.Equal(2, Regex.Matches(Latin1(document.ToArray()), "/GS1 gs\n").Count);
    }

    [Fact]
    public void Images_StayOpaqueAfterATransparentFill()
    {
        var document = new PdfDocument { Compress = false };
        var image = document.AddImage(ImageSource.FromFile(TestAssets.Image("sample-rgb.jpg")));
        document.AddPage(PageSize.A4).Canvas
            .SetFillColor(HalfYellow).DrawRectangle(10, 10, 20, 20, PdfPaint.Fill)
            .DrawImage(image, 100, 100, 50, 50)
            .DrawRectangle(40, 10, 20, 20, PdfPaint.Fill);
        var text = Latin1(document.ToArray());

        Assert.Matches(@"q\n/GS2 gs\n[^\n]+ cm\n/Im1 Do\nQ\n", text);
        // After the image the transparent fill applies again without a new "gs".
        Assert.Single(Regex.Matches(text, "/GS1 gs\n"));
    }

    [Fact]
    public void Preview_BlendsWithTheBackground()
    {
        using var bitmap = SKBitmap.Decode(PngPreview.RenderPage(Overlay(HalfYellow), 0));
        var pixel = bitmap.GetPixel(200, 166); // inside both areas (150 pt, 125 pt at 96 dpi)
        Assert.Equal(255, pixel.Red);
        Assert.InRange(pixel.Green, 126, 130); // half yellow over red: orange
        Assert.Equal(0, pixel.Blue);
    }

    [VeraPdfFact]
    public void VeraPdf_TransparencyIsAllowedInPdfA3()
    {
        var document = new PdfDocument { Conformance = PdfConformance.PdfA3B };
        document.Info.Title = "Transparenz";
        Overlay(HalfYellow).AppendTo(document);
        var (passed, report) = VeraPdf.Validate(document.ToArray(), "3b");
        Assert.True(passed, report);
    }
}
