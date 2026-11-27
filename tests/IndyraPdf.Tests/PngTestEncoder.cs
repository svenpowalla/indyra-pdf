// Test helper: creates PNG images of every color type, bit depth and interlace method.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace IndyraPdf.Tests;

/// <summary>
/// Creates PNG files for tests. The rows are encoded with all five PNG filters in turn, so that
/// splitting and decoding are tested with every filter.
/// </summary>
internal static class PngTestEncoder
{
    /// <summary>Encodes an image as PNG file.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="colorType">PNG color type (0, 2, 3, 4 or 6).</param>
    /// <param name="bitDepth">Bits per sample.</param>
    /// <param name="samples">Values per pixel and channel in full bit depth, row by row.</param>
    /// <param name="palette">PLTE content for indexed images.</param>
    /// <param name="transparency">tRNS content.</param>
    /// <param name="interlaced">Whether to store the image interlaced (Adam7).</param>
    public static byte[] Encode(int width, int height, int colorType, int bitDepth, ushort[] samples,
                                byte[]? palette = null, byte[]? transparency = null, bool interlaced = false)
    {
        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        var raw = new MemoryStream();
        int rowCounter = 0;

        void EncodePass(int x0, int y0, int dx, int dy)
        {
            int w = (width - x0 + dx - 1) / dx;
            int h = (height - y0 + dy - 1) / dy;
            if (w <= 0 || h <= 0)
                return;
            int rowBytes = (w * channels * bitDepth + 7) / 8;
            int bpp = Math.Max(1, channels * bitDepth / 8);
            var previous = new byte[rowBytes];
            for (int y = 0; y < h; y++)
            {
                var row = new byte[rowBytes];
                for (int x = 0; x < w; x++)
                {
                    int src = ((y0 + y * dy) * width + x0 + x * dx) * channels;
                    for (int ch = 0; ch < channels; ch++)
                        WriteSample(row, x * channels + ch, bitDepth, samples[src + ch]);
                }
                byte filter = (byte)(rowCounter++ % 5);
                raw.WriteByte(filter);
                raw.Write(Filter(filter, row, previous, bpp));
                previous = row;
            }
        }

        if (interlaced)
        {
            foreach (var (x0, y0, dx, dy) in new[] { (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2) })
                EncodePass(x0, y0, dx, dy);
        }
        else
        {
            EncodePass(0, 0, 1, 1);
        }

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw.ToArray());

        var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = (byte)bitDepth;
        ihdr[9] = (byte)colorType;
        ihdr[12] = interlaced ? (byte)1 : (byte)0;
        Chunk(png, "IHDR", ihdr);
        if (palette is not null)
            Chunk(png, "PLTE", palette);
        if (transparency is not null)
            Chunk(png, "tRNS", transparency);
        // Split the image data into two IDAT chunks to test that they are joined.
        var data = compressed.ToArray();
        int half = data.Length / 2;
        Chunk(png, "IDAT", data[..half]);
        Chunk(png, "IDAT", data[half..]);
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static void WriteSample(byte[] row, int index, int bitDepth, ushort value)
    {
        switch (bitDepth)
        {
            case 16:
                row[2 * index] = (byte)(value >> 8);
                row[2 * index + 1] = (byte)value;
                break;
            case 8:
                row[index] = (byte)value;
                break;
            default:
                int bit = index * bitDepth;
                int shift = 8 - bitDepth - bit % 8;
                row[bit / 8] |= (byte)((value & ((1 << bitDepth) - 1)) << shift);
                break;
        }
    }

    static byte[] Filter(byte filter, byte[] row, byte[] previous, int bpp)
    {
        var result = new byte[row.Length];
        for (int i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? row[i - bpp] : 0;
            int b = previous[i];
            int c = i >= bpp ? previous[i - bpp] : 0;
            int predictor = filter switch
            {
                1 => a,
                2 => b,
                3 => (a + b) / 2,
                4 => Paeth(a, b, c),
                _ => 0,
            };
            result[i] = (byte)(row[i] - predictor);
        }
        return result;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void Chunk(Stream png, string type, byte[] data)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        Encoding.ASCII.GetBytes(type, header.AsSpan(4));
        png.Write(header);
        png.Write(data);
        var crcInput = header[4..].Concat(data).ToArray();
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcInput));
        png.Write(crc);
    }

    static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
