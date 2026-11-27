// Prepares PNG images for PDF.
//
// PDF has no PNG filter, but it can decompress zlib data and undo the PNG row filters itself
// (FlateDecode with /Predictor 15). So in the common case the compressed data is passed through
// unchanged. This approach comes from FPDF; PDFsharp 6.2.4 instead decodes every image fully and
// compresses it again, which is slower and gives larger files.
//
// PDF does not know alpha channels inside the image data. Transparency is a separate grayscale
// image, the "soft mask" (/SMask). Three ways, from fastest to most general:
//
//   1. No alpha, not interlaced: pass the data through. A tRNS chunk becomes a soft mask.
//   2. Alpha channel, not interlaced: decompress, split every filtered row into color bytes and
//      alpha bytes without undoing the filters, compress both again (FPDF's trick). This is valid
//      because every PNG filter combines a byte only with the same byte of the neighboring pixel,
//      i.e. always within the same channel.
//   3. Interlaced: decode fully (PngDecoder), write 8-bit samples without filters.
//
// Beyond FPDF: 16-bit images, interlacing and tRNS transparency (also partially transparent
// palettes) are supported.

using IndyraPdf.Internal;

namespace IndyraPdf.Writer.Images;

/// <summary>Converts PNG files into <see cref="ImageData"/> for PDF image objects.</summary>
internal static class PngReader
{
    /// <summary>Alpha value of a fully opaque pixel in an 8-bit mask.</summary>
    const byte Opaque = 255;

    /// <summary>Alpha value of a fully transparent pixel.</summary>
    const byte Transparent = 0;

    /// <summary>Whether the data starts with the PNG signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) => PngFile.IsPng(data);

    /// <summary>Reads a PNG file.</summary>
    /// <exception cref="ImageFormatException">The data is no valid or supported PNG file.</exception>
    public static ImageData Read(byte[] data)
    {
        var png = PngFile.Parse(data);
        if (png.Interlaced)
            return ReadFullyDecoded(png);
        if (png.HasAlphaChannel)
            return ReadWithAlphaChannel(png);
        return ReadPassThrough(png);
    }

    /// <summary>Way 1: pass the compressed data through; tRNS transparency becomes a soft mask.</summary>
    static ImageData ReadPassThrough(PngFile png) => new()
    {
        Width = png.Width,
        Height = png.Height,
        ColorSpace = ColorSpaceOf(png),
        BitsPerComponent = png.BitDepth,
        Compression = ImageCompression.Flate,
        Data = png.ImageData,
        PngPredictor = true,
        Colors = png.ColorChannels,
        Palette = png.Palette,
        SoftMask = png.Transparency is null ? null : GrayMask(png, AlphaFromTransparency(png, PngDecoder.DecodeSamples(png))),
    };

    /// <summary>Way 2: split filtered rows into color and alpha without undoing the filters.</summary>
    static ImageData ReadWithAlphaChannel(PngFile png)
    {
        var raw = Zlib.Decompress(png.ImageData);
        int sampleBytes = png.BitDepth / 8;          // 1 or 2; alpha images have 8 or 16 bits
        int colorBytes = png.ColorChannels * sampleBytes;
        int rowLength = 1 + png.Width * (colorBytes + sampleBytes); // filter byte + pixels
        if (raw.Length < rowLength * png.Height)
            throw new ImageFormatException("The PNG image data is incomplete.");

        var color = new byte[png.Height * (1 + png.Width * colorBytes)];
        var alpha = new byte[png.Height * (1 + png.Width * sampleBytes)];
        int c = 0, a = 0;
        for (int y = 0; y < png.Height; y++)
        {
            int pos = y * rowLength;
            byte filter = raw[pos++];
            color[c++] = filter;   // both parts keep the filter of the original row
            alpha[a++] = filter;
            for (int x = 0; x < png.Width; x++)
            {
                for (int i = 0; i < colorBytes; i++)
                    color[c++] = raw[pos++];
                for (int i = 0; i < sampleBytes; i++)
                    alpha[a++] = raw[pos++];
            }
        }

        return new ImageData
        {
            Width = png.Width,
            Height = png.Height,
            ColorSpace = ColorSpaceOf(png),
            BitsPerComponent = png.BitDepth,
            Compression = ImageCompression.Flate,
            Data = Zlib.Compress(color),
            PngPredictor = true,
            Colors = png.ColorChannels,
            SoftMask = new ImageData
            {
                Width = png.Width,
                Height = png.Height,
                ColorSpace = ImageColorSpace.Gray,
                BitsPerComponent = png.BitDepth,
                Compression = ImageCompression.Flate,
                Data = Zlib.Compress(alpha),
                PngPredictor = true,
                Colors = 1,
            },
        };
    }

