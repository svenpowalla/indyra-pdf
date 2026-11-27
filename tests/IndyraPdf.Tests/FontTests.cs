// Tests for reading TrueType fonts and building subset fonts.

using IndyraPdf.Fonts;

namespace IndyraPdf.Tests;

public class FontTests
{
    static readonly TrueTypeFont Sans = TrueTypeFont.Load(TestAssets.SansRegular);

    [Fact]
    public void Load_ReadsNamesAndMetrics()
    {
        Assert.Equal("Liberation Sans", Sans.FamilyName);
        Assert.Equal("LiberationSans", Sans.PostScriptName);
        Assert.Equal(2048, Sans.UnitsPerEm);
        Assert.True(Sans.Ascender > 0);
        Assert.True(Sans.Descender < 0);
        Assert.True(Sans.CapHeight > 1000 && Sans.CapHeight < Sans.Ascender);
        Assert.False(Sans.IsBold);
        Assert.False(Sans.IsItalic);
        Assert.True(Sans.EmbeddingAllowed);
    }

    [Fact]
    public void Load_DetectsBoldAndItalic()
    {
        var bold = TrueTypeFont.Load(TestAssets.Font("LiberationSans-Bold.ttf"));
        var italic = TrueTypeFont.Load(TestAssets.Font("LiberationSerif-Italic.ttf"));
        Assert.True(bold.IsBold);
        Assert.True(italic.IsItalic);
        Assert.True(italic.ItalicAngle < 0);
    }

    [Fact]
    public void CharacterMap_CoversLatinGreekCyrillicAndEuro()
    {
        foreach (var c in "AÄßẞ€„“–αΩДж")
            Assert.True(Sans.HasCharacter(c), $"Character {c} is missing");
        Assert.False(Sans.HasCharacter(0x1F600)); // emoji
        Assert.Equal(0, Sans.GetGlyphId(0x1F600));
    }

    [Fact]
    public void AdvanceWidths_MatchArialMetrics()
    {
        // Liberation Sans is metrically compatible with Arial: "A" = 1366, space = 569 units.
        Assert.Equal(1366, Sans.GetAdvanceWidth(Sans.GetGlyphId('A')));
        Assert.Equal(569, Sans.GetAdvanceWidth(Sans.GetGlyphId(' ')));
    }

    [Fact]
    public void Load_RejectsInvalidData()
    {
        Assert.Throws<FontFormatException>(() => TrueTypeFont.Load(new byte[100]));
        Assert.Throws<FontFormatException>(() => TrueTypeFont.Load([0x00, 0x01, 0x00, 0x00, 0, 1, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Subset_KeepsGlyphsAndMetrics()
    {
        int a = Sans.GetGlyphId('A');
        int euro = Sans.GetGlyphId('€');
        int[] glyphs = [0, a, euro];

        var result = TrueTypeSubsetter.Build(Sans, glyphs);
        var subset = TrueTypeFont.Load(result.FontProgram, requireCharacterMap: false);

        Assert.True(result.FontProgram.Length < 20_000, $"Subset too large: {result.FontProgram.Length} bytes");
        Assert.Equal(glyphs, result.Glyphs);
        Assert.Equal(glyphs.Length, subset.GlyphCount);
        Assert.Equal(Sans.UnitsPerEm, subset.UnitsPerEm);
        for (int newId = 0; newId < glyphs.Length; newId++)
        {
            Assert.Equal(Sans.GetAdvanceWidth(glyphs[newId]), subset.GetAdvanceWidth(newId));
            Assert.Equal(Sans.GetLeftSideBearing(glyphs[newId]), subset.GetLeftSideBearing(newId));
        }
        Assert.True(Sans.GetGlyphData(a).SequenceEqual(subset.GetGlyphData(1)));
        Assert.True(Sans.GetGlyphData(euro).SequenceEqual(subset.GetGlyphData(2)));
    }

    [Fact]
    public void Subset_DoesNotModifyTheInputList()
    {
        int composite = FindCompositeGlyph();
        var input = new List<int> { 0, composite };
        var result = TrueTypeSubsetter.Build(Sans, input);

        Assert.Equal([0, composite], input);
        Assert.True(result.Glyphs.Count > input.Count);
    }

    [Fact]
    public void Subset_ChecksumAdjustmentIsValid()
    {
        var subset = TrueTypeSubsetter.Build(Sans, [0, Sans.GetGlyphId('x')]);
        // The checksum of the whole file must equal 0xB1B0AFBA (OpenType, table "head").
        Assert.Equal(0xB1B0AFBAu, FontChecksum.Compute(subset.FontProgram));
    }

    [Fact]
    public void Subset_ResolvesCompositeGlyphsAndRenumbersReferences()
    {
        int composite = FindCompositeGlyph();
        var components = CompositeGlyph.GetComponents(Sans.GetGlyphData(composite)).Select(c => c.GlyphId).ToList();

        var result = TrueTypeSubsetter.Build(Sans, [0, composite]);
        var glyphs = result.Glyphs.ToList();
        var subset = TrueTypeFont.Load(result.FontProgram, requireCharacterMap: false);

        // The components were appended …
        foreach (var component in components)
            Assert.Contains(component, glyphs);
        Assert.Equal(glyphs.Count, subset.GlyphCount);

        // … and the references inside the subset point to the new numbers.
        var newReferences = CompositeGlyph.GetComponents(subset.GetGlyphData(1)).Select(c => c.GlyphId).ToList();
        Assert.Equal(components.Select(c => glyphs.IndexOf(c)), newReferences);
        foreach (var newId in newReferences)
            Assert.True(Sans.GetGlyphData(glyphs[newId]).SequenceEqual(subset.GetGlyphData(newId)));
    }

    [Fact]
    public void Subset_RequiresNotdefFirst()
    {
        Assert.Throws<ArgumentException>(() => TrueTypeSubsetter.Build(Sans, [Sans.GetGlyphId('A')]));
    }

    /// <summary>Finds a composite glyph in the test font (e.g. a letter with accent).</summary>
    static int FindCompositeGlyph() => Enumerable.Range(1, Sans.GlyphCount - 1)
        .First(g => CompositeGlyph.GetComponents(Sans.GetGlyphData(g)).Count > 0);
}
