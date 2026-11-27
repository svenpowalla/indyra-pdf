// A block at a fixed position on a page (address field, logo, footer, input line).

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout;

/// <summary>
/// A block placed at a fixed position on every page of a <see cref="PageTemplate"/>. It is laid
/// out separately for each page, so page number fields show the right values.
/// </summary>
/// <param name="Area">Position and size on the page. The width is binding; content taller than the area overflows unless <paramref name="Clip"/> is set.</param>
/// <param name="Content">The block.</param>
/// <param name="Layer">Behind or in front of the flowing content.</param>
/// <param name="Clip">Whether content beyond the area is cut off.</param>
/// <param name="IsArtifact">
/// In tagged PDFs: whether the block is decoration or repetition that screen readers skip, e.g.
/// headers and footers with page numbers. Otherwise it is read in the order of its position.
/// </param>
public sealed record FixedBlock(Box Area, Block Content, FixedLayer Layer = FixedLayer.Foreground, bool Clip = false, bool IsArtifact = false);
