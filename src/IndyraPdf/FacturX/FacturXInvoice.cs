// Turns a PDF document into a hybrid e-invoice according to Factur-X 1.0 / ZUGFeRD 2.x.
//
// Requirements (Factur-X 1.09.2 = ZUGFeRD 2.5.2, chapter "PDF/A-3"):
//   - the PDF conforms to PDF/A-3 (IndyraPdf: level B or U),
//   - the invoice XML (CII syntax) is embedded as "factur-x.xml" ("xrechnung.xml" for XRechnung),
//     MIME type text/xml, with modification date, listed in the AF array of the catalog,
//   - AFRelationship is "Alternative"; for MINIMUM and BASIC WL it is "Data", because their XML
//     is not a complete invoice,
//   - the XMP metadata contains the Factur-X extension schema (prefix "fx") with DocumentType,
//     DocumentFileName, Version and ConformanceLevel, described as PDF/A extension schema.
//
// Creating and validating the XML itself (EN 16931 business rules, KoSIT validator) is the job of
// the application; this class only embeds it.

using System.Xml;
using IndyraPdf.Writer;

namespace IndyraPdf.FacturX;

/// <summary>Embeds a Factur-X / ZUGFeRD invoice XML into a PDF/A-3 document.</summary>
public static class FacturXInvoice
{
    /// <summary>Namespace of the Factur-X XMP extension schema.</summary>
    public const string XmpNamespace = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";

    /// <summary>Root element of a Cross Industry Invoice (UN/CEFACT CII D16B).</summary>
    const string CiiRootElement = "CrossIndustryInvoice";

    /// <summary>
    /// Embeds the invoice XML and adds the Factur-X metadata. If the document is not set to
    /// PDF/A yet, it is switched to PDF/A-3b.
    /// </summary>
    /// <param name="document">The PDF document showing the invoice.</param>
    /// <param name="xml">The invoice in CII syntax (UTF-8).</param>
    /// <param name="profile">Profile of the XML.</param>
    /// <param name="modificationDate">Modification date of the XML; default: creation date of the document.</param>
    /// <param name="version">Value of fx:Version, the version of the Factur-X XMP schema ("1.0").</param>
    /// <exception cref="ArgumentException">The XML is not well-formed or not a Cross Industry Invoice.</exception>
    public static void Attach(PdfDocument document, byte[] xml, FacturXProfile profile,
                              DateTimeOffset? modificationDate = null, string version = "1.0")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(xml);
        CheckCrossIndustryInvoice(xml);

        if (document.Conformance == PdfConformance.None)
            document.Conformance = PdfConformance.PdfA3B;

        string fileName = FileName(profile);
        document.Attach(new PdfAttachment(fileName, xml, "text/xml")
        {
            Description = profile == FacturXProfile.XRechnung ? "XRechnung" : "Factur-X/ZUGFeRD invoice",
            Relationship = profile is FacturXProfile.Minimum or FacturXProfile.BasicWl
                ? AttachmentRelationship.Data
                : AttachmentRelationship.Alternative,
            ModificationDate = modificationDate,
        });

        document.AddXmpExtension(new XmpExtension("Factur-X PDFA Extension Schema", XmpNamespace, "fx")
            .Add(new XmpProperty("DocumentFileName", fileName, "The name of the embedded XML document"))
            .Add(new XmpProperty("DocumentType", "INVOICE", "The type of the hybrid document in capital letters, e.g. INVOICE or ORDER"))
            .Add(new XmpProperty("Version", version, "The actual version of the standard applying to the embedded XML document"))
            .Add(new XmpProperty("ConformanceLevel", ConformanceLevel(profile), "The conformance level of the embedded XML document")));
    }

    /// <summary>File name of the embedded XML for a profile.</summary>
    public static string FileName(FacturXProfile profile)
        => profile == FacturXProfile.XRechnung ? "xrechnung.xml" : "factur-x.xml";

    /// <summary>Value of fx:ConformanceLevel for a profile.</summary>
    public static string ConformanceLevel(FacturXProfile profile) => profile switch
    {
        FacturXProfile.Minimum => "MINIMUM",
        FacturXProfile.BasicWl => "BASIC WL",
        FacturXProfile.Basic => "BASIC",
        FacturXProfile.En16931 => "EN 16931",
        FacturXProfile.Extended => "EXTENDED",
        FacturXProfile.XRechnung => "XRECHNUNG",
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    /// <summary>Checks that the data is well-formed XML with a CrossIndustryInvoice root element.</summary>
    static void CheckCrossIndustryInvoice(byte[] xml)
    {
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            reader.MoveToContent();
            if (reader.LocalName != CiiRootElement)
                throw new ArgumentException($"The invoice XML must have the root element {CiiRootElement} (CII syntax), not {reader.LocalName}.", nameof(xml));
            while (reader.Read())
            {
                // read to the end to detect syntax errors anywhere in the document
            }
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"The invoice XML is not well-formed: {ex.Message}", nameof(xml), ex);
        }
    }
}
