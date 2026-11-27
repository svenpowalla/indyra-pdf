// One line of a formatted paragraph with its metrics.
//
// Vertical metrics follow the CSS model used by browsers and therefore by web-based text editors:
// the content height is ascender − descender of the largest font in the line; the remaining
// space up to the line height ("leading") is split equally above and below.

namespace IndyraPdf.Layout.Text;

/// <summary>A line of text: segments, widths and vertical metrics.</summary>
internal sealed class TextLine
{
    /// <summary>Creates a line.</summary>
    /// <param name="segments">Segments in order; trailing spaces included.</param>
    /// <param name="indent">Extra indent of this line (first line indent), in points.</param>
    /// <param name="availableWidth">Width available for this line.</param>
    /// <param name="justifiable">Whether the line may be stretched for justified text.</param>
    /// <param name="emptyLineSegment">Metrics source for a line without segments.</param>
    /// <param name="lineSpacing">Line spacing factor of the paragraph.</param>
    public TextLine(IReadOnlyList<LineSegment> segments, double indent, double availableWidth,
                    bool justifiable, LineSegment emptyLineSegment, double lineSpacing)
    {
        Segments = segments;
        Indent = indent;
        AvailableWidth = availableWidth;
        Justifiable = justifiable;

        // Trailing spaces do not count for the width (they would shift right-aligned text).
        int last = segments.Count - 1;
        while (last >= 0 && segments[last].IsSpace)
            last--;
        VisibleSegmentCount = last + 1;
        ContentWidth = last >= 0 ? segments[last].X + segments[last].Width : 0;
        InnerSpaceCount = segments.Take(VisibleSegmentCount).Where(s => s.IsSpace).Sum(s => s.Glyphs.Count);

        var metricSources = segments.Count > 0 ? segments : [emptyLineSegment];
        Ascent = metricSources.Max(s => s.Ascent);
        Descent = metricSources.Min(s => s.Descent);
        double gap = metricSources.Max(s => s.LineGap);
        Height = (Ascent - Descent + gap) * lineSpacing;
    }

    /// <summary>All segments, including trailing spaces.</summary>
    public IReadOnlyList<LineSegment> Segments { get; }

    /// <summary>Number of segments up to the last visible one (trailing spaces excluded).</summary>
    public int VisibleSegmentCount { get; }

    /// <summary>Extra indent of this line (first line indent).</summary>
    public double Indent { get; }

    /// <summary>Width available for this line.</summary>
    public double AvailableWidth { get; }

    /// <summary>Width of the visible content.</summary>
    public double ContentWidth { get; }

    /// <summary>Number of space glyphs between visible content (widened for justified text).</summary>
    public int InnerSpaceCount { get; }

    /// <summary>Whether the line may be stretched for justified text (not the last line, no forced break).</summary>
    public bool Justifiable { get; }

    /// <summary>Largest ascender in the line.</summary>
    public double Ascent { get; }

    /// <summary>Lowest descender in the line (negative).</summary>
    public double Descent { get; }

    /// <summary>Height of the line including line spacing.</summary>
    public double Height { get; }

    /// <summary>Distance from the top of the line to the baseline: half the leading plus the ascender.</summary>
    public double BaselineOffset => (Height - (Ascent - Descent)) / 2 + Ascent;
}
