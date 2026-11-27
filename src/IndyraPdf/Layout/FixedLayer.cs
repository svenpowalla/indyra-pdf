// Drawing layer of fixed blocks relative to the flowing content.

namespace IndyraPdf.Layout;

/// <summary>Whether a fixed block is drawn behind or in front of the flowing content.</summary>
public enum FixedLayer
{
    /// <summary>In front of the flowing content (default; for fields, headers, footers).</summary>
    Foreground,

    /// <summary>Behind the flowing content (for background areas and watermarks).</summary>
    Background,
}
