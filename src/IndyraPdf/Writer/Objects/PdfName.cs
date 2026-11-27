// PDF name object, e.g. /Type or /FlateDecode.

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// A name (ISO 32000-2, 7.3.5). Names act as keys and fixed values, e.g. /Type /Page.
/// The value is stored without the leading slash.
/// </summary>
internal sealed class PdfName : PdfObject
{
    /// <summary>Creates a name.</summary>
    /// <param name="value">Name without slash, e.g. "Page".</param>
    public PdfName(string value) => Value = value;

    /// <summary>The name without slash.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override bool StartsWithDelimiter => true;

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output) => output.Write(PdfFormat.Name(Value));
}
