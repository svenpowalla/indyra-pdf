// A paragraph after line breaking: knows its height and draws itself onto a drawing surface.
//
// Drawing order per line: backgrounds (highlighting), glyphs, then underline and strikeout.
// Consecutive segments with equal font, size and color are drawn as one glyph run, so a normal
// line becomes a single text operation in the PDF.

using IndyraPdf.Drawing;
using IndyraPdf.Layout.Text;

namespace IndyraPdf.Layout;

/// <summary>A paragraph broken into lines for a given width.</summary>
public sealed class FormattedParagraph
{
    /// <summary>The paragraph (for indents, alignment and spacing).</summary>
    readonly Paragraph _paragraph;

    /// <summary>The lines.</summary>
    readonly IReadOnlyList<TextLine> _lines;

    /// <summary>The list marker, or null.</summary>
    readonly ShapedMarker? _marker;

    /// <summary>Creates the result. Only <see cref="Paragraph.Format"/> calls this.</summary>
    internal FormattedParagraph(Paragraph paragraph, IReadOnlyList<TextLine> lines, double width, ShapedMarker? marker)
    {
        _paragraph = paragraph;
        _lines = lines;
        _marker = marker;
        Width = width;
    }

    /// <summary>Space above the first line.</summary>
    internal double SpaceBefore => _paragraph.SpaceBefore;

    /// <summary>Space below the last line.</summary>
    internal double SpaceAfter => _paragraph.SpaceAfter;

    /// <summary>Width the paragraph was formatted for, including indents.</summary>
    public double Width { get; }

    /// <summary>Number of lines.</summary>
    public int LineCount => _lines.Count;

    /// <summary>Total height: space before, all lines, space after.</summary>
    public double Height => _paragraph.SpaceBefore + _lines.Sum(l => l.Height) + _paragraph.SpaceAfter;

    /// <summary>Height of one line.</summary>
    public double GetLineHeight(int line) => _lines[line].Height;

    /// <summary>Draws the whole paragraph with its upper left corner at (x, y).</summary>
    public void Draw(IDrawingSurface surface, double x, double y)
        => DrawLines(surface, x, y + _paragraph.SpaceBefore, 0, _lines.Count);

    /// <summary>
    /// Draws a range of lines, the first one with its top at <paramref name="top"/>. Used when a
    /// paragraph is split across pages.
    /// </summary>
    internal void DrawLines(IDrawingSurface surface, double x, double top, int firstLine, int count)
    {
        ArgumentNullException.ThrowIfNull(surface);

        // Structure for tagged PDF: a paragraph or heading, or – with a marker – a list item
        // consisting of label (the marker) and body (the text). The paragraph object is the key,
        // so the parts of a paragraph split across pages form one element.
        if (_marker is null)
        {
            surface.BeginTag(new StructureTag(_paragraph.Role, _paragraph), top);
            DrawLineRange(surface, x, top, firstLine, count);
            surface.EndTag();
            return;
        }

        surface.BeginTag(new StructureTag(StructureRole.ListItem, _paragraph), top);
        if (firstLine == 0)
        {
            surface.BeginTag(new StructureTag(StructureRole.ListLabel, (_paragraph, StructureRole.ListLabel)), top);
            DrawMarker(surface, _lines[0], x, top);
            surface.EndTag();
        }
        surface.BeginTag(new StructureTag(StructureRole.ListBody, (_paragraph, StructureRole.ListBody)), top);
        DrawLineRange(surface, x, top, firstLine, count);
        surface.EndTag();
        surface.EndTag();
    }

    /// <summary>Draws a range of lines one below the other.</summary>
    void DrawLineRange(IDrawingSurface surface, double x, double top, int firstLine, int count)
    {
        double lineTop = top;
        for (int i = firstLine; i < firstLine + count; i++)
        {
            DrawLine(surface, _lines[i], x, lineTop);
            lineTop += _lines[i].Height;
        }
    }

    /// <summary>Draws the list marker left of the first line, on the same baseline.</summary>
    void DrawMarker(IDrawingSurface surface, TextLine firstLine, double x, double top)
    {
        if (_marker is null || _marker.Glyphs.Count == 0)
            return;
        double markerX = x + _paragraph.LeftIndent + _paragraph.FirstLineIndent - _paragraph.MarkerWidth;
        var glyphs = new List<PositionedGlyph>();
        double offset = 0;
        foreach (var glyph in _marker.Glyphs)
        {
            glyphs.Add(new PositionedGlyph(glyph.GlyphId, glyph.Text, offset));
            offset += glyph.Advance;
        }
        surface.DrawGlyphs(new GlyphRun(_marker.Font, _marker.Style.FontSize, _marker.Style.Color,
                                        markerX, top + firstLine.BaselineOffset, glyphs));
    }

