// Converts text into glyphs ("shaping").
//
// One character becomes one glyph via the font's character map. With kerning, the advance of
// each glyph is adjusted by the font's pair value for the following glyph (e.g. "AV" closer
// together). Because the adjusted advances are used for line breaking and drawing alike, the
// PDF shows exactly the measured positions (written as TJ adjustments).
//
// Ligatures (e.g. "fi") are not applied to text fonts: the Liberation fonts only define
// discretionary ligatures ("dlig"), which browsers do not use by default either.
//
// Fallback fonts for emoji are different: there a sequence of characters must become one picture
// (👍 + 🏽 → thumbs up in medium skin tone). For them the "ccmp" ligatures of the font are
// applied. The resulting glyph keeps the text of all characters it replaces, so copying the
// PDF text yields the original sequence again.

using System.Text;
using IndyraPdf.Fonts;

namespace IndyraPdf.Layout.Text;

/// <summary>Converts text into glyphs with advance widths.</summary>
internal static class TextShaper
{
    /// <summary>Character shown instead of characters the font does not contain.</summary>
    const char ReplacementCharacter = '?';

    /// <summary>
    /// Shapes text in one font and size. Control characters are skipped; tabs count as spaces;
    /// characters missing in the font become "?".
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="kerning">Whether to apply the font's kerning between neighbouring glyphs.</param>
    /// <param name="ligatures">
    /// Whether to apply the font's composition ligatures (for emoji fallback fonts). Variation
    /// selectors and characters missing in the font then become part of the preceding glyph's
    /// text instead of being replaced by "?".
    /// </param>
    public static List<ShapedGlyph> Shape(string text, TrueTypeFont font, double size, bool kerning = true, bool ligatures = false)
    {
        var units = ligatures ? ComposeLigatures(text, font) : MapCharacters(text, font);
        var glyphs = new List<ShapedGlyph>(units.Count);
        double scale = size / font.UnitsPerEm;
        foreach (var (glyph, glyphText) in units)
        {
            if (kerning && glyphs.Count > 0)
            {
                var previous = glyphs[^1];
                int adjustment = font.GetKerning(previous.GlyphId, glyph);
                if (adjustment != 0)
                    glyphs[^1] = previous with { Advance = previous.Advance + adjustment * scale };
            }
            glyphs.Add(new ShapedGlyph(glyph, glyphText, font.GetAdvanceWidth(glyph) * scale));
        }
        return glyphs;
    }

    /// <summary>One glyph per character; missing characters become "?".</summary>
    static List<(int Glyph, string Text)> MapCharacters(string text, TrueTypeFont font)
    {
        var units = new List<(int, string)>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            int codePoint = rune.Value == '\t' ? ' ' : rune.Value;
            if (codePoint < 0x20)
                continue;
            int glyph = font.GetGlyphId(codePoint);
            string glyphText = rune.Value == '\t' ? " " : rune.ToString();
            if (glyph == 0)
            {
                glyph = font.GetGlyphId(ReplacementCharacter);
                glyphText = ReplacementCharacter.ToString();
            }
            units.Add((glyph, glyphText));
        }
        return units;
    }

    /// <summary>
    /// Maps the characters to glyphs and combines sequences through the font's ligatures.
    /// Characters without a glyph (variation selectors, joiners the font lacks) stay in the text
    /// of the preceding glyph.
    /// </summary>
    static List<(int Glyph, string Text)> ComposeLigatures(string text, TrueTypeFont font)
    {
        var glyphIds = new List<int>(text.Length);
        var texts = new List<StringBuilder>(text.Length);
        var leading = new StringBuilder(); // characters before the first glyph
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value < 0x20)
                continue;
            int glyph = font.GetGlyphId(rune.Value);
            if (glyph == 0)
            {
                (texts.Count > 0 ? texts[^1] : leading).Append(rune.ToString());
                continue;
            }
            glyphIds.Add(glyph);
            texts.Add(new StringBuilder(texts.Count == 0 ? leading.ToString() : "").Append(rune.ToString()));
        }

        var units = new List<(int, string)>(glyphIds.Count);
        int source = 0;
        foreach (var (glyph, count) in font.Ligatures.Apply(glyphIds))
        {
            var combined = new StringBuilder();
            for (int k = source; k < source + count; k++)
                combined.Append(texts[k]);
            units.Add((glyph, combined.ToString()));
            source += count;
        }
        return units;
    }
}
