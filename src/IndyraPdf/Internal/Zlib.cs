// Compression in the zlib format.
//
// PDF compresses streams with the "FlateDecode" filter, which expects data in the zlib format
// (RFC 1950: a 2-byte header, deflate-compressed data, an Adler-32 checksum). PNG stores its
// image data in exactly the same format. .NET ships an implementation (ZLibStream), so no
// external library is needed.

using System.IO.Compression;

namespace IndyraPdf.Internal;

/// <summary>Compresses and decompresses data in the zlib format (RFC 1950).</summary>
internal static class Zlib
{
    /// <summary>Compresses data with the highest compression level.</summary>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(data);
        return buffer.ToArray();
    }

    /// <summary>Decompresses zlib data.</summary>
    /// <exception cref="InvalidDataException">The data is not valid zlib data.</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> data)
    {
        using var input = new MemoryStream(data.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var result = new MemoryStream();
        zlib.CopyTo(result);
        return result.ToArray();
    }
}
