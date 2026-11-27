// Color types of PNG images. The numeric values are the ones stored in the PNG header.

namespace IndyraPdf.Writer.Images;

/// <summary>Color type of a PNG image (PNG specification, 11.2.2, IHDR).</summary>
internal enum PngColorType
{
    /// <summary>Gray value per pixel.</summary>
    Gray = 0,

    /// <summary>Red, green, blue per pixel.</summary>
    Rgb = 2,

    /// <summary>Palette index per pixel.</summary>
    Indexed = 3,

    /// <summary>Gray value and alpha (opacity) per pixel.</summary>
    GrayAlpha = 4,

    /// <summary>Red, green, blue and alpha per pixel.</summary>
    RgbAlpha = 6,
}
