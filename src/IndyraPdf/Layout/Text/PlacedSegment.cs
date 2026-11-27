// A line segment with its final position on the page, computed just before drawing.

namespace IndyraPdf.Layout.Text;

/// <summary>A segment with its final glyph positions (after alignment and justification).</summary>
/// <param name="Segment">The segment.</param>
/// <param name="Positions">Absolute x position of every glyph.</param>
/// <param name="Start">Absolute x position of the segment.</param>
/// <param name="Width">Width including widened spaces.</param>
internal sealed record PlacedSegment(LineSegment Segment, double[] Positions, double Start, double Width);
