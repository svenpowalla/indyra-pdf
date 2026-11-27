// Unit conversion. PDF measures everything in points; documents are usually designed in millimeters.

namespace IndyraPdf.Writer;

/// <summary>Converts between points (1/72 inch, the unit of PDF) and metric units.</summary>
public static class PdfUnits
{
    /// <summary>Points per inch.</summary>
    public const double PointsPerInch = 72;

    /// <summary>Millimeters per inch.</summary>
    const double MillimetersPerInch = 25.4;

    /// <summary>Converts millimeters to points.</summary>
    public static double FromMillimeters(double mm) => mm * PointsPerInch / MillimetersPerInch;

    /// <summary>Converts centimeters to points.</summary>
    public static double FromCentimeters(double cm) => FromMillimeters(cm * 10);

    /// <summary>Converts inches to points.</summary>
    public static double FromInches(double inches) => inches * PointsPerInch;

    /// <summary>Converts points to millimeters.</summary>
    public static double ToMillimeters(double points) => points * MillimetersPerInch / PointsPerInch;
}
