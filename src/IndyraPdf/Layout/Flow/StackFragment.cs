// Fragments of several blocks placed one below the other.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>Fragments stacked vertically, each with its offset from the top.</summary>
internal sealed class StackFragment : IFragment
{
    /// <summary>The fragments with their vertical offsets.</summary>
    readonly IReadOnlyList<(IFragment Fragment, double Y)> _parts;

    /// <summary>Creates the fragment.</summary>
    public StackFragment(IReadOnlyList<(IFragment Fragment, double Y)> parts, double height)
    {
        _parts = parts;
        Height = height;
    }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
    {
        foreach (var (fragment, offset) in _parts)
            fragment.Draw(surface, x, y + offset);
    }
}
