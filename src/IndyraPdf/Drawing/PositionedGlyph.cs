// One glyph of a GlyphRun with its exact horizontal position.

namespace IndyraPdf.Drawing;

/// <summary>
/// A glyph placed at a horizontal position within a <see cref="GlyphRun"/>.
/// </summary>
/// <param name="GlyphId">Glyph number in the font.</param>
/// <param name="Text">The text this glyph represents (for copying and searching).</param>
/// <param name="X">Position of the glyph origin, relative to the start of the run, in points.</param>
public readonly record struct PositionedGlyph(int GlyphId, string Text, double X);
