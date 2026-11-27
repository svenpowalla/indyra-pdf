// Image data prepared for a PDF image object. Produced by PngReader and JpegReader,
// consumed by ImageXObjectWriter.

namespace IndyraPdf.Writer.Images;

/// <summary>Prepared image data, ready to be written as a PDF image object.</summary>
internal sealed class ImageData
{
    /// <summary>Width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Color space of the pixel data.</summary>
    public required ImageColorSpace ColorSpace { get; init; }

    /// <summary>Bits per color component (1, 2, 4, 8 or 16).</summary>
    public required int BitsPerComponent { get; init; }

    /// <summary>How <see cref="Data"/> is compressed.</summary>
    public required ImageCompression Compression { get; init; }

    /// <summary>The compressed image data.</summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Whether every row starts with a PNG filter byte. The PDF reader then undoes the filters
    /// itself (DecodeParms /Predictor 15, ISO 32000-2, 7.4.4.4).
    /// </summary>
    public bool PngPredictor { get; init; }

    /// <summary>Number of color components per pixel in <see cref="Data"/> (needed for the predictor).</summary>
    public int Colors { get; init; } = 1;

    /// <summary>Color palette (RGB triples) for <see cref="ImageColorSpace.Indexed"/>.</summary>
    public byte[]? Palette { get; init; }

    /// <summary>
    /// Values for /Decode, which maps the stored values to colors, e.g. [1 0 …] to invert
    /// CMYK JPEGs written by Photoshop.
    /// </summary>
    public IReadOnlyList<double>? Decode { get; init; }

    /// <summary>Transparency mask (one gray value per pixel: 0 = transparent, max = opaque), or null.</summary>
    public ImageData? SoftMask { get; init; }
}
