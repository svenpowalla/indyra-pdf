// Compression of image data as written to PDF.

namespace IndyraPdf.Writer.Images;

/// <summary>How image data is compressed, i.e. which PDF filter decodes it (ISO 32000-2, 7.4).</summary>
internal enum ImageCompression
{
    /// <summary>zlib/deflate (filter FlateDecode), used for PNG data.</summary>
    Flate,

    /// <summary>JPEG (filter DCTDecode); the JPEG file is embedded unchanged.</summary>
    Jpeg,
}
