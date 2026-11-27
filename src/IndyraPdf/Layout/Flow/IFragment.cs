// A placed piece of a block: the part of a block that went onto one page.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>
/// The part of a block placed in one area (usually one page). It knows its height and draws itself.
/// </summary>
internal interface IFragment
{
    /// <summary>Height of the fragment in points.</summary>
    double Height { get; }

    /// <summary>Draws the fragment with its upper left corner at (x, y).</summary>
    void Draw(IDrawingSurface surface, double x, double y);
}
