// One glyph as written into a TJ array of the page content.

namespace IndyraPdf.Writer;

/// <summary>The code of a glyph in the page content, its position and its natural advance.</summary>
/// <param name="Hex">The code in hexadecimal digits (four for CID fonts, two for Type3 fonts).</param>
/// <param name="X">Position of the glyph origin relative to the start of the run, in points.</param>
/// <param name="Advance">Width the font gives the glyph at the run's size, in points.</param>
internal readonly record struct GlyphCode(string Hex, double X, double Advance);
