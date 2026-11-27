// A measured table row: the content of every cell and the common row height.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>
/// A table row with the formatted content of each cell. All cells of a row get the height of the
/// tallest cell, so backgrounds and borders line up. A cell spanning several columns gets the
/// sum of their widths.
/// </summary>
internal sealed class RowLayout : IFragment
{
    /// <summary>
    /// Placed cells: table cell, number of columns it actually covers (its span, cut off at the
    /// last column), horizontal offset, width and formatted content.
    /// </summary>
    readonly IReadOnlyList<(TableCell Cell, int Span, double X, double Width, IFragment? Content)> _cells;

    /// <summary>The row (key of its structure element).</summary>
    readonly TableRow _row;

    /// <summary>Whether the row is a header row (cells tagged as TH instead of TD).</summary>
    readonly bool _isHeader;

    /// <summary>Measures a row: every cell is formatted with unlimited height.</summary>
    public RowLayout(TableRow row, bool isHeader, double[] columnWidths, LayoutEnvironment environment)
    {
        _row = row;
        _isHeader = isHeader;
        var cells = new List<(TableCell, int, double, double, IFragment?)>();
        double height = row.MinHeight;
        int column = 0;
        double x = 0;
        foreach (var tableCell in row.Cells)
        {
            if (column >= columnWidths.Length)
                break;
            int span = Math.Min(tableCell.ColumnSpan, columnWidths.Length - column);
            double width = columnWidths.Skip(column).Take(span).Sum();
            var frame = tableCell.Content;

            double innerWidth = Math.Max(0, width - frame.Padding.Horizontal);
            var content = new StackFormatter(frame.Blocks, environment, innerWidth)
                .Format(double.PositiveInfinity, atTopOfArea: true).Fragment;
            height = Math.Max(height, Math.Max(frame.MinHeight, (content?.Height ?? 0) + frame.Padding.Vertical));
            cells.Add((tableCell, span, x, width, content));

            column += span;
            x += width;
        }
        _cells = cells;
        Height = height;
    }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
    {
        surface.BeginTag(new StructureTag(StructureRole.TableRow, _row), y);
        var cellRole = _isHeader ? StructureRole.TableHeaderCell : StructureRole.TableDataCell;
        foreach (var (cell, span, offset, width, content) in _cells)
        {
            surface.BeginTag(new StructureTag(cellRole, cell, ColumnSpan: span), y);
            cell.Content.DrawPart(surface, new Box(x + offset, y, width, Height), content);
            surface.EndTag();
        }
        surface.EndTag();
    }
}
