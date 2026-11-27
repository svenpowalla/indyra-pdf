// Writes the XMP metadata packet of a document (ISO 16684-1, embedded as ISO 32000-2, 14.3.2).
//
// XMP is RDF/XML. PDF/A requires it in the catalog and checks that it matches the info dictionary:
//   Info /Title        ↔ dc:title (language alternative, x-default)
//   Info /Author       ↔ dc:creator (ordered list)
//   Info /Subject      ↔ dc:description (language alternative)
//   Info /Keywords     ↔ pdf:Keywords
//   Info /Creator      ↔ xmp:CreatorTool
//   Info /Producer     ↔ pdf:Producer
//   Info /CreationDate ↔ xmp:CreateDate,  Info /ModDate ↔ xmp:ModifyDate
// The PDF/A identification is pdfaid:part (3) and pdfaid:conformance (B or U).
// Extension schemas (e.g. Factur-X) are described in pdfaExtension:schemas.
//
// The packet is built with System.Xml.Linq, so all values are escaped correctly.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IndyraPdf.Writer.Metadata;

/// <summary>Builds the XMP metadata packet.</summary>
internal static class XmpWriter
{
    static readonly XNamespace X = "adobe:ns:meta/";
    static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    static readonly XNamespace PdfaId = "http://www.aiim.org/pdfa/ns/id/";
    static readonly XNamespace PdfaExtension = "http://www.aiim.org/pdfa/ns/extension/";
    static readonly XNamespace PdfaSchema = "http://www.aiim.org/pdfa/ns/schema#";
    static readonly XNamespace PdfaProperty = "http://www.aiim.org/pdfa/ns/property#";
    static readonly XNamespace PdfuaId = "http://www.aiim.org/pdfua/ns/id/";

