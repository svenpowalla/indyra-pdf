// Identifies a structure element while the layout engine draws its content.

namespace IndyraPdf.Drawing;

/// <summary>
/// A structure element to open on a drawing surface.
/// </summary>
/// <param name="Role">Semantic role.</param>
/// <param name="Key">
/// Identity of the element. Opening a tag with the same key again (e.g. on the next page, when a
/// paragraph or table continues) continues the same structure element.
/// </param>
/// <param name="AlternativeText">Text read instead of the content (required for <see cref="StructureRole.Figure"/>).</param>
/// <param name="ColumnSpan">
/// Number of table columns a cell covers (at least 1). Only meaningful for
/// <see cref="StructureRole.TableHeaderCell"/> and <see cref="StructureRole.TableDataCell"/>.
/// The PDF writer stores it as the table attribute /ColSpan (ISO 32000-1, 14.8.5.7), so screen
/// readers and validators count the columns of a row correctly.
/// </param>
public sealed record StructureTag(StructureRole Role, object Key, string? AlternativeText = null, int ColumnSpan = 1)
{
    /// <summary>Number of table columns a cell covers.</summary>
    public int ColumnSpan { get; } = ColumnSpan >= 1
        ? ColumnSpan
        : throw new ArgumentOutOfRangeException(nameof(ColumnSpan), "A cell spans at least one column.");
}
