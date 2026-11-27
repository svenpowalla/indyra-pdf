// Appearance of lines and outlines.

using IndyraPdf.Writer;

namespace IndyraPdf.Drawing;

/// <summary>Appearance of a line: width, color and optional dash pattern.</summary>
/// <param name="Width">Line width in points.</param>
/// <param name="Color">Line color.</param>
/// <param name="DashPattern">Alternating dash and gap lengths in points; null for a solid line.</param>
public sealed record LineStyle(double Width, PdfColor Color, IReadOnlyList<double>? DashPattern = null)
{
    /// <summary>Thin black solid line (0.75 pt).</summary>
    public static LineStyle Thin { get; } = new(0.75, PdfColor.Black);
}
