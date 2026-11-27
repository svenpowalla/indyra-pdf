// How a closed shape is painted.

namespace IndyraPdf.Writer;

/// <summary>How a shape is painted (ISO 32000-2, 8.5.3: path-painting operators).</summary>
public enum PdfPaint
{
    /// <summary>Outline only (PDF operator "S").</summary>
    Stroke,

    /// <summary>Fill only (PDF operator "f").</summary>
    Fill,

    /// <summary>Fill and outline (PDF operator "B").</summary>
    FillAndStroke,
}
