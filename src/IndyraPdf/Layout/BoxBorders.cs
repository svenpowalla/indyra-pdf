// Border lines on the four sides of a frame or table cell.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout;

/// <summary>
/// Border lines of a box; null means no line on that side. Lines are centered on the edge of the
/// box, so the borders of neighbouring table cells lie exactly on top of each other.
/// </summary>
public sealed record BoxBorders
{
    /// <summary>No borders.</summary>
    public static BoxBorders None { get; } = new();

    /// <summary>The same line on all four sides.</summary>
    public static BoxBorders All(LineStyle line) => new() { Top = line, Right = line, Bottom = line, Left = line };

    /// <summary>Line on the top side.</summary>
    public LineStyle? Top { get; init; }

    /// <summary>Line on the right side.</summary>
    public LineStyle? Right { get; init; }

    /// <summary>Line on the bottom side.</summary>
    public LineStyle? Bottom { get; init; }

    /// <summary>Line on the left side.</summary>
    public LineStyle? Left { get; init; }

    /// <summary>Draws the lines around a box.</summary>
    internal void Draw(IDrawingSurface surface, Box box)
    {
        if (Top is { } top)
            surface.DrawLine(box.X, box.Y, box.Right, box.Y, top);
        if (Bottom is { } bottom)
            surface.DrawLine(box.X, box.Bottom, box.Right, box.Bottom, bottom);
        if (Left is { } left)
            surface.DrawLine(box.X, box.Y, box.X, box.Bottom, left);
        if (Right is { } right)
            surface.DrawLine(box.Right, box.Y, box.Right, box.Bottom, right);
    }
}
