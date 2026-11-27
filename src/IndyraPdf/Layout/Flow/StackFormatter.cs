// Places a sequence of blocks one below the other, across as many areas as needed.
// Used for the content flow of a document, for frames and for table cells.

namespace IndyraPdf.Layout.Flow;

/// <summary>Formats blocks from top to bottom; a block that does not fit continues in the next area.</summary>
internal sealed class StackFormatter : IBlockFormatter
{
    /// <summary>The blocks.</summary>
    readonly IReadOnlyList<Block> _blocks;

    /// <summary>Fonts and field values.</summary>
    readonly LayoutEnvironment _environment;

    /// <summary>Available width.</summary>
    readonly double _width;

    /// <summary>Index of the block being placed.</summary>
    int _index;

    /// <summary>Formatter of the block being placed (created when it is first placed).</summary>
    IBlockFormatter? _current;

    /// <summary>Creates the formatter.</summary>
    public StackFormatter(IReadOnlyList<Block> blocks, LayoutEnvironment environment, double width)
    {
        _blocks = blocks;
        _environment = environment;
        _width = width;
    }

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        var placed = new List<(IFragment Fragment, double Y)>();
        double used = 0;
        while (_index < _blocks.Count)
        {
            _current ??= _blocks[_index].CreateFormatter(_environment, _width);
            var result = _current.Format(availableHeight - used, atTopOfArea && placed.Count == 0);
            if (result.Fragment is { } fragment)
            {
                placed.Add((fragment, used));
                used += fragment.Height;
            }
            if (!result.IsComplete)
                break; // the area is full; the block continues in the next area
            _index++;
            _current = null;
        }

        bool complete = _index == _blocks.Count;
        if (placed.Count == 0 && !complete)
            return FormatResult.NothingFits;
        return new FormatResult(new StackFragment(placed, used), complete);
    }
}
