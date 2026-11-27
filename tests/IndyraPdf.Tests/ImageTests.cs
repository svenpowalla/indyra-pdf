// Tests for the PNG and JPEG readers. Every PNG variant is created with PngTestEncoder and the
// result is decoded the way a PDF viewer would do it, then compared with the original pixels.

using IndyraPdf.Internal;
using IndyraPdf.Writer;
using IndyraPdf.Writer.Images;

namespace IndyraPdf.Tests;

public class ImageTests
{
    /// <summary>Odd image size, so that interlace passes and bit boundaries are uneven.</summary>
    const int W = 13, H = 7;

    /// <summary>Creates a deterministic sample pattern that covers the whole value range.</summary>
    static ushort[] Pattern(int channels, int bitDepth, int seed = 1)
    {
        var samples = new ushort[W * H * channels];
        int max = (1 << bitDepth) - 1;
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (ushort)((i * 37 + seed * 101 + i / 5 * 13) % (max + 1));
        return samples;
    }

    /// <summary>Decodes image data the way a PDF viewer does (FlateDecode, optional PNG predictor).</summary>
    static ushort[] DecodeLikePdfViewer(ImageData image)
    {
        byte[] raw = Zlib.Decompress(image.Data);
        int bpc = image.BitsPerComponent;
        int colors = image.Colors;
        int rowBytes = (image.Width * colors * bpc + 7) / 8;
        int bpp = Math.Max(1, colors * bpc / 8);
        var samples = new ushort[image.Width * image.Height * colors];
        var previous = new byte[rowBytes];
        int pos = 0;
        for (int y = 0; y < image.Height; y++)
        {
            var row = new byte[rowBytes];
            if (image.PngPredictor)
            {
                byte filter = raw[pos++];
                Array.Copy(raw, pos, row, 0, rowBytes);
                for (int i = 0; i < rowBytes; i++)
                {
                    int a = i >= bpp ? row[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
                    int p = a + b - c;
                    int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                    row[i] += (byte)(filter switch
                    {
                        1 => a,
                        2 => b,
                        3 => (a + b) / 2,
                        4 => pa <= pb && pa <= pc ? a : pb <= pc ? b : c,
                        _ => 0,
                    });
                }
            }
            else
            {
                Array.Copy(raw, pos, row, 0, rowBytes);
            }
            pos += rowBytes;
            for (int s = 0; s < image.Width * colors; s++)
            {
                samples[y * image.Width * colors + s] = bpc switch
                {
                    16 => (ushort)((row[2 * s] << 8) | row[2 * s + 1]),
                    8 => row[s],
                    _ => (ushort)((row[s * bpc / 8] >> (8 - bpc - s * bpc % 8)) & ((1 << bpc) - 1)),
                };
            }
            previous = row;
        }
        return samples;
    }

    /// <summary>Splits interleaved samples (color…, alpha) into color samples and alpha samples.</summary>
    static (ushort[] Color, ushort[] Alpha) Split(ushort[] samples, int colorChannels)
    {
        int channels = colorChannels + 1;
        var color = new List<ushort>();
        var alpha = new List<ushort>();
        for (int p = 0; p < samples.Length / channels; p++)
        {
            for (int c = 0; c < colorChannels; c++)
                color.Add(samples[p * channels + c]);
            alpha.Add(samples[p * channels + colorChannels]);
        }
        return (color.ToArray(), alpha.ToArray());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 4)]
    [InlineData(0, 8)]
    [InlineData(0, 16)]
    [InlineData(2, 8)]
    [InlineData(2, 16)]
    public void Png_WithoutAlpha_PassesDataThrough(int colorType, int bitDepth)
    {
        int channels = colorType == 2 ? 3 : 1;
        var samples = Pattern(channels, bitDepth);
        var png = PngTestEncoder.Encode(W, H, colorType, bitDepth, samples);

        var image = PngReader.Read(png);

        Assert.True(image.PngPredictor);
        Assert.Equal(bitDepth, image.BitsPerComponent);
        Assert.Equal(colorType == 2 ? ImageColorSpace.Rgb : ImageColorSpace.Gray, image.ColorSpace);
        Assert.Null(image.SoftMask);
        Assert.Equal(samples, DecodeLikePdfViewer(image));
    }

