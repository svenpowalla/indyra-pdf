// A placed image.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>An image placed at a horizontal offset with a computed size.</summary>
internal sealed class ImageFragment : IFragment
{
    /// <summary>The image.</summary>
    readonly ImageSource _image;

    /// <summary>Horizontal offset within the available width (for centered or right-aligned images).</summary>
    readonly double _offset;

    /// <summary>Drawn width.</summary>
    readonly double _width;

    /// <summary>Creates the fragment.</summary>
    public ImageFragment(ImageSource image, double offset, double width, double height)
    {
        _image = image;
        _offset = offset;
        _width = width;
        Height = height;
    }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
        => surface.DrawImage(_image, new Box(x + _offset, y, _width, Height));
}
