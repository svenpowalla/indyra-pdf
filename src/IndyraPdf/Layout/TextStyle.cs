// Character formatting of text: font, size, emphasis, decorations and colors.

using IndyraPdf.Writer;

namespace IndyraPdf.Layout;

/// <summary>
/// Character format. Immutable; derive variants with <c>with</c>, e.g.
/// <c>style with { Bold = true }</c>.
/// </summary>
public sealed record TextStyle
{
    /// <summary>Default style: default family, 11 pt, black.</summary>
    public static TextStyle Default { get; } = new();

    /// <summary>Font family; null means the default family of the <see cref="FontCollection"/>.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font size in points.</summary>
    public double FontSize { get; init; } = 11;

    /// <summary>Bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Italic.</summary>
    public bool Italic { get; init; }

    /// <summary>Underlined.</summary>
    public bool Underline { get; init; }

    /// <summary>Struck through.</summary>
    public bool Strikethrough { get; init; }

    /// <summary>Text color.</summary>
    public PdfColor Color { get; init; } = PdfColor.Black;

    /// <summary>Background color behind the text (highlighting), or null.</summary>
    public PdfColor? Background { get; init; }

    /// <summary>
    /// Apply the font's kerning (pair-wise spacing such as "AV"). On by default, as in browsers.
    /// </summary>
    public bool Kerning { get; init; } = true;
}
