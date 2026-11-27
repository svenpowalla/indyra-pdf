// The structure tree of a tagged PDF (ISO 32000-1, 14.7 and 14.8).
//
// Written objects:
//   Catalog /StructTreeRoot → <</Type/StructTreeRoot /K Document /ParentTree … /ParentTreeNextKey n>>
//   Structure element       → <</Type/StructElem /S/P /P parent /Pg page /K [kids] /Alt (…)>>
//                              kids: MCID numbers (content on /Pg), <</Type/MCR/Pg page/MCID n>>
//                              for content on other pages, or child elements
//   Parent tree             → number tree: page /StructParents i → array whose entry n is the
//                              element owning MCID n on that page (lets readers go from content
//                              to structure)
//
// Elements are identified by the key of their StructureTag, so an element that continues on the
// next page (a long paragraph, a table) stays one element. The children of the root are sorted
// by page and vertical position, so fixed blocks such as an address field appear in reading
// order before the body text, although they are drawn after it.

using IndyraPdf.Drawing;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Tagging;

/// <summary>Collects structure elements and marked content while pages are drawn, then writes the tree.</summary>
internal sealed class StructureTree
{
    /// <summary>Elements by the key of their tag.</summary>
    readonly Dictionary<object, StructureElement> _elements = [];

    /// <summary>Per page: owner element of each MCID (index = MCID).</summary>
    readonly Dictionary<int, List<StructureElement>> _contentByPage = [];

    /// <summary>The root element of type "Document".</summary>
    public StructureElement Root { get; } = new("Document", null, null, 0, 0);

    /// <summary>
    /// Returns the element for a tag, creating it as child of <paramref name="parent"/> on first use.
    /// </summary>
    public StructureElement GetOrCreate(StructureTag tag, StructureElement parent, int pageIndex, double top)
    {
        if (!_elements.TryGetValue(tag.Key, out var element))
        {
            element = new StructureElement(StructureElement.TypeOf(tag.Role), parent, tag.AlternativeText, pageIndex, top, tag.ColumnSpan);
            _elements[tag.Key] = element;
            parent.AddKid(element);
        }
        return element;
    }

    /// <summary>Registers new marked content of an element on a page and returns its MCID.</summary>
    public int AddContent(StructureElement owner, int pageIndex)
    {
        if (!_contentByPage.TryGetValue(pageIndex, out var owners))
            _contentByPage[pageIndex] = owners = [];
        owners.Add(owner);
        int mcid = owners.Count - 1;
        owner.AddKid(new MarkedContentReference(pageIndex, mcid));
        return mcid;
    }

    /// <summary>
    /// Writes the tree and returns the StructTreeRoot. Every page gets /StructParents (its index)
    /// and /Tabs /S (tab order follows the structure, required by PDF/UA).
    /// </summary>
    public PdfDictionary Write(PdfObjectTable table, IReadOnlyList<PdfDictionary> pages)
    {
        // Reading order of the top level: by page, then from top to bottom.
        Root.SortKids((a, b) =>
        {
            var x = (StructureElement)a;
            var y = (StructureElement)b;
            return x.FirstPage != y.FirstPage ? x.FirstPage.CompareTo(y.FirstPage) : x.Top.CompareTo(y.Top);
        });

        var root = table.Add(new PdfDictionary("StructTreeRoot"));
        var written = new Dictionary<StructureElement, PdfDictionary>(ReferenceEqualityComparer.Instance);
        root["K"] = WriteElement(table, Root, root, pages, written);

        var nums = new PdfArray();
        for (int page = 0; page < pages.Count; page++)
        {
            pages[page]["StructParents"] = new PdfNumber(page);
            pages[page]["Tabs"] = new PdfName("S");
            var owners = new PdfArray();
            foreach (var owner in _contentByPage.GetValueOrDefault(page) ?? [])
                owners.Add(written[owner]);
            nums.Add(new PdfNumber(page)).Add(owners);
        }
        var parentTree = new PdfDictionary();
        parentTree["Nums"] = nums;
        root["ParentTree"] = parentTree;
        root["ParentTreeNextKey"] = new PdfNumber(pages.Count);
        return root;
    }

    /// <summary>Writes an element and, recursively, its kids.</summary>
    static PdfDictionary WriteElement(PdfObjectTable table, StructureElement element, PdfDictionary parent,
                                      IReadOnlyList<PdfDictionary> pages, Dictionary<StructureElement, PdfDictionary> written)
    {
        var dictionary = table.Add(new PdfDictionary("StructElem"));
        written[element] = dictionary;
        dictionary["S"] = new PdfName(element.Type);
        dictionary["P"] = parent;

        // /Pg names the default page; MCIDs on it can be written as plain numbers.
        int? defaultPage = element.Kids.OfType<MarkedContentReference>().FirstOrDefault()?.PageIndex;
        if (defaultPage is { } pageIndex)
            dictionary["Pg"] = pages[pageIndex];
        if (element.AlternativeText is { } alt)
            dictionary["Alt"] = PdfString.FromText(alt);
        if (TableAttributes(element) is { } attributes)
            dictionary["A"] = attributes;

        var kids = new PdfArray();
        foreach (var kid in element.Kids)
        {
            switch (kid)
            {
                case StructureElement child:
                    kids.Add(WriteElement(table, child, dictionary, pages, written));
                    break;
                case MarkedContentReference reference when reference.PageIndex == defaultPage:
                    kids.Add(new PdfNumber(reference.Mcid));
                    break;
                case MarkedContentReference reference:
                    var mcr = new PdfDictionary("MCR");
                    mcr["Pg"] = pages[reference.PageIndex];
                    mcr["MCID"] = new PdfNumber(reference.Mcid);
                    kids.Add(mcr);
                    break;
            }
        }
        dictionary["K"] = kids;
        return dictionary;
    }

    /// <summary>
    /// Builds the attribute object of a table cell (ISO 32000-1, 14.8.5.7, owner /Table), or
    /// returns null when the element needs none.
    /// <list type="bullet">
    /// <item>/Scope /Column on header cells: the header applies to the column below it
    /// (PDF/UA requires a scope or explicit headers).</item>
    /// <item>/ColSpan n on cells that cover several columns. Without it, readers count one column
    /// per cell, and PDF/UA validation (veraPDF rule 7.2-43) reports rows of different width.</item>
    /// </list>
    /// </summary>
    static PdfDictionary? TableAttributes(StructureElement element)
    {
        bool isHeader = element.Type == "TH";
        bool isCell = isHeader || element.Type == "TD";
        if (!isCell || (!isHeader && element.ColumnSpan == 1))
            return null;

        var attributes = new PdfDictionary();
        attributes["O"] = new PdfName("Table");
        if (isHeader)
            attributes["Scope"] = new PdfName("Column");
        if (element.ColumnSpan > 1)
            attributes["ColSpan"] = new PdfNumber(element.ColumnSpan);
        return attributes;
    }
}
