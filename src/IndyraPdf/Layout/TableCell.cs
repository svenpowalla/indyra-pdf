// A cell of a table row: its content frame and how many columns it spans.

namespace IndyraPdf.Layout;

/// <summary>A table cell.</summary>
/// <param name="Content">Content with padding, borders and background.</param>
/// <param name="ColumnSpan">Number of columns the cell covers (at least 1), like MigraDoc's MergeRight + 1.</param>
public sealed record TableCell(Frame Content, int ColumnSpan = 1)
{
    /// <summary>Number of columns the cell covers.</summary>
    public int ColumnSpan { get; } = ColumnSpan >= 1
        ? ColumnSpan
        : throw new ArgumentOutOfRangeException(nameof(ColumnSpan), "A cell spans at least one column.");
}
