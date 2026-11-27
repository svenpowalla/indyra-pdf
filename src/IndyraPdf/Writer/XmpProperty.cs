// One property of an XMP extension schema with its value.

namespace IndyraPdf.Writer;

/// <summary>A property of an <see cref="XmpExtension"/>, e.g. fx:DocumentType = INVOICE.</summary>
/// <param name="Name">Property name without prefix, e.g. "DocumentType".</param>
/// <param name="Value">Value written to the metadata.</param>
/// <param name="Description">Human-readable description for the schema definition.</param>
/// <param name="ValueType">XMP value type of the property; "Text" for plain strings.</param>
/// <param name="Category">"external" (set by the user/application) or "internal" (computed).</param>
public sealed record XmpProperty(string Name, string Value, string Description, string ValueType = "Text", string Category = "external");
