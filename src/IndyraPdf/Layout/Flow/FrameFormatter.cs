// Places a frame: its content is placed inside the padding; the frame may be split.

namespace IndyraPdf.Layout.Flow;

/// <summary>Formatter for <see cref="Frame"/>.</summary>
internal sealed class FrameFormatter : IBlockFormatter
{
    /// <summary>The frame.</summary>
    readonly Frame _frame;

    /// <summary>Formats the content inside the padding.</summary>
    readonly StackFormatter _content;

    /// <summary>Width of the frame.</summary>
    readonly double _width;

    /// <summary>Whether the first part has been placed (the minimum height only applies to it).</summary>
    bool _started;

    /// <summary>Creates the formatter.</summary>
    public FrameFormatter(Frame frame, LayoutEnvironment environment, double width)
    {
        _frame = frame;
        _width = width;
        _content = new StackFormatter(frame.Blocks, environment, Math.Max(0, width - frame.Padding.Horizontal));
    }

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        // The minimum height applies to the first part. If it does not fit, the frame starts in
        // the next area instead of running beyond the bottom of this one.
        if (!_started && !atTopOfArea && _frame.MinHeight > availableHeight + 1e-6)
            return FormatResult.NothingFits;

        var padding = _frame.Padding;
        var result = _content.Format(availableHeight - padding.Vertical, atTopOfArea);
        if (result.Fragment is null)
            return FormatResult.NothingFits;

        double height = result.Fragment.Height + padding.Vertical;
        if (!_started)
            height = Math.Max(height, _frame.MinHeight);
        _started = true;
        return new FormatResult(new FrameFragment(_frame, result.Fragment, _width, height), result.IsComplete);
    }
}
