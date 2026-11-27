// A document for the layout engine: page templates and flowing content.
//
// Example: a letter. The first page has the address field and a content area starting below it;
// the following pages have a larger content area. The text flows from the first page onto as
// many following pages as needed.

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>Page templates plus content that flows through the content areas of the pages.</summary>
public sealed class LayoutDocument
{
    /// <summary>Safety limit against endless layouts (e.g. content that never fits).</summary>
    const int MaxPages = 10_000;

    /// <summary>The flowing content.</summary>
    readonly List<Block> _content = [];

    /// <summary>Creates a document.</summary>
    /// <param name="firstPage">Template of the first page.</param>
    /// <param name="followingPages">Template of all further pages; null uses the first page template.</param>
    public LayoutDocument(PageTemplate firstPage, PageTemplate? followingPages = null)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        FirstPage = firstPage;
        FollowingPages = followingPages ?? firstPage;
    }

    /// <summary>Template of the first page.</summary>
    public PageTemplate FirstPage { get; }

    /// <summary>Template of all further pages.</summary>
    public PageTemplate FollowingPages { get; }

    /// <summary>The flowing content.</summary>
    public IReadOnlyList<Block> Content => _content;

    /// <summary>Minimum number of pages, e.g. 2 to always print the following page template once.</summary>
    public int MinimumPageCount { get; set; } = 1;

    /// <summary>Appends a block to the flowing content.</summary>
    public LayoutDocument Add(Block block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _content.Add(block);
        return this;
    }

    /// <summary>
    /// Breaks the content into pages.
    /// </summary>
    /// <remarks>
    /// Page number fields need the total page count, which is only known after page breaking.
    /// So the layout runs once with an estimate, then again with the real count; if the count
    /// changes once more (rare), a third run follows.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The templates do not allow the content to be placed.</exception>
    public LaidOutDocument Layout(FontCollection fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        Validate();

        int estimate = FieldValues.Estimate.TotalPages;
        List<LaidOutPage> pages = [];
        for (int pass = 0; pass < 3; pass++)
        {
            pages = Paginate(fonts, estimate);
            if (pages.Count == estimate)
                break;
            estimate = pages.Count;
        }
        return new LaidOutDocument(fonts, pages);
    }

    /// <summary>Checks that the templates can take the content.</summary>
    void Validate()
    {
        if (_content.Count == 0)
            return;
        if (FirstPage.ContentArea is not { } first || FollowingPages.ContentArea is not { } following)
            throw new InvalidOperationException("Flowing content needs a content area on the first and the following pages.");
        // Blocks are formatted for one width; a paragraph continuing on the next page keeps its lines.
        if (Math.Abs(first.Width - following.Width) > 0.01)
            throw new InvalidOperationException("The content areas of first and following pages must have the same width.");
    }

    /// <summary>One layout pass with an assumed total page count.</summary>
    List<LaidOutPage> Paginate(FontCollection fonts, int totalPages)
    {
        var pages = new List<LaidOutPage>();
        var environment = new LayoutEnvironment(fonts, () => new FieldValues(pages.Count + 1, totalPages));
        double width = FirstPage.ContentArea?.Width ?? 0;
        var flow = new StackFormatter(_content, environment, width);

        bool complete = _content.Count == 0;
        while (!complete)
        {
            var template = pages.Count == 0 ? FirstPage : FollowingPages;
            var area = template.ContentArea!.Value;
            var result = flow.Format(area.Height, atTopOfArea: true);
            pages.Add(new LaidOutPage(template, result.Fragment));
            complete = result.IsComplete;
            if (pages.Count >= MaxPages)
                throw new InvalidOperationException($"The layout exceeds {MaxPages} pages.");
        }

        while (pages.Count < MinimumPageCount)
            pages.Add(new LaidOutPage(pages.Count == 0 ? FirstPage : FollowingPages, null));
        return pages;
    }
}
