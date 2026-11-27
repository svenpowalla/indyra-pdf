// Page sizes in points, including the common ISO and US formats.

namespace IndyraPdf.Writer;

/// <summary>Size of a page in points.</summary>
/// <param name="Width">Width in points.</param>
/// <param name="Height">Height in points.</param>
public readonly record struct PageSize(double Width, double Height)
{
    /// <summary>ISO A3 portrait (297 × 420 mm).</summary>
    public static PageSize A3 => FromMillimeters(297, 420);

    /// <summary>ISO A4 portrait (210 × 297 mm).</summary>
    public static PageSize A4 => FromMillimeters(210, 297);

    /// <summary>ISO A5 portrait (148 × 210 mm).</summary>
    public static PageSize A5 => FromMillimeters(148, 210);

    /// <summary>US Letter (8.5 × 11 inch).</summary>
    public static PageSize Letter => new(612, 792);

    /// <summary>US Legal (8.5 × 14 inch).</summary>
    public static PageSize Legal => new(612, 1008);

    /// <summary>Creates a page size from millimeters.</summary>
    public static PageSize FromMillimeters(double width, double height)
        => new(PdfUnits.FromMillimeters(width), PdfUnits.FromMillimeters(height));

    /// <summary>The same size in landscape orientation (wider than high).</summary>
    public PageSize Landscape => Width >= Height ? this : new PageSize(Height, Width);

    /// <summary>The same size in portrait orientation (higher than wide).</summary>
    public PageSize Portrait => Width <= Height ? this : new PageSize(Height, Width);
}
