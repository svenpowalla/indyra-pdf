// A table: columns, rows of cells, header rows repeated on every page.
//
// Comparison: FPDF has no tables (only single cells). MigraDoc 6.2.4 repeats header rows and
// keeps every row on one page; IndyraPdf does the same. A row is never split; a row taller than
// a whole page is placed anyway and overflows, so layout always finishes.

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>A table with column definitions, rows and repeated header rows.</summary>
public sealed class Table : Block
{
    /// <summary>Column definitions.</summary>
    readonly List<TableColumn> _columns = [];

    /// <summary>All rows, header rows first.</summary>
    readonly List<TableRow> _rows = [];

    /// <summary>Column definitions from left to right.</summary>
    public IReadOnlyList<TableColumn> Columns => _columns;

    /// <summary>All rows, header rows first.</summary>
    public IReadOnlyList<TableRow> Rows => _rows;

    /// <summary>Number of leading rows repeated at the top of every page the table spans.</summary>
    public int HeaderRowCount { get; set; }

    /// <summary>Appends a column definition.</summary>
    public Table AddColumn(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        _columns.Add(column);
        return this;
    }

    /// <summary>Appends a row.</summary>
    public Table AddRow(TableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _rows.Add(row);
        return this;
    }

    /// <inheritdoc/>
    internal override IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("A table needs at least one column.");
        return new TableFormatter(this, environment, TableColumn.ComputeWidths(_columns, width));
    }
}