    /// <summary>Draws one line.</summary>
    void DrawLine(IDrawingSurface surface, TextLine line, double x, double top)
    {
        double baseline = top + line.BaselineOffset;
        double free = line.AvailableWidth - line.ContentWidth;
        double start = x + _paragraph.LeftIndent + line.Indent + _paragraph.Alignment switch
        {
            TextAlignment.Center => free / 2,
            TextAlignment.Right => free,
            _ => 0,
        };
        bool stretch = _paragraph.Alignment == TextAlignment.Justify && line.Justifiable && line.InnerSpaceCount > 0;
        double extraPerSpace = stretch ? free / line.InnerSpaceCount : 0;

        var placed = PlaceSegments(line, start, extraPerSpace);
        foreach (var segment in placed)
            DrawBackground(surface, segment, baseline);
        foreach (var run in GroupIntoRuns(placed, baseline))
            surface.DrawGlyphs(run);
        foreach (var segment in placed)
            DrawDecorations(surface, segment, baseline);
    }

    /// <summary>
    /// Computes the final position of every visible segment and glyph. For justified text every
    /// space glyph gets <paramref name="extraPerSpace"/> additional width; trailing spaces are dropped.
    /// </summary>
    static List<PlacedSegment> PlaceSegments(TextLine line, double start, double extraPerSpace)
    {
        var result = new List<PlacedSegment>();
        double shift = 0; // accumulated widening of the spaces so far
        for (int s = 0; s < line.VisibleSegmentCount; s++)
        {
            var segment = line.Segments[s];
            var positions = new double[segment.Glyphs.Count];
            double x = start + segment.X + shift;
            double segmentStart = x;
            for (int g = 0; g < segment.Glyphs.Count; g++)
            {
                positions[g] = x;
                x += segment.Glyphs[g].Advance;
                if (segment.IsSpace)
                {
                    x += extraPerSpace;
                    shift += extraPerSpace;
                }
            }
            result.Add(new PlacedSegment(segment, positions, segmentStart, x - segmentStart));
        }
        return result;
    }

    /// <summary>Combines neighbouring segments with equal font, size and color into glyph runs.</summary>
    static IEnumerable<GlyphRun> GroupIntoRuns(List<PlacedSegment> placed, double baseline)
    {
        int i = 0;
        while (i < placed.Count)
        {
            var first = placed[i].Segment;
            double runX = placed[i].Positions.Length > 0 ? placed[i].Positions[0] : placed[i].Start;
            var glyphs = new List<PositionedGlyph>();
            int j = i;
            while (j < placed.Count && SameRun(first, placed[j].Segment))
            {
                var segment = placed[j];
                for (int g = 0; g < segment.Segment.Glyphs.Count; g++)
                {
                    var shaped = segment.Segment.Glyphs[g];
                    glyphs.Add(new PositionedGlyph(shaped.GlyphId, shaped.Text, segment.Positions[g] - runX));
                }
                j++;
            }
            yield return new GlyphRun(first.Font, first.Style.FontSize, first.Style.Color, runX, baseline, glyphs);
            i = j;
        }
    }

    /// <summary>Whether two segments can be drawn as one glyph run.</summary>
    static bool SameRun(LineSegment a, LineSegment b)
        => ReferenceEquals(a.Font, b.Font) && a.Style.FontSize == b.Style.FontSize && a.Style.Color == b.Style.Color;

    /// <summary>Fills the highlight color behind a segment, from ascender to descender.</summary>
    static void DrawBackground(IDrawingSurface surface, PlacedSegment placed, double baseline)
    {
        if (placed.Segment.Style.Background is not { } color)
            return;
        var segment = placed.Segment;
        surface.FillRectangle(new Box(placed.Start, baseline - segment.Ascent, placed.Width, segment.Ascent - segment.Descent), color);
    }

    /// <summary>
    /// Draws underline and strikeout as thin filled rectangles in the text color. The font states
    /// the position of the top edge of each line relative to the baseline (OpenType "post" and
    /// "OS/2" tables).
    /// </summary>
    static void DrawDecorations(IDrawingSurface surface, PlacedSegment placed, double baseline)
    {
        var style = placed.Segment.Style;
        // The style font, also below emoji of a fallback font, so the line runs through evenly.
        var font = placed.Segment.MetricsFont;
        double scale = style.FontSize / font.UnitsPerEm;
        if (style.Underline)
            DrawDecorationLine(surface, placed, baseline - font.UnderlinePosition * scale, font.UnderlineThickness * scale);
        if (style.Strikethrough)
            DrawDecorationLine(surface, placed, baseline - font.StrikeoutPosition * scale, font.StrikeoutThickness * scale);
    }

    /// <summary>Draws one decoration line whose top edge is at <paramref name="top"/>.</summary>
    static void DrawDecorationLine(IDrawingSurface surface, PlacedSegment placed, double top, double thickness)
        => surface.FillRectangle(new Box(placed.Start, top, placed.Width, thickness), placed.Segment.Style.Color);
}
