// Places an image block as a whole.

namespace IndyraPdf.Layout.Flow;

/// <summary>Formatter for <see cref="ImageBlock"/>: places the image as a whole or not at all.</summary>
internal sealed class ImageFormatter : IBlockFormatter
{
    /// <summary>The image block.</summary>
    readonly ImageBlock _block;

    /// <summary>Available width.</summary>
    readonly double _width;

    /// <summary>Creates the formatter.</summary>
    public ImageFormatter(ImageBlock block, double width)
    {
        _block = block;
        _width = width;
    }

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        var (w, h) = _block.ComputeSize(_width);
        if (h > availableHeight + 1e-6 && !atTopOfArea)
            return FormatResult.NothingFits;
        double offset = _block.Alignment switch
        {
            TextAlignment.Center => (_width - w) / 2,
            TextAlignment.Right => _width - w,
            _ => 0,
        };
        IFragment fragment = new ImageFragment(_block.Image, offset, w, h);
        // With alternative text the image is a figure; without, it is decoration.
        fragment = _block.AlternativeText is { } alt
            ? new TaggedFragment(new Drawing.StructureTag(Drawing.StructureRole.Figure, _block, alt), fragment)
            : new ArtifactFragment(fragment);
        return new FormatResult(fragment, IsComplete: true);
    }
}
