// A paragraph: text spans with paragraph formatting (alignment, indents, spacing, list marker).

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>A paragraph of text spans with paragraph formatting.</summary>
public sealed class Paragraph : Block
{
    /// <summary>The spans in reading order.</summary>
    readonly List<TextSpan> _spans = [];

    /// <summary>Creates an empty paragraph.</summary>
    /// <param name="style">
    /// Default character format for <see cref="Add(string)"/>; also determines the height of an
    /// empty paragraph.
    /// </param>
    public Paragraph(TextStyle? style = null) => Style = style ?? TextStyle.Default;

    /// <summary>Default character format of the paragraph.</summary>
    public TextStyle Style { get; }

    /// <summary>The spans in reading order.</summary>
    public IReadOnlyList<TextSpan> Spans => _spans;

    /// <summary>Horizontal alignment.</summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    /// <summary>Distance from the left edge of the available area to all lines, in points.</summary>
    public double LeftIndent { get; set; }

    /// <summary>Distance from the right edge of the available area to all lines, in points.</summary>
    public double RightIndent { get; set; }

    /// <summary>Additional indent of the first line in points (negative for a hanging indent).</summary>
    public double FirstLineIndent { get; set; }

    /// <summary>Space above the paragraph in points.</summary>
    public double SpaceBefore { get; set; }

    /// <summary>Space below the paragraph in points.</summary>
    public double SpaceAfter { get; set; }

    /// <summary>Line height as multiple of the font's natural line height (1 = single spacing).</summary>
    public double LineSpacing { get; set; } = 1;

    /// <summary>
    /// List marker such as "•" or "3.", drawn in the paragraph's default style on the first line,
    /// in a column of <see cref="MarkerWidth"/> left of the text. Null for no marker.
    /// </summary>
    /// <remarks>
    /// The text keeps its position; set <see cref="LeftIndent"/> so the marker column has room,
    /// e.g. LeftIndent = 22 and MarkerWidth = 16 puts the marker 6 pt from the left edge.
    /// </remarks>
    public string? Marker { get; set; }

    /// <summary>Width of the marker column in points.</summary>
    public double MarkerWidth { get; set; } = 16;

    /// <summary>
    /// Role in tagged PDFs: paragraph (default) or heading level 1–6. Paragraphs with a
    /// <see cref="Marker"/> are tagged as list items instead; put them into a <see cref="ListBlock"/>.
    /// </summary>
    public Drawing.StructureRole Role { get; set; } = Drawing.StructureRole.Paragraph;

    /// <summary>
    /// Prevents a single line of the paragraph at the bottom ("orphan") or top ("widow") of a
    /// page: when the paragraph is split, every part keeps at least two lines. On by default,
    /// as in MigraDoc and word processors.
    /// </summary>
    public bool WidowControl { get; set; } = true;

    /// <summary>Appends text in the paragraph's default format.</summary>
    public Paragraph Add(string text) => Add(new TextSpan(text, Style));

    /// <summary>Appends text in the given format.</summary>
    public Paragraph Add(string text, TextStyle style) => Add(new TextSpan(text, style));

    /// <summary>Appends a span (text or field).</summary>
    public Paragraph Add(TextSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        _spans.Add(span);
        return this;
    }

    /// <summary>
    /// Breaks the paragraph into lines for the given width.
    /// </summary>
    /// <param name="fonts">Fonts to choose from.</param>
    /// <param name="width">Available width in points, including the indents.</param>
    /// <param name="fields">Values for page number fields; use <see cref="FieldValues.Estimate"/> while unknown.</param>
    public FormattedParagraph Format(FontCollection fonts, double width, FieldValues fields)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        var lines = new Text.LineBreaker(this, fonts, fields).Break(width - LeftIndent - RightIndent);
        var marker = Marker is null ? null : new Text.ShapedMarker(Marker, fonts.Resolve(Style.FontFamily, Style.Bold, Style.Italic), Style);
        return new FormattedParagraph(this, lines, width, marker);
    }

    /// <inheritdoc/>
    internal override IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width)
        => new ParagraphFormatter(this, environment, width);
}
