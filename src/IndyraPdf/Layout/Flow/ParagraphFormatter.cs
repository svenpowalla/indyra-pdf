// Places a paragraph into areas of limited height, splitting it between lines.
//
// The paragraph is broken into lines when it is placed for the first time, so page number
// fields get the number of the page on which the paragraph starts. Space before is only added
// above the first line, space after only below the last line; at a page break it is dropped.
//
// Widow control (as in MigraDoc's WidowControl): when the paragraph is split, every part keeps
// at least two lines. If that is impossible in the current area, the paragraph moves on.

namespace IndyraPdf.Layout.Flow;

/// <summary>Formatter for <see cref="Paragraph"/>: splits between lines.</summary>
internal sealed class ParagraphFormatter : IBlockFormatter
{
    /// <summary>Minimum number of lines of a split paragraph per area when widow control is on.</summary>
    const int MinimumLinesPerPart = 2;

    /// <summary>The paragraph.</summary>
    readonly Paragraph _paragraph;

    /// <summary>Fonts and field values.</summary>
    readonly LayoutEnvironment _environment;

    /// <summary>Available width.</summary>
    readonly double _width;

    /// <summary>The paragraph broken into lines, created on the first call.</summary>
    FormattedParagraph? _formatted;

    /// <summary>First line not placed yet.</summary>
    int _nextLine;

    /// <summary>Creates the formatter.</summary>
    public ParagraphFormatter(Paragraph paragraph, LayoutEnvironment environment, double width)
    {
        _paragraph = paragraph;
        _environment = environment;
        _width = width;
    }

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        _formatted ??= _paragraph.Format(_environment.Fonts, _width, _environment.CurrentFields);

        double spaceBefore = _nextLine == 0 ? _formatted.SpaceBefore : 0;
        int remaining = _formatted.LineCount - _nextLine;
        int count = CountFittingLines(availableHeight - spaceBefore);

        if (_paragraph.WidowControl && count < remaining)
            count = ApplyWidowControl(count, remaining);

        if (count == 0)
        {
            if (!atTopOfArea)
                return FormatResult.NothingFits;
            // At the top of an area something must be placed: as many lines as fit, at least one.
            count = Math.Max(1, CountFittingLines(availableHeight - spaceBefore));
        }

        int first = _nextLine;
        double height = spaceBefore;
        for (int i = 0; i < count; i++)
            height += _formatted.GetLineHeight(_nextLine++);

        bool complete = _nextLine == _formatted.LineCount;
        if (complete)
            height += Math.Clamp(availableHeight - height, 0, _formatted.SpaceAfter);
        return new FormatResult(new ParagraphFragment(_formatted, first, count, spaceBefore, height), complete);
    }

    /// <summary>Number of lines from <see cref="_nextLine"/> on that fit into the given height.</summary>
    int CountFittingLines(double height)
    {
        int count = 0;
        double used = 0;
        while (_nextLine + count < _formatted!.LineCount && used + _formatted.GetLineHeight(_nextLine + count) <= height + 1e-6)
            used += _formatted.GetLineHeight(_nextLine + count++);
        return count;
    }

    /// <summary>
    /// Reduces the number of lines of a split so that both this part and the rest keep at least
    /// two lines. Returns 0 if the paragraph should move on as a whole.
    /// </summary>
    static int ApplyWidowControl(int count, int remaining)
    {
        if (remaining - count < MinimumLinesPerPart)
            count = remaining - MinimumLinesPerPart;  // leave two lines for the next area
        return count < MinimumLinesPerPart ? 0 : count;
    }
}
