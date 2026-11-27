// Reference to an indirect PDF object, written as "n 0 R".

using System.Globalization;

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// Reference to an indirect object (ISO 32000-2, 7.3.10). "12 0 R" means: object number 12,
/// generation 0. IndyraPdf always writes new files, so the generation is always 0.
/// </summary>
internal sealed class PdfReference : PdfObject
{
    /// <summary>Creates a reference.</summary>
    /// <param name="objectNumber">Number of the object, starting at 1.</param>
    public PdfReference(int objectNumber) => ObjectNumber = objectNumber;

    /// <summary>Number of the referenced object.</summary>
    public int ObjectNumber { get; }

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output)
        => output.Write(ObjectNumber.ToString(CultureInfo.InvariantCulture) + " 0 R");
}
