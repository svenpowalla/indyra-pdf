// Places a table row by row; header rows are repeated in every area the table continues in.

namespace IndyraPdf.Layout.Flow;

/// <summary>Formatter for <see cref="Table"/>.</summary>
internal sealed class TableFormatter : IBlockFormatter
{
    /// <summary>The table.</summary>
    readonly Table _table;

    /// <summary>Fonts and field values.</summary>
    readonly LayoutEnvironment _environment;

    /// <summary>Column widths in points.</summary>
    readonly double[] _columnWidths;

    /// <summary>Measured header rows (measured on the first call).</summary>
    List<RowLayout>? _headers;

    /// <summary>Index of the next body row to place.</summary>
    int _nextRow;

    /// <summary>Whether the header rows have been placed once (later placements are repetitions).</summary>
    bool _headerPlaced;

    /// <summary>Creates the formatter.</summary>
    public TableFormatter(Table table, LayoutEnvironment environment, double[] columnWidths)
    {
        _table = table;
        _environment = environment;
        _columnWidths = columnWidths;
    }

    /// <summary>Number of header rows actually present.</summary>
    int HeaderCount => Math.Clamp(_table.HeaderRowCount, 0, _table.Rows.Count);

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        if (_headers is null)
        {
            _headers = _table.Rows.Take(HeaderCount).Select(Measure).ToList();
            _nextRow = HeaderCount;
        }

        var parts = new List<(IFragment, double)>();
        double used = 0;
        foreach (var header in _headers)
        {
            // Repeated headers on following pages are artifacts: screen readers read them once.
            parts.Add((_headerPlaced ? new ArtifactFragment(header) : header, used));
            used += header.Height;
        }

        // The header must not stand alone at the bottom of a page: it needs at least one body row.
        int bodyRowsPlaced = 0;
        while (_nextRow < _table.Rows.Count)
        {
            var row = Measure(_table.Rows[_nextRow]);
            bool fits = used + row.Height <= availableHeight + 1e-6;
            bool mustPlace = atTopOfArea && bodyRowsPlaced == 0;
            if (!fits && !mustPlace)
                break;
            parts.Add((row, used));
            used += row.Height;
            bodyRowsPlaced++;
            _nextRow++;
        }

        bool complete = _nextRow == _table.Rows.Count;
        if (bodyRowsPlaced == 0 && !complete)
            return FormatResult.NothingFits;
        _headerPlaced = true;
        var table = new StackFragment(parts, used);
        return new FormatResult(new TaggedFragment(new Drawing.StructureTag(Drawing.StructureRole.Table, _table), table), complete);
    }

    /// <summary>Measures a row with the field values of the current page.</summary>
    RowLayout Measure(TableRow row)
        => new(row, _table.Rows.Take(HeaderCount).Contains(row), _columnWidths, _environment);
}
