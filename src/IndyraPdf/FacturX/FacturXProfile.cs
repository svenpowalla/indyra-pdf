// Profiles of Factur-X 1.0 / ZUGFeRD 2.x and their names in the XMP metadata.

namespace IndyraPdf.FacturX;

/// <summary>Factur-X / ZUGFeRD profile of the embedded invoice XML.</summary>
public enum FacturXProfile
{
    /// <summary>MINIMUM: only the data needed for booking; not a complete invoice.</summary>
    Minimum,

    /// <summary>BASIC WL ("without lines"): header data without invoice lines; not a complete invoice.</summary>
    BasicWl,

    /// <summary>BASIC: invoice with lines, subset of EN 16931.</summary>
    Basic,

    /// <summary>EN 16931 ("COMFORT"): the European standard for electronic invoices.</summary>
    En16931,

    /// <summary>EXTENDED: EN 16931 plus additional data.</summary>
    Extended,

    /// <summary>XRECHNUNG: German CIUS of EN 16931 for public authorities.</summary>
    XRechnung,
}
