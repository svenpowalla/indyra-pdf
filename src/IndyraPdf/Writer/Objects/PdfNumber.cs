// PDF number object (integer or real).

namespace IndyraPdf.Writer.Objects;

/// <summary>A number (ISO 32000-2, 7.3.3). Written without exponent, see <see cref="PdfFormat.Number"/>.</summary>
internal sealed class PdfNumber : PdfObject
{
    /// <summary>Creates a number.</summary>
    public PdfNumber(double value) => Value = value;

    /// <summary>The value.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output) => output.Write(PdfFormat.Number(Value));
}
