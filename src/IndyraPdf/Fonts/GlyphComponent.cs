// One component of a composite glyph, as returned by CompositeGlyph.GetComponents.

namespace IndyraPdf.Fonts;

/// <summary>One component of a composite glyph.</summary>
/// <param name="Offset">Byte position of the glyph reference inside the glyph data (to rewrite it).</param>
/// <param name="GlyphId">Glyph number the component refers to.</param>
internal readonly record struct GlyphComponent(int Offset, int GlyphId);
