// An image as content block, scaled to a size while keeping its aspect ratio.

using IndyraPdf.Drawing;
using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>An image block. It is never split; if it does not fit, it moves to the next area.</summary>
/// <remarks>
/// Size rules, always keeping the aspect ratio:
/// <list type="bullet">
/// <item>Width and height given: the image is scaled to fit into this box (it may be smaller in one direction).</item>
/// <item>Only width or only height given: the other dimension follows from the aspect ratio.</item>
/// <item>Nothing given: natural size at 96 dpi, reduced to the available width if necessary.</item>
/// </list>
/// The image is never wider than the available width.
/// </remarks>
public sealed class ImageBlock : Block
{
    /// <summary>Points per pixel at the usual screen resolution of 96 dpi.</summary>
    const double PointsPerPixel = 72.0 / 96.0;

    /// <summary>Creates an image block.</summary>
    public ImageBlock(ImageSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Image = image;
    }

    /// <summary>The image.</summary>
    public ImageSource Image { get; }

    /// <summary>Maximum width in points, or null.</summary>
    public double? Width { get; set; }

    /// <summary>Maximum height in points, or null.</summary>
    public double? Height { get; set; }

    /// <summary>Horizontal position within the available width (only Left, Center and Right are used).</summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    /// <summary>
    /// Text read by screen readers instead of the image (tagged PDF: "Figure" with /Alt). Without
    /// it, the image counts as decoration (artifact), e.g. for logos.
    /// </summary>
    public string? AlternativeText { get; set; }

    /// <inheritdoc/>
    internal override IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width)
        => new ImageFormatter(this, width);

    /// <summary>Computes the drawn size for the given available width.</summary>
    internal (double Width, double Height) ComputeSize(double availableWidth)
    {
        double naturalWidth = Image.PixelWidth * PointsPerPixel;
        double naturalHeight = Image.PixelHeight * PointsPerPixel;
        double maxWidth = Math.Min(Width ?? naturalWidth, availableWidth);
        double maxHeight = Height ?? double.PositiveInfinity;
        if (Width is null && Height is not null)
            maxWidth = availableWidth;

        double scale = Math.Min(maxWidth / naturalWidth, maxHeight / naturalHeight);
        return (naturalWidth * scale, naturalHeight * scale);
    }
}
