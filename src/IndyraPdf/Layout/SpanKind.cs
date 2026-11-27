// Kinds of text spans: fixed text or values known only after page breaking.

namespace IndyraPdf.Layout;

/// <summary>What a <see cref="TextSpan"/> contains.</summary>
public enum SpanKind
{
    /// <summary>Fixed text.</summary>
    Text,

    /// <summary>Number of the current page, inserted while drawing.</summary>
    PageNumber,

    /// <summary>Total number of pages, inserted while drawing.</summary>
    TotalPages,
}
