// How an embedded file relates to the PDF document (PDF 2.0 / PDF/A-3 "AFRelationship").

namespace IndyraPdf.Writer;

/// <summary>Relationship of an embedded file to the document (ISO 32000-2, 7.11.3, AFRelationship).</summary>
public enum AttachmentRelationship
{
    /// <summary>The file is the original source of the document (e.g. a word processor file).</summary>
    Source,

    /// <summary>The file contains data used for the document (e.g. a table behind a chart).</summary>
    Data,

    /// <summary>The file is an alternative representation of the content (e.g. the XML of an e-invoice).</summary>
    Alternative,

    /// <summary>The file supplements the document.</summary>
    Supplement,

    /// <summary>The relationship is not known or none of the above.</summary>
    Unspecified,
}
