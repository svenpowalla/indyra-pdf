// Color spaces available for PdfColor.

namespace IndyraPdf.Writer;

/// <summary>Color space of a <see cref="PdfColor"/> (ISO 32000-2, 8.6.4: device color spaces).</summary>
public enum PdfColorSpace
{
    /// <summary>Gray levels (DeviceGray).</summary>
    Gray,

    /// <summary>Red, green, blue (DeviceRGB), for screens.</summary>
    Rgb,

    /// <summary>Cyan, magenta, yellow, black (DeviceCMYK), for printing.</summary>
    Cmyk,
}
