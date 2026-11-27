// A glyph after shaping: which glyph, what text it stands for, how wide it is.

namespace IndyraPdf.Layout.Text;

/// <summary>A glyph chosen for a character, with its advance width at the font size used.</summary>
/// <param name="GlyphId">Glyph number in the font.</param>
/// <param name="Text">The text the glyph represents.</param>
/// <param name="Advance">Advance width in points.</param>
internal readonly record struct ShapedGlyph(int GlyphId, string Text, double Advance);
