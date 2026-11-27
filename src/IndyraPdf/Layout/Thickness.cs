// Distances on the four sides of a box (inner padding of frames and table cells).

namespace IndyraPdf.Layout;

/// <summary>Distances in points on the four sides of a box.</summary>
/// <param name="Left">Left side.</param>
/// <param name="Top">Top side.</param>
/// <param name="Right">Right side.</param>
/// <param name="Bottom">Bottom side.</param>
public readonly record struct Thickness(double Left, double Top, double Right, double Bottom)
{
    /// <summary>The same distance on all sides.</summary>
    public static Thickness Uniform(double value) => new(value, value, value, value);

    /// <summary>Sum of left and right.</summary>
    public double Horizontal => Left + Right;

    /// <summary>Sum of top and bottom.</summary>
    public double Vertical => Top + Bottom;
}
