// Shape of corners where line segments meet. The numeric values are the ones PDF uses.

namespace IndyraPdf.Writer;

/// <summary>Shape of corners where two line segments meet (ISO 32000-2, 8.4.3.4).</summary>
public enum PdfLineJoin
{
    /// <summary>Pointed corner.</summary>
    Miter = 0,

    /// <summary>Rounded corner.</summary>
    Round = 1,

    /// <summary>Beveled (cut off) corner.</summary>
    Bevel = 2,
}
