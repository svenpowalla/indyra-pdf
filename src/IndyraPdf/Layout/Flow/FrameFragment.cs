// A placed frame (or part of a frame) of fixed size.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>A frame part with its content fragment.</summary>
internal sealed class FrameFragment : IFragment
{
    /// <summary>The frame (padding, background, borders).</summary>
    readonly Frame _frame;

    /// <summary>The content placed inside.</summary>
    readonly IFragment _content;

    /// <summary>Width of the frame.</summary>
    readonly double _width;

    /// <summary>Creates the fragment.</summary>
    public FrameFragment(Frame frame, IFragment content, double width, double height)
    {
        _frame = frame;
        _content = content;
        _width = width;
        Height = height;
    }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
        => _frame.DrawPart(surface, new Box(x, y, _width, Height), _content);
}
