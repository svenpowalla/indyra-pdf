// Shape of line ends. The numeric values are the ones PDF uses.

namespace IndyraPdf.Writer;

/// <summary>Shape of line ends (ISO 32000-2, 8.4.3.3).</summary>
public enum PdfLineCap
{
    /// <summary>Cut off square at the end point.</summary>
    Butt = 0,

    /// <summary>Semicircle around the end point.</summary>
    Round = 1,

    /// <summary>Square that extends half the line width beyond the end point.</summary>
    Square = 2,
}
