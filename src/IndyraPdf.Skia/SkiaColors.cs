// Converts the colours of IndyraPdf into SkiaSharp colours.

using IndyraPdf.Writer;
using SkiaSharp;

namespace IndyraPdf.Skia;

/// <summary>Converts <see cref="PdfColor"/> into <see cref="SKColor"/>.</summary>
internal static class SkiaColors
{
    /// <summary>
    /// Converts a colour including its opacity (Skia blends it with what lies below, like a PDF
    /// viewer). CMYK is converted with the simple formula R = (1 − C)(1 − K) etc.; a preview
    /// without colour management is accurate enough for that.
    /// </summary>
    public static SKColor ToSkia(PdfColor color)
    {
        byte alpha = Byte(color.Alpha);
        return color.Space switch
        {
            PdfColorSpace.Gray => new SKColor(Byte(color.C1), Byte(color.C1), Byte(color.C1), alpha),
            PdfColorSpace.Rgb => new SKColor(Byte(color.C1), Byte(color.C2), Byte(color.C3), alpha),
            _ => new SKColor(
                Byte((1 - color.C1) * (1 - color.C4)),
                Byte((1 - color.C2) * (1 - color.C4)),
                Byte((1 - color.C3) * (1 - color.C4)),
                alpha),
        };
    }

    /// <summary>Converts a component of 0–1 into 0–255.</summary>
    static byte Byte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