    [Theory]
    [InlineData(4, 8)]
    [InlineData(4, 16)]
    [InlineData(6, 8)]
    [InlineData(6, 16)]
    public void Png_WithAlphaChannel_SplitsColorAndMask(int colorType, int bitDepth)
    {
        int colorChannels = colorType == 6 ? 3 : 1;
        var samples = Pattern(colorChannels + 1, bitDepth);
        var png = PngTestEncoder.Encode(W, H, colorType, bitDepth, samples);

        var image = PngReader.Read(png);
        var (expectedColor, expectedAlpha) = Split(samples, colorChannels);

        Assert.Equal(expectedColor, DecodeLikePdfViewer(image));
        Assert.NotNull(image.SoftMask);
        Assert.Equal(ImageColorSpace.Gray, image.SoftMask!.ColorSpace);
        Assert.Equal(expectedAlpha, DecodeLikePdfViewer(image.SoftMask));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Png_Palette_WithPartialTransparency(int bitDepth)
    {
        int entries = 1 << bitDepth;
        var palette = Enumerable.Range(0, entries * 3).Select(i => (byte)(i * 7)).ToArray();
        var trns = Enumerable.Range(0, Math.Min(entries, 3)).Select(i => (byte)(i * 100)).ToArray(); // alpha 0, 100, 200
        var samples = Pattern(1, bitDepth);
        var png = PngTestEncoder.Encode(W, H, 3, bitDepth, samples, palette, trns);

        var image = PngReader.Read(png);

        Assert.Equal(ImageColorSpace.Indexed, image.ColorSpace);
        Assert.Equal(palette, image.Palette);
        Assert.Equal(samples, DecodeLikePdfViewer(image));
        var expectedAlpha = samples.Select(s => (ushort)(s < trns.Length ? trns[s] : 255)).ToArray();
        Assert.Equal(expectedAlpha, DecodeLikePdfViewer(image.SoftMask!));
    }

    [Fact]
    public void Png_RgbColorKey_BecomesMask()
    {
        var samples = Pattern(3, 8);
        samples[0] = 10; samples[1] = 20; samples[2] = 30;   // first pixel = key color
        var trns = new byte[] { 0, 10, 0, 20, 0, 30 };
        var png = PngTestEncoder.Encode(W, H, 2, 8, samples, transparency: trns);

        var alpha = DecodeLikePdfViewer(PngReader.Read(png).SoftMask!);

        Assert.Equal(0, alpha[0]);
        Assert.All(alpha.Skip(1), a => Assert.Equal(255, a));
    }

    [Theory]
    [InlineData(6, 8)]
    [InlineData(2, 16)]
    [InlineData(0, 4)]
    [InlineData(3, 4)]
    public void Png_Interlaced_IsDeinterlaced(int colorType, int bitDepth)
    {
        int channels = colorType switch { 6 => 4, 2 => 3, _ => 1 };
        var samples = Pattern(channels, bitDepth);
        byte[]? palette = colorType == 3 ? Enumerable.Range(0, 48).Select(i => (byte)i).ToArray() : null;
        var interlaced = PngReader.Read(PngTestEncoder.Encode(W, H, colorType, bitDepth, samples, palette, interlaced: true));

        // The result has 8 bits per component; compare with the same conversion.
        ushort To8(ushort v) => colorType == 3 ? v : bitDepth switch
        {
            16 => (ushort)(v >> 8),
            8 => v,
            _ => (ushort)(v * 255 / ((1 << bitDepth) - 1)),
        };
        int colorChannels = colorType == 6 ? 3 : channels;
        var (color, alpha) = colorType == 6 ? Split(samples, 3) : (samples, null);

        Assert.Equal(8, interlaced.BitsPerComponent);
        Assert.Equal(color.Select(To8), DecodeLikePdfViewer(interlaced));
        Assert.Equal(colorChannels, interlaced.Colors);
        if (alpha is not null)
            Assert.Equal(alpha.Select(To8), DecodeLikePdfViewer(interlaced.SoftMask!));
    }

    [Fact]
    public void Jpeg_Rgb()
    {
        var image = JpegReader.Read(File.ReadAllBytes(TestAssets.Image("sample-rgb.jpg")));
        Assert.Equal((240, 160), (image.Width, image.Height));
        Assert.Equal(ImageColorSpace.Rgb, image.ColorSpace);
        Assert.Equal(ImageCompression.Jpeg, image.Compression);
        Assert.Null(image.Decode);
    }

    [Fact]
    public void Jpeg_AdobeCmyk_IsInverted()
    {
        var image = JpegReader.Read(File.ReadAllBytes(TestAssets.Image("sample-cmyk.jpg")));
        Assert.Equal(ImageColorSpace.Cmyk, image.ColorSpace);
        Assert.Equal(new double[] { 1, 0, 1, 0, 1, 0, 1, 0 }, image.Decode!);
    }

    [Fact]
    public void UnknownFormat_Throws()
    {
        var document = new PdfDocument();
        Assert.Throws<ImageFormatException>(() => document.AddImage("GIF89a......"u8.ToArray()));
    }

    [Fact]
    public void RealPngFiles_AreRead()
    {
        var plain = PngReader.Read(File.ReadAllBytes(TestAssets.Image("sample-rgba.png")));
        var interlaced = PngReader.Read(File.ReadAllBytes(TestAssets.Image("sample-rgba-interlaced.png")));

        // Both files show the same picture; after decoding, the pixels must be equal.
        Assert.Equal(DecodeLikePdfViewer(plain), DecodeLikePdfViewer(interlaced));
        Assert.Equal(DecodeLikePdfViewer(plain.SoftMask!), DecodeLikePdfViewer(interlaced.SoftMask!));
    }
}
