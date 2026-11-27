// Colors for lines, fills and text.
//
// Besides the color components a color carries an opacity ("alpha"). PDF has no color operator
// with opacity; the PDF writer sets it separately through a graphics state parameter dictionary
// (ExtGState, ISO 32000-2, 8.4.5: /ca for fills and text, /CA for lines). PDF/A-2 and PDF/A-3
// allow this kind of transparency.

using System.Globalization;

namespace IndyraPdf.Writer;

/// <summary>
/// A color in one of the device color spaces. All components range from 0 to 1;
/// values outside are clamped. <see cref="Alpha"/> is the opacity (1 = opaque, the default).
/// </summary>
public readonly record struct PdfColor
{
    /// <summary>Creates an opaque color. Unused components stay 0.</summary>
    PdfColor(PdfColorSpace space, double c1, double c2 = 0, double c3 = 0, double c4 = 0)
        : this(space, c1, c2, c3, c4, alpha: 1)
    {
    }

    /// <summary>Creates a color with opacity.</summary>
    PdfColor(PdfColorSpace space, double c1, double c2, double c3, double c4, double alpha)
    {
        Space = space;
        C1 = Clamp(c1);
        C2 = Clamp(c2);
        C3 = Clamp(c3);
        C4 = Clamp(c4);
        Alpha = Clamp(alpha);
    }

    /// <summary>Color space.</summary>
    public PdfColorSpace Space { get; }

    /// <summary>First component (gray, red or cyan).</summary>
    public double C1 { get; }

    /// <summary>Second component (green or magenta).</summary>
    public double C2 { get; }

    /// <summary>Third component (blue or yellow).</summary>
    public double C3 { get; }

    /// <summary>Fourth component (black).</summary>
    public double C4 { get; }

    /// <summary>
    /// Opacity from 0 (invisible) to 1 (opaque). Colors created without opacity are opaque.
    /// A color is not mixed with its background in advance; the viewer blends it with whatever
    /// lies below.
    /// </summary>
    public double Alpha { get; }

    /// <summary>Whether the color is fully opaque.</summary>
    public bool IsOpaque => Alpha >= 1;

    /// <summary>Black.</summary>
    public static PdfColor Black => Gray(0);

    /// <summary>White.</summary>
    public static PdfColor White => Gray(1);

    /// <summary>Gray level from 0 (black) to 1 (white).</summary>
    public static PdfColor Gray(double value) => new(PdfColorSpace.Gray, value);

    /// <summary>RGB color with components from 0 to 1.</summary>
    public static PdfColor Rgb(double red, double green, double blue) => new(PdfColorSpace.Rgb, red, green, blue);

    /// <summary>RGB color with components from 0 to 255.</summary>
    public static PdfColor FromRgb(byte red, byte green, byte blue) => Rgb(red / 255.0, green / 255.0, blue / 255.0);

    /// <summary>CMYK color with components from 0 to 1.</summary>
    public static PdfColor Cmyk(double cyan, double magenta, double yellow, double black)
        => new(PdfColorSpace.Cmyk, cyan, magenta, yellow, black);

    /// <summary>
    /// RGB color from "#RRGGBB" or "#AARRGGBB" (the "#" is optional). In the eight-digit form the
    /// first two digits are the opacity, e.g. "#80FFFF00" is yellow at about 50 % opacity. This is
    /// the order used by .NET, Android and many editors; note that CSS puts the opacity last
    /// ("#RRGGBBAA").
    /// </summary>
    /// <exception cref="FormatException">The text is not a color in one of these formats.</exception>
    public static PdfColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (digits.Length is not (6 or 8) || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"\"{hex}\" is not a color in the format #RRGGBB or #AARRGGBB.");
        var color = FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return digits.Length == 8 ? color.WithAlpha((byte)(value >> 24) / 255.0) : color;
    }

    /// <summary>
    /// Returns the same color with another opacity from 0 (invisible) to 1 (opaque).
    /// The color itself is not changed.
    /// </summary>
    public PdfColor WithAlpha(double alpha) => new(Space, C1, C2, C3, C4, alpha);

    /// <summary>Limits a component to the range 0–1; NaN becomes 0.</summary>
    static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
}
