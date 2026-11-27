// Tests for the SkiaSharp preview: image size, colours, text and image positions.
// The PNGs are decoded again with SkiaSharp and checked pixel by pixel.

using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Skia;
using IndyraPdf.Writer;
using SkiaSharp;

namespace IndyraPdf.Tests;

public class PreviewTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"))
        .Add(TestAssets.Font("LiberationSans-Bold.ttf"));

    static readonly PdfColor Red = PdfColor.Rgb(1, 0, 0);

    /// <summary>A page with a red frame at (100, 100) pt, a text line and an image.</summary>
    static LaidOutDocument Sample()
    {
        var template = new PageTemplate(PageSize.A4, new Box(100, 300, 395, 400));
        template.AddFixed(new FixedBlock(new Box(100, 100, 100, 50), new Frame { Background = Red, MinHeight = 50 }));
        template.AddFixed(new FixedBlock(new Box(300, 100, 120, 80), new ImageBlock(ImageSource.FromFile(TestAssets.Image("sample-rgb.jpg"))) { Width = 120 }));
        return new LayoutDocument(template)
            .Add(new Paragraph(TextStyle.Default with { FontSize = 20 }).Add("HHHHHHHHHH"))
            .Layout(Fonts);
    }

    static SKBitmap Decode(byte[] png) => SKBitmap.Decode(png) ?? throw new InvalidOperationException("PNG could not be decoded.");

    /// <summary>Converts points into pixels at the given resolution.</summary>
    static int Px(double points, double dpi) => (int)Math.Round(points * dpi / 72);

    [Theory]
    [InlineData(96, 794, 1123)]
    [InlineData(200, 1654, 2339)]
    public void Page_HasA4SizeAtTheResolution(double dpi, int width, int height)
    {
        using var bitmap = Decode(PngPreview.RenderPage(Sample(), 0, dpi));
        Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
    }

    [Fact]
    public void Background_IsWhite_AndFrameIsRed()
    {
        using var bitmap = Decode(PngPreview.RenderPage(Sample(), 0));
        Assert.Equal(SKColors.White, bitmap.GetPixel(5, 5));
        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(Px(150, 96), Px(125, 96)));
        Assert.Equal(SKColors.White, bitmap.GetPixel(Px(210, 96), Px(125, 96))); // right of the frame
    }

    [Fact]
    public void Image_IsDrawnIntoItsBox()
    {
        using var bitmap = Decode(PngPreview.RenderPage(Sample(), 0));
        // The sample image has a red circle in the middle and coloured stripes at the bottom.
        var center = bitmap.GetPixel(Px(360, 96), Px(140, 96));
        Assert.NotEqual(SKColors.White, center);
        Assert.Equal(SKColors.White, bitmap.GetPixel(Px(430, 96), Px(140, 96))); // right of the image
    }

    [Fact]
    public void Text_IsDrawnWhereTheLayoutPutIt()
    {
        const double dpi = 144; // 2 px per point
        using var bitmap = Decode(PngPreview.RenderPage(Sample(), 0, dpi));
        var font = Fonts.Resolve(null, false, false);
        double width = new PdfDocument().AddFont(font).MeasureText("HHHHHHHHHH", 20);
        double lsb = font.GetLeftSideBearing(font.GetGlyphId('H')) * 20.0 / font.UnitsPerEm;

        // Find the dark pixels of the text line (content area starts at y = 300 pt).
        int top = Px(300, dpi), bottom = Px(330, dpi);
        int left = int.MaxValue, right = int.MinValue;
        for (int y = top; y < bottom; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }

        // Left edge of the first "H" and right edge of the last one, within one pixel.
        Assert.InRange(left, Px(100 + lsb, dpi) - 1, Px(100 + lsb, dpi) + 1);
        Assert.InRange(right, Px(100 + width - lsb, dpi) - 2, Px(100 + width - lsb, dpi) + 1);
    }

    [Fact]
    public void RenderPages_RendersAllOrALimitedNumber()
    {
        var document = new LayoutDocument(new PageTemplate(PageSize.A5, new Box(30, 30, 360, 520))) { MinimumPageCount = 3 }.Layout(Fonts);
        Assert.Equal(3, PngPreview.RenderPages(document).Count);
        Assert.Equal(2, PngPreview.RenderPages(document, maxPages: 2).Count);
    }

    [Fact]
    public void InvalidResolution_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PngPreview.RenderPage(Sample(), 0, 0));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 255, 255, 255)]
    [InlineData(0, 1, 1, 0, 255, 0, 0)]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    public void CmykColours_AreConvertedForThePreview(double c, double m, double y, double k, byte r, byte g, byte b)
    {
        Assert.Equal(new SKColor(r, g, b), SkiaColors.ToSkia(PdfColor.Cmyk(c, m, y, k)));
    }
}
