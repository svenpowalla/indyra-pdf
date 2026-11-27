// Result of the layout engine: pages ready to be drawn onto any drawing surface.
//
// Merging documents: call AppendTo for several laid-out documents with the same PdfDocument.
// Every document keeps its own page numbering ("page 1 of 2" per letter in a mail merge).

using IndyraPdf.Drawing;
using IndyraPdf.Layout.Flow;
using IndyraPdf.Writer;

namespace IndyraPdf.Layout;

/// <summary>A document broken into pages, ready to be drawn.</summary>
public sealed class LaidOutDocument
{
    /// <summary>Content may exceed its area by this much (in points) without counting as overflow.</summary>
    const double OverflowTolerance = 0.01;

    /// <summary>Fonts, needed to lay out fixed blocks per page.</summary>
    readonly FontCollection _fonts;

    /// <summary>The pages.</summary>
    readonly IReadOnlyList<LaidOutPage> _pages;

    /// <summary>Overflows, determined on first request (fixed blocks must be laid out for it).</summary>
    readonly Lazy<IReadOnlyList<LayoutOverflow>> _overflows;

    /// <summary>Creates the result. Only <see cref="LayoutDocument.Layout"/> calls this.</summary>
    internal LaidOutDocument(FontCollection fonts, IReadOnlyList<LaidOutPage> pages)
    {
        _fonts = fonts;
        _pages = pages;
        _overflows = new Lazy<IReadOnlyList<LayoutOverflow>>(FindOverflows);
    }

    /// <summary>Number of pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>
    /// Places where content is taller than its area: flowing content that does not fit onto a
    /// page, and fixed blocks whose content is taller than <see cref="FixedBlock.Area"/>. Sorted
    /// by page; on each page the flowing content comes first, then the fixed blocks in the order
    /// of the template.
    /// </summary>
    public IReadOnlyList<LayoutOverflow> Overflows => _overflows.Value;

    /// <summary>Whether any content is taller than its area (see <see cref="Overflows"/>).</summary>
    public bool HasOverflow => Overflows.Count > 0;

    /// <summary>Size of a page.</summary>
    public PageSize GetPageSize(int pageIndex) => _pages[pageIndex].Template.Size;

    /// <summary>
    /// Draws a page. The visible result is: fixed blocks of the background layer, then the
    /// flowing content, then fixed blocks of the foreground layer. Blocks that do not overlap the
    /// content area are drawn in reading order, e.g. a header in the foreground before the
    /// flowing content (see <see cref="PageDrawOrder"/>).
    /// </summary>
    /// <param name="pageIndex">Page, starting at 0.</param>
    /// <param name="surface">Target surface.</param>
    public void DrawPage(int pageIndex, IDrawingSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var page = _pages[pageIndex];
        var fields = new FieldValues(pageIndex + 1, _pages.Count);
        var (beforeFlow, afterFlow) = PageDrawOrder.Arrange(page.Template);

        DrawFixed(surface, beforeFlow, fields);
        if (page.Content is { } content && page.Template.ContentArea is { } area)
            content.Draw(surface, area.X, area.Y);
        DrawFixed(surface, afterFlow, fields);
    }

    /// <summary>Appends all pages to a PDF document.</summary>
    public void AppendTo(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (int i = 0; i < _pages.Count; i++)
        {
            var page = document.AddPage(GetPageSize(i));
            DrawPage(i, new PdfDrawingSurface(document, page));
        }
    }

    /// <summary>Creates a PDF document with all pages.</summary>
    public PdfDocument ToPdf()
    {
        var document = new PdfDocument();
        AppendTo(document);
        return document;
    }

    /// <summary>Lays out and draws fixed blocks with the field values of this page.</summary>
    void DrawFixed(IDrawingSurface surface, IEnumerable<FixedBlock> blocks, FieldValues fields)
    {
        foreach (var block in blocks)
        {
            if (FormatFixed(block, fields) is not { } fragment)
                continue;
            if (block.Clip)
            {
                surface.SaveState();
                surface.Clip(block.Area);
            }
            if (block.IsArtifact)
                surface.BeginArtifact();
            fragment.Draw(surface, block.Area.X, block.Area.Y);
            if (block.IsArtifact)
                surface.EndArtifact();
            if (block.Clip)
                surface.RestoreState();
        }
    }

    /// <summary>
    /// Lays out a fixed block for one page: bound in width, unlimited in height, so its natural
    /// height is known. Returns null if the block has no content.
    /// </summary>
    IFragment? FormatFixed(FixedBlock block, FieldValues fields)
        => FormatFixed(block, fields, double.PositiveInfinity).Fragment;

    /// <summary>Lays out a fixed block for one page into the given height.</summary>
    FormatResult FormatFixed(FixedBlock block, FieldValues fields, double availableHeight)
        => block.Content.CreateFormatter(new LayoutEnvironment(_fonts, () => fields), block.Area.Width)
            .Format(availableHeight, atTopOfArea: true);

    /// <summary>Compares the height of all placed content with the height of its area.</summary>
    IReadOnlyList<LayoutOverflow> FindOverflows()
    {
        var overflows = new List<LayoutOverflow>();
        for (int i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            // The page breaking fills an area only as far as it fits, except at the top of a page,
            // where it must place something. Anything taller than the area is such a forced part.
            if (page.Content is { } content && page.Template.ContentArea is { } area
                && content.Height - area.Height > OverflowTolerance)
                overflows.Add(new LayoutOverflow(i, null, content.Height - area.Height));

            var fields = new FieldValues(i + 1, _pages.Count);
            foreach (var block in page.Template.FixedBlocks)
            {
                // Laid out into the height of its area, as the page breaking would do: spacing
                // after the last paragraph then shrinks to the free height and does not count.
                var fitted = FormatFixed(block, fields, block.Area.Height);
                bool fits = fitted.IsComplete && (fitted.Fragment?.Height ?? 0) - block.Area.Height <= OverflowTolerance;
                if (!fits && FormatFixed(block, fields) is { } full)
                    overflows.Add(new LayoutOverflow(i, block, Math.Max(full.Height - block.Area.Height, OverflowTolerance)));
            }
        }
        return overflows;
    }
}
