// Wraps a fragment in a structure element (tagged PDF).

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>Draws a fragment inside a structure element, e.g. a table part inside "Table".</summary>
internal sealed class TaggedFragment : IFragment
{
    /// <summary>The structure element.</summary>
    readonly StructureTag _tag;

    /// <summary>The content.</summary>
    readonly IFragment _content;

    /// <summary>Creates the wrapper.</summary>
    public TaggedFragment(StructureTag tag, IFragment content)
    {
        _tag = tag;
        _content = content;
    }

    /// <inheritdoc/>
    public double Height => _content.Height;

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
    {
        surface.BeginTag(_tag, y);
        _content.Draw(surface, x, y);
        surface.EndTag();
    }
}
