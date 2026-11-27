// Ligature substitution: several glyphs in a row are replaced by one glyph.
//
// Emoji fonts use this to build one picture from a sequence of characters, e.g.
//   👍 + 🏽 (skin tone modifier)          → thumbs up in medium skin tone
//   🇩 + 🇪 (two regional indicators)     → flag of Germany
//   👨 + ZWJ + 👩 + ZWJ + 👧               → family (ZWJ = zero width joiner U+200D)
// The rules come from the GSUB table, lookup type 4 (see LigatureReader).

namespace IndyraPdf.Fonts.Ligatures;

/// <summary>The ligature rules of a font, grouped into lookups that are applied one after another.</summary>
internal sealed class LigatureTable
{
    /// <summary>
    /// Per lookup: first glyph → candidate ligatures in the order of the font. The font orders
    /// them by preference (usually longest first); the first one that matches wins.
    /// </summary>
    readonly IReadOnlyList<IReadOnlyDictionary<int, IReadOnlyList<Ligature>>> _lookups;

    /// <summary>Creates the table.</summary>
    public LigatureTable(IReadOnlyList<IReadOnlyDictionary<int, IReadOnlyList<Ligature>>> lookups) => _lookups = lookups;

    /// <summary>A table without rules (fonts without ligatures).</summary>
    public static LigatureTable Empty { get; } = new([]);

    /// <summary>Whether the font defines any ligature.</summary>
    public bool IsEmpty => _lookups.Count == 0;

    /// <summary>
    /// Applies all lookups to a glyph sequence. Returns the resulting glyphs, each with the
    /// number of input glyphs it stands for (1 for an unchanged glyph), so the caller can assign
    /// the original text to the new glyph.
    /// </summary>
    public IReadOnlyList<(int GlyphId, int SourceCount)> Apply(IReadOnlyList<int> glyphs)
    {
        var current = glyphs.Select(g => (GlyphId: g, SourceCount: 1)).ToList();
        foreach (var lookup in _lookups)
        {
            var next = new List<(int GlyphId, int SourceCount)>(current.Count);
            int i = 0;
            while (i < current.Count)
            {
                var match = Match(lookup, current, i);
                if (match is { } ligature)
                {
                    int length = ligature.Components.Count + 1;
                    int sources = 0;
                    for (int k = i; k < i + length; k++)
                        sources += current[k].SourceCount;
                    next.Add((ligature.GlyphId, sources));
                    i += length;
                }
                else
                {
                    next.Add(current[i++]);
                }
            }
            current = next;
        }
        return current;
    }

    /// <summary>Returns the first ligature of the lookup that matches at position <paramref name="start"/>.</summary>
    static Ligature? Match(IReadOnlyDictionary<int, IReadOnlyList<Ligature>> lookup, List<(int GlyphId, int SourceCount)> glyphs, int start)
    {
        if (!lookup.TryGetValue(glyphs[start].GlyphId, out var candidates))
            return null;
        foreach (var ligature in candidates)
        {
            var components = ligature.Components;
            if (start + components.Count >= glyphs.Count)
                continue; // not enough glyphs left for this ligature
            bool matches = true;
            for (int k = 0; k < components.Count && matches; k++)
                matches = glyphs[start + 1 + k].GlyphId == components[k];
            if (matches)
                return ligature;
        }
        return null;
    }
}
