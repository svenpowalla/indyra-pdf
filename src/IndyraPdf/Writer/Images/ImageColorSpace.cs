// Color spaces of image data as written to PDF.

namespace IndyraPdf.Writer.Images;

/// <summary>Color space of image data (ISO 32000-2, 8.6).</summary>
internal enum ImageColorSpace
{
    /// <summary>One gray value per pixel (DeviceGray).</summary>
    Gray,

    /// <summary>Red, green, blue per pixel (DeviceRGB).</summary>
    Rgb,

    /// <summary>Cyan, magenta, yellow, black per pixel (DeviceCMYK).</summary>
    Cmyk,

    /// <summary>One index into a color palette per pixel (Indexed over DeviceRGB).</summary>
    Indexed,
}
