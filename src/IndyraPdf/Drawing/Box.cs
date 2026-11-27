// Axis-aligned rectangle used by the drawing surface and the layout engine.

namespace IndyraPdf.Drawing;

/// <summary>
/// A rectangle in points. (X, Y) is the upper left corner; y grows downwards, as everywhere in
/// IndyraPdf.
/// </summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct Box(double X, double Y, double Width, double Height)
{
    /// <summary>Right edge.</summary>
    public double Right => X + Width;

    /// <summary>Bottom edge.</summary>
    public double Bottom => Y + Height;
}
