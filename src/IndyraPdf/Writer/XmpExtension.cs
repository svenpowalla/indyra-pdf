// Custom metadata in the XMP of a PDF/A document, e.g. the Factur-X invoice description.
//
// PDF/A only accepts XMP properties of predefined schemas. Any other schema must be described
// inside the XMP itself ("PDF/A extension schema", ISO 19005-1, 6.7.8, taken over by PDF/A-3):
// name of the schema, namespace, prefix and every property with type, category and description.
// IndyraPdf writes this description automatically for every XmpExtension.

namespace IndyraPdf.Writer;

/// <summary>An additional XMP schema with its property values, described for PDF/A.</summary>
public sealed class XmpExtension
{
    /// <summary>The properties.</summary>
    readonly List<XmpProperty> _properties = [];

    /// <summary>Creates an extension schema.</summary>
    /// <param name="schemaName">Name of the schema, e.g. "Factur-X PDFA Extension Schema".</param>
    /// <param name="namespaceUri">Namespace URI, e.g. "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#".</param>
    /// <param name="prefix">Preferred prefix, e.g. "fx".</param>
    public XmpExtension(string schemaName, string namespaceUri, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        SchemaName = schemaName;
        NamespaceUri = namespaceUri;
        Prefix = prefix;
    }

    /// <summary>Name of the schema.</summary>
    public string SchemaName { get; }

    /// <summary>Namespace URI.</summary>
    public string NamespaceUri { get; }

    /// <summary>Prefix.</summary>
    public string Prefix { get; }

    /// <summary>The properties in order.</summary>
    public IReadOnlyList<XmpProperty> Properties => _properties;

    /// <summary>Adds a property.</summary>
    public XmpExtension Add(XmpProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        _properties.Add(property);
        return this;
    }
}
