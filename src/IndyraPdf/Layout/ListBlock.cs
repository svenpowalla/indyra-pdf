// A list: consecutive list items (paragraphs with a marker) grouped into one list element.
//
// Visually a list is just a sequence of paragraphs with markers. For tagged PDF the items must be
// grouped: L (list) → LI (item) → Lbl (marker) + LBody (text). This block provides the L level.

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>A bulleted or numbered list of paragraphs.</summary>
public sealed class ListBlock : Block
{
    /// <summary>The items.</summary>
    readonly List<Paragraph> _items = [];

    /// <summary>The items in order.</summary>
    public IReadOnlyList<Paragraph> Items => _items;

    /// <summary>Appends an item. It should have a <see cref="Paragraph.Marker"/> such as "•" or "1.".</summary>
    public ListBlock Add(Paragraph item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        return this;
    }

    /// <inheritdoc/>
    internal override IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width)
        => new ListFormatter(this, new StackFormatter(_items, environment, width));
}
