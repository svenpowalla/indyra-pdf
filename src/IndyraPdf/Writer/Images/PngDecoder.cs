// Fully decodes the pixels of a PNG image.
//
// PNG image data is compressed in two steps: first every row is "filtered" (each byte is replaced
// by its difference to a neighboring byte, which compresses better), then everything is
// zlib-compressed. Decoding reverses both steps.
//
// The five filters (PNG specification, section 9). For each byte x:
//   a = byte one pixel to the left, b = byte above, c = byte above-left (0 outside the image)
//   0 None     x
//   1 Sub      x + a
//   2 Up       x + b
//   3 Average  x + floor((a + b) / 2)
//   4 Paeth    x + whichever of a, b, c is closest to a + b − c
// All sums are modulo 256.
//
// Interlaced images (Adam7) store seven partial images with different pixel grids; every
// partial image has its own rows and filters.
//
// This class is only used where the raw data cannot be passed to PDF directly (see PngReader).

using IndyraPdf.Internal;

namespace IndyraPdf.Writer.Images;

/// <summary>Decodes PNG image data into sample values.</summary>
internal static class PngDecoder
{
    /// <summary>The seven Adam7 passes: start column, start row, column step, row step.</summary>
    static readonly (int X0, int Y0, int Dx, int Dy)[] Adam7Passes =
        [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    /// <summary>
    /// Decodes all pixels. Returns the samples in full bit depth, row by row, channel by channel
    /// (e.g. R, G, B, A, R, G, B, A, …).
    /// </summary>
    /// <exception cref="ImageFormatException">The image data is incomplete or damaged.</exception>
    public static ushort[] DecodeSamples(PngFile png)
    {
        var raw = Zlib.Decompress(png.ImageData);
        int channels = png.Channels;
        var samples = new ushort[png.Width * png.Height * channels];
        int pos = 0;

        if (!png.Interlaced)
        {
            DecodePass(png, raw, ref pos, png.Width, png.Height,
                       (x, y) => (y * png.Width + x) * channels, samples);
            return samples;
        }

        foreach (var (x0, y0, dx, dy) in Adam7Passes)
        {
            int w = (png.Width - x0 + dx - 1) / dx;
            int h = (png.Height - y0 + dy - 1) / dy;
            if (w <= 0 || h <= 0)
                continue; // small images may have empty passes
            DecodePass(png, raw, ref pos, w, h,
                       (x, y) => ((y0 + y * dy) * png.Width + x0 + x * dx) * channels, samples);
        }
        return samples;
    }

    /// <summary>
    /// Decodes one (partial) image of <paramref name="width"/> × <paramref name="height"/> pixels.
    /// </summary>
    /// <param name="png">The PNG file (for bit depth and channels).</param>
    /// <param name="raw">Decompressed data of all passes.</param>
    /// <param name="pos">Read position in <paramref name="raw"/>; advanced past this pass.</param>
    /// <param name="width">Width of this pass.</param>
    /// <param name="height">Height of this pass.</param>
    /// <param name="target">Maps a pixel of this pass to its first sample index in the full image.</param>
    /// <param name="samples">Output array.</param>
    static void DecodePass(PngFile png, byte[] raw, ref int pos, int width, int height,
                           Func<int, int, int> target, ushort[] samples)
    {
        int channels = png.Channels;
        int bitDepth = png.BitDepth;
        int rowBytes = (width * channels * bitDepth + 7) / 8;
        // Filters refer to the byte one pixel to the left; for bit depths below 8 that is the previous byte.
        int bytesPerPixel = Math.Max(1, channels * bitDepth / 8);
        var previous = new byte[rowBytes];
        var current = new byte[rowBytes];

        for (int y = 0; y < height; y++)
        {
            if (pos + 1 + rowBytes > raw.Length)
                throw new ImageFormatException("The PNG image data is incomplete.");
            byte filter = raw[pos++];
            Array.Copy(raw, pos, current, 0, rowBytes);
            pos += rowBytes;
            Unfilter(filter, current, previous, bytesPerPixel);

            for (int x = 0; x < width; x++)
            {
                int index = target(x, y);
                for (int ch = 0; ch < channels; ch++)
                    samples[index + ch] = ReadSample(current, x * channels + ch, bitDepth);
            }
            (previous, current) = (current, previous);
        }
    }

    /// <summary>
    /// Reads one sample from a row. Samples of 1, 2 or 4 bits are packed into bytes, most
    /// significant bits first; 16-bit samples are big-endian.
    /// </summary>
    static ushort ReadSample(byte[] row, int sampleIndex, int bitDepth)
    {
        switch (bitDepth)
        {
            case 16:
                return BigEndian.U16(row, 2 * sampleIndex);
            case 8:
                return row[sampleIndex];
            default:
                int bit = sampleIndex * bitDepth;
                int shift = 8 - bitDepth - bit % 8;
                return (ushort)((row[bit / 8] >> shift) & ((1 << bitDepth) - 1));
        }
    }

    /// <summary>Reverses the PNG filter of one row in place (see file header).</summary>
    static void Unfilter(byte filter, byte[] row, byte[] previous, int bytesPerPixel)
    {
        switch (filter)
        {
            case 0: // None
                break;
            case 1: // Sub
                for (int i = bytesPerPixel; i < row.Length; i++)
                    row[i] += row[i - bytesPerPixel];
                break;
            case 2: // Up
                for (int i = 0; i < row.Length; i++)
                    row[i] += previous[i];
                break;
            case 3: // Average
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    row[i] += (byte)((left + previous[i]) / 2);
                }
                break;
            case 4: // Paeth
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                    int b = previous[i];
                    int c = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                    row[i] += (byte)PaethPredictor(a, b, c);
                }
                break;
            default:
                throw new ImageFormatException($"Unknown PNG filter type {filter}.");
        }
    }

    /// <summary>Paeth predictor: whichever of left, above, above-left is closest to left + above − above-left.</summary>
    static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
