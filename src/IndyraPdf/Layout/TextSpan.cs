// A piece of text with one character format, part of a paragraph.

namespace IndyraPdf.Layout;

/// <summary>A piece of text with one character format, or a page number field.</summary>
public sealed record TextSpan
{
    /// <summary>Creates a text span.</summary>
    public TextSpan(string text, TextStyle style) : this(SpanKind.Text, text, style) { }

    /// <summary>Creates a span of the given kind.</summary>
    TextSpan(SpanKind kind, string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        Kind = kind;
        Text = text;
        Style = style;
    }

    /// <summary>Field showing the number of the current page.</summary>
    public static TextSpan PageNumber(TextStyle style) => new(SpanKind.PageNumber, "", style);

    /// <summary>Field showing the total number of pages.</summary>
    public static TextSpan TotalPages(TextStyle style) => new(SpanKind.TotalPages, "", style);

    /// <summary>What the span contains.</summary>
    public SpanKind Kind { get; }

    /// <summary>The text (empty for fields). "\n" forces a line break within the paragraph.</summary>
    public string Text { get; }

    /// <summary>Character format.</summary>
    public TextStyle Style { get; }

    /// <summary>The text to set, with fields replaced by their values.</summary>
    internal string Resolve(FieldValues values) => Kind switch
    {
        SpanKind.PageNumber => values.PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SpanKind.TotalPages => values.TotalPages.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => Text,
    };
}
