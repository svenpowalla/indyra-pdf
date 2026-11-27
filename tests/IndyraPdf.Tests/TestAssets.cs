// Test helper: paths to the fonts and images in the repository's "assets" folder.

namespace IndyraPdf.Tests;

/// <summary>Paths to the test files in the "assets" folder of the repository.</summary>
internal static class TestAssets
{
    /// <summary>Repository root, found by searching upwards for IndyraPdf.slnx.</summary>
    static readonly Lazy<string> Root = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IndyraPdf.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository folder containing IndyraPdf.slnx not found.");
    });

    /// <summary>Path of a font in assets/fonts.</summary>
    public static string Font(string name) => Path.Combine(Root.Value, "assets", "fonts", name);

    /// <summary>Path of an image in assets/images.</summary>
    public static string Image(string name) => Path.Combine(Root.Value, "assets", "images", name);

    /// <summary>Path of an invoice XML in assets/invoices.</summary>
    public static string Invoice(string name) => Path.Combine(Root.Value, "assets", "invoices", name);

    /// <summary>Liberation Sans Regular, the default test font.</summary>
    public static string SansRegular => Font("LiberationSans-Regular.ttf");

    /// <summary>Noto Color Emoji, the fallback font for emoji tests.</summary>
    public static string EmojiFont => Font("NotoColorEmoji.ttf");
}
