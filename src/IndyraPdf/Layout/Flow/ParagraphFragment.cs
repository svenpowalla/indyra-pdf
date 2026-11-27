// The lines of a paragraph that were placed in one area.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>A range of lines of a formatted paragraph.</summary>
internal sealed class ParagraphFragment : IFragment
{
    /// <summary>The formatted paragraph.</summary>
    readonly FormattedParagraph _paragraph;

    /// <summary>First line of the range.</summary>
    readonly int _firstLine;

    /// <summary>Number of lines.</summary>
    readonly int _lineCount;

    /// <summary>Space above the first line of the range.</summary>
    readonly double _spaceBefore;

    /// <summary>Creates the fragment.</summary>
    public ParagraphFragment(FormattedParagraph paragraph, int firstLine, int lineCount, double spaceBefore, double height)
    {
        _paragraph = paragraph;
        _firstLine = firstLine;
        _lineCount = lineCount;
        _spaceBefore = spaceBefore;
        Height = height;
    }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void Draw(IDrawingSurface surface, double x, double y)
        => _paragraph.DrawLines(surface, x, y + _spaceBefore, _firstLine, _lineCount);
}
