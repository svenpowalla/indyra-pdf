// Archive standards a PDF document can be written to.

namespace IndyraPdf.Writer;

/// <summary>Standard the written PDF conforms to.</summary>
public enum PdfConformance
{
    /// <summary>Plain PDF 1.7.</summary>
    None,

    /// <summary>
    /// PDF/A-3b (ISO 19005-3, level B "basic"): long-term archivable appearance. Allows embedded
    /// files, as required for ZUGFeRD / Factur-X invoices.
    /// </summary>
    PdfA3B,

    /// <summary>PDF/A-3u: like PDF/A-3b, additionally all text can be mapped to Unicode.</summary>
    PdfA3U,

    /// <summary>
    /// PDF/A-3a ("accessible"): like PDF/A-3u, additionally tagged with a logical structure and a
    /// document language. Requires the content to be drawn by the layout engine.
    /// </summary>
    PdfA3A,
}