    /// <summary>
    /// Builds the packet. The xpacket processing instructions frame the XML so that tools can
    /// find and update it inside the file; "begin" contains the byte order mark U+FEFF.
    /// </summary>
    /// <param name="info">Document information.</param>
    /// <param name="conformance">PDF/A level.</param>
    /// <param name="universalAccessibility">Whether to write the PDF/UA-1 identification (pdfuaid:part = 1).</param>
    /// <param name="extensions">Additional schemas.</param>
    public static byte[] Build(PdfDocumentInfo info, PdfConformance conformance, bool universalAccessibility, IReadOnlyList<XmpExtension> extensions)
    {
        var rdf = new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName));

        if (conformance != PdfConformance.None)
        {
            string level = conformance switch
            {
                PdfConformance.PdfA3A => "A",
                PdfConformance.PdfA3U => "U",
                _ => "B",
            };
            rdf.Add(Description(PdfaId, "pdfaid",
                new XElement(PdfaId + "part", "3"),
                new XElement(PdfaId + "conformance", level)));
        }

        var allExtensions = extensions.ToList();
        if (universalAccessibility)
        {
            rdf.Add(Description(PdfuaId, "pdfuaid", new XElement(PdfuaId + "part", "1")));
            // pdfuaid is no predefined PDF/A schema, so a PDF/A file must describe it (ISO 19005-3, 6.6.2.3).
            if (conformance != PdfConformance.None)
            {
                allExtensions.Insert(0, new XmpExtension("PDF/UA Universal Accessibility Schema", PdfuaId.NamespaceName, "pdfuaid")
                    .Add(new XmpProperty("part", "1", "Indicates, which part of ISO 14289 standard is followed", "Integer", "internal")));
            }
        }

        var dc = Description(Dc, "dc", new XElement(Dc + "format", "application/pdf"));
        if (!string.IsNullOrEmpty(info.Title))
            dc.Add(new XElement(Dc + "title", LanguageAlternative(info.Title)));
        if (!string.IsNullOrEmpty(info.Author))
            dc.Add(new XElement(Dc + "creator", new XElement(Rdf + "Seq", new XElement(Rdf + "li", info.Author))));
        if (!string.IsNullOrEmpty(info.Subject))
            dc.Add(new XElement(Dc + "description", LanguageAlternative(info.Subject)));
        rdf.Add(dc);

        var xmp = Description(Xmp, "xmp",
            new XElement(Xmp + "CreateDate", Date(info.CreationDate)),
            new XElement(Xmp + "ModifyDate", Date(info.EffectiveModificationDate)),
            new XElement(Xmp + "MetadataDate", Date(info.EffectiveModificationDate)));
        if (!string.IsNullOrEmpty(info.Creator))
            xmp.AddFirst(new XElement(Xmp + "CreatorTool", info.Creator));
        rdf.Add(xmp);

        var pdf = Description(Pdf, "pdf");
        if (!string.IsNullOrEmpty(info.Producer))
            pdf.Add(new XElement(Pdf + "Producer", info.Producer));
        if (!string.IsNullOrEmpty(info.Keywords))
            pdf.Add(new XElement(Pdf + "Keywords", info.Keywords));
        rdf.Add(pdf);

        if (allExtensions.Count > 0)
        {
            rdf.Add(ExtensionSchemas(allExtensions));
            foreach (var extension in extensions)
            {
                XNamespace ns = extension.NamespaceUri;
                rdf.Add(Description(ns, extension.Prefix,
                    extension.Properties.Select(p => new XElement(ns + p.Name, p.Value)).ToArray()));
            }
        }

        var meta = new XElement(X + "xmpmeta", new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName), rdf);

        var sb = new StringBuilder();
        sb.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = " " }))
            meta.WriteTo(writer);
        sb.Append("\n<?xpacket end=\"w\"?>");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
    }

    /// <summary>An rdf:Description for one namespace (rdf:about="" means: this document).</summary>
    static XElement Description(XNamespace ns, string prefix, params object[] content)
        => new(Rdf + "Description",
               new XAttribute(Rdf + "about", ""),
               new XAttribute(XNamespace.Xmlns + prefix, ns.NamespaceName),
               content);

    /// <summary>A language alternative with only the default language: rdf:Alt / rdf:li xml:lang="x-default".</summary>
    static XElement LanguageAlternative(string value)
        => new(Rdf + "Alt", new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value));

    /// <summary>
    /// Describes all extension schemas for PDF/A: a bag of schemas, each with name, namespace,
    /// prefix and an ordered list of property definitions.
    /// </summary>
    static XElement ExtensionSchemas(IReadOnlyList<XmpExtension> extensions)
    {
        var bag = new XElement(Rdf + "Bag");
        foreach (var extension in extensions)
        {
            var properties = new XElement(Rdf + "Seq");
            foreach (var property in extension.Properties)
            {
                properties.Add(new XElement(Rdf + "li", new XAttribute(Rdf + "parseType", "Resource"),
                    new XElement(PdfaProperty + "name", property.Name),
                    new XElement(PdfaProperty + "valueType", property.ValueType),
                    new XElement(PdfaProperty + "category", property.Category),
                    new XElement(PdfaProperty + "description", property.Description)));
            }
            bag.Add(new XElement(Rdf + "li", new XAttribute(Rdf + "parseType", "Resource"),
                new XElement(PdfaSchema + "schema", extension.SchemaName),
                new XElement(PdfaSchema + "namespaceURI", extension.NamespaceUri),
                new XElement(PdfaSchema + "prefix", extension.Prefix),
                new XElement(PdfaSchema + "property", properties)));
        }
        return new XElement(Rdf + "Description",
            new XAttribute(Rdf + "about", ""),
            new XAttribute(XNamespace.Xmlns + "pdfaExtension", PdfaExtension.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdfaSchema", PdfaSchema.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdfaProperty", PdfaProperty.NamespaceName),
            new XElement(PdfaExtension + "schemas", bag));
    }

    /// <summary>XMP date (ISO 8601) with the same offset as the PDF date, e.g. 2026-11-22T10:30:00+01:00.</summary>
    internal static string Date(DateTimeOffset date)
        => date.Offset == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
