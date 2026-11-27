// PDF boolean object.

namespace IndyraPdf.Writer.Objects;

/// <summary>A boolean value, written as "true" or "false" (ISO 32000-2, 7.3.2).</summary>
internal sealed class PdfBoolean : PdfObject
{
    /// <summary>Creates a boolean value.</summary>
    public PdfBoolean(bool value) => Value = value;

    /// <summary>The value.</summary>
    public bool Value { get; }

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output) => output.Write(Value ? "true" : "false");
}
