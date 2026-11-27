// Report of content that did not fit into its area.
//
// The layout engine never fails because content is too large: at the top of a page it always
// places something, and fixed blocks are drawn even if their content is taller than their area.
// Such content runs beyond the area (or is cut off, see FixedBlock.Clip). LaidOutDocument.Overflows
// lists these places, so an application can warn its user, e.g. in a document designer.

namespace IndyraPdf.Layout;

/// <summary>A place where content is taller than the area reserved for it.</summary>
/// <param name="PageIndex">Page, starting at 0.</param>
/// <param name="FixedBlock">
/// The fixed block whose content is taller than its <see cref="FixedBlock.Area"/>, or null if the
/// flowing content does not fit into the content area of the page (e.g. a table row or an image
/// taller than a whole page).
/// </param>
/// <param name="Excess">How far the content extends beyond the area, in points.</param>
public sealed record LayoutOverflow(int PageIndex, FixedBlock? FixedBlock, double Excess);
