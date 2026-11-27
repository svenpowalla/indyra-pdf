// Parses the chunk structure of a PNG file.
//
// A PNG file (PNG specification, https://www.w3.org/TR/png/) starts with an 8-byte signature,
// followed by "chunks". Every chunk consists of: length (4 bytes), type (4 ASCII letters),
// data, CRC checksum (4 bytes). Relevant chunks:
//   IHDR  header: width, height, bit depth, color type, interlace method
//   PLTE  color palette (RGB triples) for indexed images
//   tRNS  simple transparency: alpha per palette entry, or one fully transparent color
//   IDAT  image data (zlib-compressed); may be split across several IDAT chunks
//   IEND  end of the file
// Other chunks (color profile, text, time) are skipped.

using IndyraPdf.Internal;

namespace IndyraPdf.Writer.Images;

/// <summary>The parsed content of a PNG file.</summary>
internal sealed class PngFile
{
    /// <summary>The 8-byte PNG signature: \x89 P N G \r \n \x1A \n.</summary>
    static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    PngFile() { }

    /// <summary>Width in pixels.</summary>
    public int Width { get; private init; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; private init; }

    /// <summary>Bits per sample (1, 2, 4, 8 or 16).</summary>
    public int BitDepth { get; private init; }

    /// <summary>Color type.</summary>
    public PngColorType ColorType { get; private init; }

    /// <summary>Whether the image is stored interlaced (Adam7).</summary>
    public bool Interlaced { get; private init; }

    /// <summary>Concatenated content of all IDAT chunks (zlib-compressed, filtered rows).</summary>
    public byte[] ImageData { get; private init; } = [];

    /// <summary>Palette (RGB triples), or null.</summary>
    public byte[]? Palette { get; private init; }

    /// <summary>Content of the tRNS chunk, or null.</summary>
    public byte[]? Transparency { get; private init; }

    /// <summary>Number of samples per pixel.</summary>
    public int Channels => ColorType switch
    {
        PngColorType.Rgb => 3,
        PngColorType.GrayAlpha => 2,
        PngColorType.RgbAlpha => 4,
        _ => 1,
    };

    /// <summary>Whether every pixel has its own alpha value.</summary>
    public bool HasAlphaChannel => ColorType is PngColorType.GrayAlpha or PngColorType.RgbAlpha;

    /// <summary>Number of color samples per pixel (without alpha).</summary>
    public int ColorChannels => ColorType is PngColorType.Rgb or PngColorType.RgbAlpha ? 3 : 1;

    /// <summary>Whether the data starts with the PNG signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) => data.Length >= 8 && data[..8].SequenceEqual(Signature);

    /// <summary>Parses a PNG file.</summary>
    /// <exception cref="ImageFormatException">The data is no valid PNG file.</exception>
    public static PngFile Parse(byte[] data)
    {
        if (!IsPng(data))
            throw new ImageFormatException("The data is not a PNG image.");

        int width = 0, height = 0, bitDepth = 0, colorType = -1;
        bool interlaced = false;
        byte[]? palette = null, transparency = null;
        var idat = new MemoryStream();

        int pos = Signature.Length;
        while (pos + 8 <= data.Length)
        {
            int length = checked((int)BigEndian.U32(data, pos));
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int chunk = pos + 8; // start of the chunk data
            if (chunk + length > data.Length)
                throw new ImageFormatException("The PNG image is incomplete.");

            switch (type)
            {
                case "IHDR":
                    // 0 width (4), 4 height (4), 8 bit depth, 9 color type,
                    // 10 compression method, 11 filter method, 12 interlace method (1 byte each)
                    width = checked((int)BigEndian.U32(data, chunk));
                    height = checked((int)BigEndian.U32(data, chunk + 4));
                    bitDepth = data[chunk + 8];
                    colorType = data[chunk + 9];
                    if (data[chunk + 10] != 0 || data[chunk + 11] != 0)
                        throw new ImageFormatException("Unknown compression or filter method in PNG image.");
                    interlaced = data[chunk + 12] == 1;
                    break;
                case "PLTE":
                    palette = data.AsSpan(chunk, length).ToArray();
                    break;
                case "tRNS":
                    transparency = data.AsSpan(chunk, length).ToArray();
                    break;
                case "IDAT":
                    idat.Write(data, chunk, length);
                    break;
            }
            if (type == "IEND")
                break;
            pos = chunk + length + 4; // skip the CRC
        }

        // Allowed combinations of color type and bit depth (PNG specification, table 11.1).
        bool valid = colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            2 or 4 or 6 => bitDepth is 8 or 16,
            _ => false,
        };
        if (!valid || width <= 0 || height <= 0)
            throw new ImageFormatException("The PNG header is missing or invalid.");
        if (colorType == (int)PngColorType.Indexed && (palette is null || palette.Length < 3))
            throw new ImageFormatException("The PNG image has no color palette.");

        return new PngFile
        {
            Width = width,
            Height = height,
            BitDepth = bitDepth,
            ColorType = (PngColorType)colorType,
            Interlaced = interlaced,
            ImageData = idat.ToArray(),
            Palette = palette,
            Transparency = transparency,
        };
    }
}