    /// <summary>Way 3: decode everything and write 8-bit samples without row filters.</summary>
    static ImageData ReadFullyDecoded(PngFile png)
    {
        var samples = PngDecoder.DecodeSamples(png);
        int channels = png.Channels;
        int colorChannels = png.ColorChannels;
        int pixels = png.Width * png.Height;
        bool indexed = png.ColorType == PngColorType.Indexed;

        // Palette indices stay unchanged (at most 255 with 8 bits); color values become 8 bit.
        var color = new byte[pixels * colorChannels];
        for (int p = 0, c = 0; p < pixels; p++)
        {
            for (int i = 0; i < colorChannels; i++)
            {
                ushort value = samples[p * channels + i];
                color[c++] = indexed ? (byte)value : To8Bit(value, png.BitDepth);
            }
        }

        byte[]? alpha = null;
        if (png.HasAlphaChannel)
        {
            alpha = new byte[pixels];
            for (int p = 0; p < pixels; p++)
                alpha[p] = To8Bit(samples[p * channels + channels - 1], png.BitDepth);
        }
        else if (png.Transparency is not null)
        {
            alpha = AlphaFromTransparency(png, samples);
        }

        return new ImageData
        {
            Width = png.Width,
            Height = png.Height,
            ColorSpace = ColorSpaceOf(png),
            BitsPerComponent = 8,
            Compression = ImageCompression.Flate,
            Data = Zlib.Compress(color),
            Colors = colorChannels,
            Palette = png.Palette,
            SoftMask = alpha is null ? null : GrayMask(png, alpha),
        };
    }

    /// <summary>PDF color space for the PNG color type.</summary>
    static ImageColorSpace ColorSpaceOf(PngFile png) => png.ColorType switch
    {
        PngColorType.Gray or PngColorType.GrayAlpha => ImageColorSpace.Gray,
        PngColorType.Rgb or PngColorType.RgbAlpha => ImageColorSpace.Rgb,
        _ => ImageColorSpace.Indexed,
    };

    /// <summary>Creates an 8-bit soft mask from one alpha value per pixel.</summary>
    static ImageData GrayMask(PngFile png, byte[] alpha) => new()
    {
        Width = png.Width,
        Height = png.Height,
        ColorSpace = ImageColorSpace.Gray,
        BitsPerComponent = 8,
        Compression = ImageCompression.Flate,
        Data = Zlib.Compress(alpha),
        Colors = 1,
    };

    /// <summary>
    /// Computes one alpha value per pixel from the tRNS chunk:
    /// indexed images have one alpha byte per palette entry (missing entries are opaque);
    /// gray and RGB images name one color (u16 per sample) that is fully transparent.
    /// </summary>
    static byte[] AlphaFromTransparency(PngFile png, ushort[] samples)
    {
        var trns = png.Transparency!;
        int pixels = png.Width * png.Height;
        var alpha = new byte[pixels];

        switch (png.ColorType)
        {
            case PngColorType.Indexed:
                for (int p = 0; p < pixels; p++)
                    alpha[p] = samples[p] < trns.Length ? trns[samples[p]] : Opaque;
                break;

            case PngColorType.Gray when trns.Length >= 2:
                int key = BigEndian.U16(trns, 0);
                for (int p = 0; p < pixels; p++)
                    alpha[p] = samples[p] == key ? Transparent : Opaque;
                break;

            case PngColorType.Rgb when trns.Length >= 6:
                int r = BigEndian.U16(trns, 0), g = BigEndian.U16(trns, 2), b = BigEndian.U16(trns, 4);
                for (int p = 0; p < pixels; p++)
                {
                    bool keyColor = samples[3 * p] == r && samples[3 * p + 1] == g && samples[3 * p + 2] == b;
                    alpha[p] = keyColor ? Transparent : Opaque;
                }
                break;

            default:
                Array.Fill(alpha, Opaque);
                break;
        }
        return alpha;
    }

    /// <summary>Scales a sample of the given bit depth to 0–255.</summary>
    static byte To8Bit(ushort value, int bitDepth) => bitDepth switch
    {
        16 => (byte)(value >> 8),
        8 => (byte)value,
        _ => (byte)(value * 255 / ((1 << bitDepth) - 1)),
    };
}
