// Horizontal alignment of the lines of a paragraph.

namespace IndyraPdf.Layout;

/// <summary>Horizontal alignment of the lines of a paragraph.</summary>
public enum TextAlignment
{
    /// <summary>Left aligned, ragged right.</summary>
    Left,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Right aligned, ragged left.</summary>
    Right,

    /// <summary>
    /// Justified: lines fill the full width by widening the spaces. The last line and lines
    /// ending with a forced line break stay left aligned.
    /// </summary>
    Justify,
}
