// One ligature rule: first glyph + further components → replacement glyph.

namespace IndyraPdf.Fonts.Ligatures;

/// <summary>
/// A ligature rule. The first glyph is the key under which the rule is stored in
/// <see cref="LigatureTable"/>; <see cref="Components"/> are the glyphs that must follow it.
/// </summary>
/// <param name="GlyphId">The glyph that replaces the whole sequence.</param>
/// <param name="Components">The glyphs following the first glyph, in order.</param>
internal sealed record Ligature(int GlyphId, IReadOnlyList<int> Components);
