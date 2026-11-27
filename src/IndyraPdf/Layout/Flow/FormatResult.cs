// Result of one formatting step of a block.

namespace IndyraPdf.Layout.Flow;

/// <summary>What a formatter placed in one area, and whether the block is complete.</summary>
/// <param name="Fragment">The placed part, or null if nothing fit.</param>
/// <param name="IsComplete">Whether the whole block has been placed.</param>
internal sealed record FormatResult(IFragment? Fragment, bool IsComplete)
{
    /// <summary>Nothing fit; the block must continue in the next area.</summary>
    public static FormatResult NothingFits { get; } = new(null, false);
}
