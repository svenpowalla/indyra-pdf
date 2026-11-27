// Semantic roles of content for tagged PDF (accessibility, PDF/UA, PDF/A-3a).
//
// Screen readers read a tagged PDF along its structure tree, not along the drawing order.
// The roles correspond to the standard structure types of ISO 32000-1, 14.8.4.

namespace IndyraPdf.Drawing;

/// <summary>Role of a structure element (standard structure types of ISO 32000-1, 14.8.4).</summary>
public enum StructureRole
{
    /// <summary>Paragraph (P).</summary>
    Paragraph,

    /// <summary>Heading level 1 (H1).</summary>
    Heading1,

    /// <summary>Heading level 2 (H2).</summary>
    Heading2,

    /// <summary>Heading level 3 (H3).</summary>
    Heading3,

    /// <summary>Heading level 4 (H4).</summary>
    Heading4,

    /// <summary>Heading level 5 (H5).</summary>
    Heading5,

    /// <summary>Heading level 6 (H6).</summary>
    Heading6,

    /// <summary>List (L).</summary>
    List,

    /// <summary>List item (LI), containing label and body.</summary>
    ListItem,

    /// <summary>List label such as "•" or "1." (Lbl).</summary>
    ListLabel,

    /// <summary>Content of a list item (LBody).</summary>
    ListBody,

    /// <summary>Table.</summary>
    Table,

    /// <summary>Table row (TR).</summary>
    TableRow,

    /// <summary>Header cell (TH); its scope is the column below it.</summary>
    TableHeaderCell,

    /// <summary>Data cell (TD).</summary>
    TableDataCell,

    /// <summary>Image or graphic with alternative text (Figure).</summary>
    Figure,
}
