// Layout of a page: size, area for the flowing content and fixed blocks.

using IndyraPdf.Drawing;
using IndyraPdf.Writer;

namespace IndyraPdf.Layout;

/// <summary>
/// Template for pages: page size, the area the flowing content runs through, and blocks at fixed
/// positions. A <see cref="LayoutDocument"/> uses one template for the first page and one for the
/// following pages.
/// </summary>
public sealed class PageTemplate
{
    /// <summary>Fixed blocks.</summary>
    readonly List<FixedBlock> _fixedBlocks = [];

    /// <summary>Creates a template.</summary>
    /// <param name="size">Page size.</param>
    /// <param name="contentArea">Area for the flowing content, or null if this page takes no flowing content.</param>
    public PageTemplate(PageSize size, Box? contentArea)
    {
        Size = size;
        ContentArea = contentArea;
    }

    /// <summary>Page size.</summary>
    public PageSize Size { get; }

    /// <summary>Area for the flowing content, or null.</summary>
    public Box? ContentArea { get; }

    /// <summary>Blocks at fixed positions.</summary>
    public IReadOnlyList<FixedBlock> FixedBlocks => _fixedBlocks;

    /// <summary>Adds a block at a fixed position.</summary>
    public PageTemplate AddFixed(FixedBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _fixedBlocks.Add(block);
        return this;
    }
}
