// Test attributes: skip emoji tests when the emoji font (about 10 MB) is not in assets/fonts.

namespace IndyraPdf.Tests;

/// <summary>A <see cref="FactAttribute"/> that skips the test if Noto Color Emoji is missing.</summary>
public sealed class EmojiFontFactAttribute : FactAttribute
{
    /// <summary>Sets the skip reason if the font is missing.</summary>
    public EmojiFontFactAttribute()
    {
        if (!File.Exists(TestAssets.EmojiFont))
            Skip = "assets/fonts/NotoColorEmoji.ttf is missing.";
    }
}

/// <summary>A <see cref="TheoryAttribute"/> that skips the test if Noto Color Emoji is missing.</summary>
public sealed class EmojiFontTheoryAttribute : TheoryAttribute
{
    /// <summary>Sets the skip reason if the font is missing.</summary>
    public EmojiFontTheoryAttribute()
    {
        if (!File.Exists(TestAssets.EmojiFont))
            Skip = "assets/fonts/NotoColorEmoji.ttf is missing.";
    }
}
