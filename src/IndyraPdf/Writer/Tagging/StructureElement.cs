// One node of the structure tree of a tagged PDF.

using IndyraPdf.Drawing;

namespace IndyraPdf.Writer.Tagging;

/// <summary>
/// A structure element: role, parent and kids. Kids are child elements or marked-content
/// references, in reading order.
/// </summary>
internal sealed class StructureElement
{
    /// <summary>The kids in reading order: <see cref="StructureElement"/> or <see cref="MarkedContentReference"/>.</summary>
    readonly List<object> _kids = [];

    /// <summary>Creates an element.</summary>
    /// <param name="type">PDF structure type, e.g. "P" or "Table".</param>
    /// <param name="parent">Parent element, null for the root (Document).</param>
    /// <param name="alternativeText">Alternative text, e.g. for figures.</param>
    /// <param name="firstPage">Page on which the element starts.</param>
    /// <param name="top">Y position on that page (used to sort the top-level elements).</param>
    /// <param name="columnSpan">Number of table columns a cell covers (1 for everything else).</param>
    public StructureElement(string type, StructureElement? parent, string? alternativeText, int firstPage, double top, int columnSpan = 1)
    {
        Type = type;
        Parent = parent;
        AlternativeText = alternativeText;
        FirstPage = firstPage;
        Top = top;
        ColumnSpan = columnSpan;
    }

    /// <summary>PDF structure type (/S).</summary>
    public string Type { get; }

    /// <summary>Parent element.</summary>
    public StructureElement? Parent { get; }

    /// <summary>Alternative text (/Alt), or null.</summary>
    public string? AlternativeText { get; }

    /// <summary>Page on which the element starts.</summary>
    public int FirstPage { get; }

    /// <summary>Y position on <see cref="FirstPage"/>.</summary>
    public double Top { get; }

    /// <summary>Number of table columns a cell (TH, TD) covers; 1 for other elements.</summary>
    public int ColumnSpan { get; }

    /// <summary>The kids in reading order.</summary>
    public IReadOnlyList<object> Kids => _kids;

    /// <summary>Appends a child element or marked-content reference.</summary>
    public void AddKid(object kid) => _kids.Add(kid);

    /// <summary>Sorts the kids, used for the top level (reading order by page, then position).</summary>
    public void SortKids(Comparison<object> comparison) => _kids.Sort(comparison);

    /// <summary>PDF structure type of a role.</summary>
    public static string TypeOf(StructureRole role) => role switch
    {
        StructureRole.Paragraph => "P",
        StructureRole.Heading1 => "H1",
        StructureRole.Heading2 => "H2",
        StructureRole.Heading3 => "H3",
        StructureRole.Heading4 => "H4",
        StructureRole.Heading5 => "H5",
        StructureRole.Heading6 => "H6",
        StructureRole.List => "L",
        StructureRole.ListItem => "LI",
        StructureRole.ListLabel => "Lbl",
        StructureRole.ListBody => "LBody",
        StructureRole.Table => "Table",
        StructureRole.TableRow => "TR",
        StructureRole.TableHeaderCell => "TH",
        StructureRole.TableDataCell => "TD",
        StructureRole.Figure => "Figure",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
