// Values for page number fields, known once the document has been broken into pages.

namespace IndyraPdf.Layout;

/// <summary>Values inserted into page number fields.</summary>
/// <param name="PageNumber">Number of the current page, starting at 1.</param>
/// <param name="TotalPages">Total number of pages.</param>
public readonly record struct FieldValues(int PageNumber, int TotalPages)
{
    /// <summary>
    /// Placeholder values used while the page count is not known yet. Two digits reserve enough
    /// width for documents up to 99 pages.
    /// </summary>
    public static FieldValues Estimate { get; } = new(88, 88);
}
