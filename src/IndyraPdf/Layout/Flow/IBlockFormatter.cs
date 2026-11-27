// Formats a block step by step into areas of limited height (pages, table cells, frames).
//
// The layout engine calls Format once per area. A formatter remembers how far it got, so a
// block that does not fit continues in the next area where it stopped. This follows the
// formatter concept of MigraDoc 6.2.4 (Format with "previous format info").

namespace IndyraPdf.Layout.Flow;

/// <summary>Places a block into areas of limited height, continuing where it stopped.</summary>
internal interface IBlockFormatter
{
    /// <summary>
    /// Places as much of the block as fits into <paramref name="availableHeight"/>.
    /// </summary>
    /// <param name="availableHeight">Free height in the current area.</param>
    /// <param name="atTopOfArea">
    /// Whether nothing has been placed in the area yet. Then the formatter must place at least
    /// something, even if it does not fit, so that layout always makes progress.
    /// </param>
    FormatResult Format(double availableHeight, bool atTopOfArea);
}
