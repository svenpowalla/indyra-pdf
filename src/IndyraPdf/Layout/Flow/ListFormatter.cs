// Places a list: its items like a sequence of paragraphs, wrapped in the list structure element.

using IndyraPdf.Drawing;

namespace IndyraPdf.Layout.Flow;

/// <summary>Formatter for <see cref="ListBlock"/>.</summary>
internal sealed class ListFormatter : IBlockFormatter
{
    /// <summary>The list (key of its structure element).</summary>
    readonly ListBlock _list;

    /// <summary>Formats the items.</summary>
    readonly StackFormatter _items;

    /// <summary>Creates the formatter.</summary>
    public ListFormatter(ListBlock list, StackFormatter items)
    {
        _list = list;
        _items = items;
    }

    /// <inheritdoc/>
    public FormatResult Format(double availableHeight, bool atTopOfArea)
    {
        var result = _items.Format(availableHeight, atTopOfArea);
        return result.Fragment is null
            ? result
            : result with { Fragment = new TaggedFragment(new StructureTag(StructureRole.List, _list), result.Fragment) };
    }
}
