// Width definition of a table column: fixed in points or a share of the remaining width.

namespace IndyraPdf.Layout;

/// <summary>Width of a table column.</summary>
public sealed record TableColumn
{
    /// <summary>Use <see cref="Fixed"/> or <see cref="Relative"/>.</summary>
    TableColumn(double? fixedWidth, double weight)
    {
        if (fixedWidth < 0 || weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Column widths must not be negative.");
        FixedWidth = fixedWidth;
        Weight = weight;
    }

    /// <summary>Column of a fixed width in points.</summary>
    public static TableColumn Fixed(double points) => new(points, 0);

    /// <summary>
    /// Column that gets a share of the width left after the fixed columns, proportional to its
    /// weight. Two columns with weights 1 and 3 get a quarter and three quarters.
    /// </summary>
    public static TableColumn Relative(double weight = 1) => new(null, weight);

    /// <summary>Fixed width in points, or null for a relative column.</summary>
    public double? FixedWidth { get; }

    /// <summary>Weight of a relative column.</summary>
    public double Weight { get; }

    /// <summary>Computes the widths of all columns for a table of the given width.</summary>
    internal static double[] ComputeWidths(IReadOnlyList<TableColumn> columns, double tableWidth)
    {
        double fixedSum = columns.Sum(c => c.FixedWidth ?? 0);
        double weightSum = columns.Where(c => c.FixedWidth is null).Sum(c => c.Weight);
        double remaining = Math.Max(0, tableWidth - fixedSum);
        return columns
            .Select(c => c.FixedWidth ?? (weightSum > 0 ? remaining * c.Weight / weightSum : 0))
            .ToArray();
    }
}
