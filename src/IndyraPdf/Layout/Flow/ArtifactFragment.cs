// Marks a fragment as artifact (decoration or repetition, skipped by screen readers).

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>Draws a fragment as artifact, e.g. a table header repeated on a following page.</summary>
internal sealed class ArtifactFragment : IFragment
{
    /// <summary>The content.</summary>
    readonly IFragment _content;

    /// <summary>Creates the wrapper.</summary>
    public ArtifactFragment(IFragment content) => _content = content;

    /// <inheritdoc/>
    public double Height => _content.Height;

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
    {
        surface.BeginArtifact();
        _content.Draw(surface, x, y);
        surface.EndArtifact();
    }
}
