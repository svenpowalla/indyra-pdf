// The fonts available to the layout engine, grouped into families.
//
// A family (e.g. "Liberation Sans") usually consists of four files: regular, bold, italic and
// bold italic. Text styles name a family and the flags bold/italic; this class picks the file.
//
// Fallback fonts take over characters the chosen font lacks, e.g. emoji: Liberation has no 😀,
// a fallback font such as Noto Color Emoji does. The choice is made per "grapheme cluster" – what
// a reader sees as one character, e.g. 👍🏽 (thumbs up + skin tone) or 🇩🇪 (two regional
// indicator letters) – so a sequence is never split between two fonts.

using IndyraPdf.Fonts;

namespace IndyraPdf.Layout;

/// <summary>A set of fonts, grouped by family name, from which text styles choose their font.</summary>
public sealed class FontCollection
{
    /// <summary>Variation selector 16: asks for the colorful emoji form of the preceding character.</summary>
    const int EmojiPresentation = 0xFE0F;

    /// <summary>Variation selector 15: asks for the plain text form of the preceding character.</summary>
    const int TextPresentation = 0xFE0E;

    /// <summary>All fonts in the order they were added.</summary>
    readonly List<TrueTypeFont> _fonts = [];

    /// <summary>Fallback fonts in the order they are tried.</summary>
    readonly List<TrueTypeFont> _fallbacks = [];

    /// <summary>Family used when a style names no family or an unknown one: the family added first.</summary>
    public string DefaultFamily => _fonts.Count > 0
        ? _fonts[0].FamilyName
        : throw new InvalidOperationException("The font collection is empty.");

    /// <summary>Names of all families.</summary>
    public IEnumerable<string> Families => _fonts.Select(f => f.FamilyName).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fallback fonts in the order they are tried.</summary>
    public IReadOnlyList<TrueTypeFont> Fallbacks => _fallbacks;

    /// <summary>Adds a font. Family, bold and italic are taken from the font file.</summary>
    /// <exception cref="ArgumentException">The font has no outlines (color emoji font); use <see cref="AddFallback(TrueTypeFont)"/>.</exception>
    public FontCollection Add(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!font.HasOutlines)
            throw new ArgumentException($"The font \"{font.FamilyName}\" has no outlines and can only be used as a fallback font (AddFallback).", nameof(font));
        _fonts.Add(font);
        return this;
    }

    /// <summary>Loads a font file and adds it.</summary>
    public FontCollection Add(string path) => Add(TrueTypeFont.Load(path));

    /// <summary>
    /// Adds a fallback font for characters the font of a text style lacks, e.g. Noto Color Emoji
    /// for emoji. Fallback fonts are tried in the order they were added. Color emoji fonts appear
    /// in color in PDF and preview; text in such a font can still be copied and searched.
    /// </summary>
    public FontCollection AddFallback(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        _fallbacks.Add(font);
        return this;
    }

    /// <summary>Loads a font file and adds it as fallback font.</summary>
    public FontCollection AddFallback(string path) => AddFallback(TrueTypeFont.Load(path));

    /// <summary>
    /// Chooses the font for one grapheme cluster (a character with everything attached to it).
    /// </summary>
    /// <remarks>
    /// Rules, in this order:
    /// 1. With variation selector 16 (U+FE0F, "show as emoji") a fallback that has the base
    ///    character wins, e.g. "©️" or "1️⃣" appear as emoji although Liberation has © and 1.
    /// 2. If the style font has every character of the cluster, it is used ("©", "1", "é").
    /// 3. Otherwise the first fallback that has the base character.
    /// 4. Otherwise the style font, which shows "?" for the missing character.
    /// The variation selectors themselves are ignored when checking (fonts rarely contain them).
    /// </remarks>
    internal TrueTypeFont ChooseFont(string cluster, TrueTypeFont styleFont)
    {
        if (_fallbacks.Count == 0)
            return styleFont;
        var codePoints = cluster.EnumerateRunes().Select(r => r.Value).ToList();
        int baseCharacter = codePoints[0];
        var relevant = codePoints.Where(c => c is not (EmojiPresentation or TextPresentation) && c >= 0x20).ToList();

        if (codePoints.Contains(EmojiPresentation) && FallbackWith(baseCharacter) is { } emoji)
            return emoji;
        if (relevant.All(styleFont.HasCharacter))
            return styleFont;
        return FallbackWith(baseCharacter) ?? styleFont;
    }

    /// <summary>The first fallback font that contains the character, or null.</summary>
    TrueTypeFont? FallbackWith(int codePoint) => _fallbacks.Find(f => f.HasCharacter(codePoint));

    /// <summary>
    /// Returns the font that best matches family and style. An unknown family falls back to the
    /// default family. If the family lacks the exact style, the closest one is used, with
    /// bold/regular weighing more than italic/upright.
    /// </summary>
    public TrueTypeFont Resolve(string? family, bool bold, bool italic)
    {
        var candidates = FontsOf(family);
        if (candidates.Count == 0)
            candidates = FontsOf(DefaultFamily);

        return candidates
            .OrderByDescending(f => (f.IsBold == bold ? 2 : 0) + (f.IsItalic == italic ? 1 : 0))
            .First();
    }

    /// <summary>Fonts of a family (case-insensitive).</summary>
    List<TrueTypeFont> FontsOf(string? family)
        => family is null ? [] : _fonts.Where(f => string.Equals(f.FamilyName, family, StringComparison.OrdinalIgnoreCase)).ToList();
}
