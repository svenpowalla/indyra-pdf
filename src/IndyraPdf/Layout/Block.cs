// Base class of all content blocks: paragraphs, images, frames, tables.

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>
/// A content block that is placed from top to bottom: <see cref="Paragraph"/>, <see cref="ImageBlock"/>,
/// <see cref="Frame"/> or <see cref="Table"/>. Blocks are a pure description; the layout engine
/// creates a formatter for each block when it is placed.
/// </summary>
public abstract class Block
{
    /// <summary>Only blocks of this library are supported, because formatters are internal.</summary>
    private protected Block() { }

    /// <summary>
    /// Measures the height the block needs at the given width when nothing limits its height,
    /// e.g. to size the header and footer area of a page before computing the content area.
    /// Works for every kind of block; to measure several blocks, put them into a <see cref="Frame"/>.
    /// </summary>
    /// <remarks>
    /// The spacing after the last paragraph counts. Page number fields are measured with a
    /// placeholder value of two digits (<see cref="FieldValues.Estimate"/>) unless
    /// <paramref name="fields"/> is given.
    /// </remarks>
    /// <param name="fonts">Fonts to lay out with (the same as for the document).</param>
    /// <param name="width">Available width in points.</param>
    /// <param name="fields">Values for page number fields, or null for the placeholder.</param>
    /// <returns>Height in points; 0 for a block without content.</returns>
    public double Measure(FontCollection fonts, double width, FieldValues? fields = null)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        var values = fields ?? FieldValues.Estimate;
        var environment = new LayoutEnvironment(fonts, () => values);
        return CreateFormatter(environment, width).Format(double.PositiveInfinity, atTopOfArea: true).Fragment?.Height ?? 0;
    }

    /// <summary>Creates a formatter that places this block for the given width.</summary>
    internal abstract IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width);
}
