// Decides in which order the fixed blocks of a page are drawn relative to the flowing content.
//
// The order in the content stream has two meanings at once:
//   1. Stacking: what is drawn later covers what was drawn earlier.
//   2. Reading order of untagged PDFs: copying text, text search and many screen readers follow
//      the content stream from start to end.
// The layers (FixedLayer) only express stacking. Drawing simply "background, flow, foreground"
// would put a page header in the foreground layer after the body text when reading.
//
// So blocks are moved where stacking does not matter: a block that lies completely outside the
// content area cannot overlap the flowing content.
//   - Foreground blocks above the content area (header) are drawn before the flowing content.
//   - Background blocks below the content area (footer) are drawn after the flowing content.
// A block is only moved if it does not overlap a block of its own layer whose relative order
// would change; otherwise the visible result could differ. The decision uses the block's area
// (FixedBlock.Area); content overflowing an unclipped area is not taken into account.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout;

/// <summary>Arranges the fixed blocks of a page template before and after the flowing content.</summary>
internal static class PageDrawOrder
{
    /// <summary>Tolerance for comparing positions in points.</summary>
    const double Tolerance = 1e-6;

    /// <summary>
    /// Splits the fixed blocks of a template into those drawn before and those drawn after the
    /// flowing content. The visible result is the same as "background, flow, foreground".
    /// </summary>
    public static (IReadOnlyList<FixedBlock> BeforeFlow, IReadOnlyList<FixedBlock> AfterFlow) Arrange(PageTemplate template)
    {
        var background = template.FixedBlocks.Where(b => b.Layer == FixedLayer.Background).ToList();
        var foreground = template.FixedBlocks.Where(b => b.Layer == FixedLayer.Foreground).ToList();
        if (template.ContentArea is not { } area)
            return (background, foreground);

        // Background below the area moves behind the flow, i.e. after all background blocks that
        // stay: allowed if no later background block overlaps it.
        var backgroundMoves = background
            .Select((block, i) => IsBelow(block.Area, area) && !Overlaps(block, background.Skip(i + 1)))
            .ToList();
        // Foreground above the area moves before the flow, i.e. before all foreground blocks that
        // stay: allowed if no earlier foreground block overlaps it.
        var foregroundMoves = foreground
            .Select((block, i) => IsAbove(block.Area, area) && !Overlaps(block, foreground.Take(i)))
            .ToList();

        // Selected by position in the list, not by equality: FixedBlock is a record, and two
        // equal blocks may appear twice on purpose.
        var before = Select(background, backgroundMoves, moved: false).Concat(Select(foreground, foregroundMoves, moved: true)).ToList();
        var after = Select(background, backgroundMoves, moved: true).Concat(Select(foreground, foregroundMoves, moved: false)).ToList();
        return (before, after);
    }

    /// <summary>Returns the blocks whose decision equals <paramref name="moved"/>, in their original order.</summary>
    static IEnumerable<FixedBlock> Select(List<FixedBlock> blocks, List<bool> moves, bool moved)
        => blocks.Where((_, i) => moves[i] == moved);

    /// <summary>Whether a box lies completely above the content area.</summary>
    static bool IsAbove(Box box, Box area) => box.Bottom <= area.Y + Tolerance;

    /// <summary>Whether a box lies completely below the content area.</summary>
    static bool IsBelow(Box box, Box area) => box.Y >= area.Bottom - Tolerance;

    /// <summary>Whether the block's area overlaps the area of one of the other blocks (touching edges do not count).</summary>
    static bool Overlaps(FixedBlock block, IEnumerable<FixedBlock> others)
        => others.Any(other => Intersect(block.Area, other.Area));

    /// <summary>Whether two boxes share an area larger than zero.</summary>
    static bool Intersect(Box a, Box b)
        => a.X < b.Right - Tolerance && b.X < a.Right - Tolerance
        && a.Y < b.Bottom - Tolerance && b.Y < a.Bottom - Tolerance;
}
