// A table row: cells from left to right, each spanning one or more columns.

namespace IndyraPdf.Layout;

/// <summary>A row of a <see cref="Table"/>.</summary>
public sealed class TableRow
{
    /// <summary>The cells from left to right.</summary>
    readonly List<TableCell> _cells = [];

    /// <summary>
    /// The cells from left to right. Columns not covered by any cell stay empty; cells beyond the
    /// last column are ignored.
    /// </summary>
    public IReadOnlyList<TableCell> Cells => _cells;

    /// <summary>Minimum height of the row in points.</summary>
    public double MinHeight { get; set; }

    /// <summary>Appends a cell covering one or more columns.</summary>
    public TableRow Add(Frame content, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(content);
        _cells.Add(new TableCell(content, columnSpan));
        return this;
    }
}
