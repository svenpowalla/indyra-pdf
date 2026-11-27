// One page of a PDF document: its size and its drawing surface.

using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>A page in a <see cref="PdfDocument"/>.</summary>
public sealed class PdfPage
{
    /// <summary>Creates the page. Only <see cref="PdfDocument.AddPage(PageSize)"/> calls this.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is not positive.</exception>
    internal PdfPage(PageSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Page width and height must be positive.");
        Size = size;
        Canvas = new PdfCanvas(size.Height);
    }

    /// <summary>Page size in points.</summary>
    public PageSize Size { get; }

    /// <summary>Width in points.</summary>
    public double Width => Size.Width;

    /// <summary>Height in points.</summary>
    public double Height => Size.Height;

    /// <summary>Drawing surface of the page.</summary>
    public PdfCanvas Canvas { get; }

    /// <summary>
    /// Creates the page object (ISO 32000-2, 7.7.3.3) with its content stream:
    /// &lt;&lt;/Type/Page/Parent …/MediaBox[0 0 w h]/Resources …/Contents …&gt;&gt;.
    /// The media box is the visible page area in points.
    /// </summary>
    internal PdfDictionary Build(PdfObjectTable table, PdfDictionary pageTree, ResourceObjects resources, bool compress)
    {
        var content = table.Add(new PdfStream(Canvas.GetContent()) { Compress = compress });

        var page = table.Add(new PdfDictionary("Page"));
        page["Parent"] = pageTree;
        page["MediaBox"] = new PdfArray(0, 0, Width, Height);
        page["Resources"] = Canvas.Resources.Build(resources);
        page["Contents"] = content;
        return page;
    }
}
