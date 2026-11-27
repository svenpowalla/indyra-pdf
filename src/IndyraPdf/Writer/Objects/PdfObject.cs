// Base type of the in-memory PDF object model.
//
// A PDF file is a collection of objects (ISO 32000-2, section 7.3): numbers, names (/Type),
// strings, arrays [...], dictionaries <<...>> and streams (a dictionary followed by binary data).
// Objects can be written "directly" where they are used, or as numbered "indirect objects" that
// are referenced elsewhere by "n 0 R". Pages, fonts and images are always indirect objects.
//
// IndyraPdf first builds all objects in memory and then writes them in one go. This approach
// follows PDFsharp 6.2.4; FPDF instead writes text directly to the output. The object model is
// needed later for PDF/A-3 (metadata, embedded files) and tagging.

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// Base type of all PDF objects (ISO 32000-2, section 7.3).
/// </summary>
/// <remarks>
/// An object registered in a <see cref="PdfObjectTable"/> is an indirect object. When it appears
/// inside an array or dictionary, a reference ("n 0 R") is written automatically instead of its
/// content.
/// </remarks>
internal abstract class PdfObject
{
    /// <summary>Reference to this object if it is registered as an indirect object, otherwise null.</summary>
    public PdfReference? Reference { get; private set; }

    /// <summary>Marks the object as indirect object with the given number. Called by <see cref="PdfObjectTable"/>.</summary>
    internal void AssignReference(PdfReference reference)
    {
        if (Reference is not null)
            throw new InvalidOperationException("The object is already registered.");
        Reference = reference;
    }

    /// <summary>Writes the content of the object in PDF syntax.</summary>
    public abstract void WriteTo(PdfOutput output);

    /// <summary>Writes the object as a value: indirect objects as reference, others directly.</summary>
    public void WriteValueTo(PdfOutput output)
    {
        if (Reference is { } reference)
            reference.WriteTo(output);
        else
            WriteTo(output);
    }

    /// <summary>
    /// Whether the written form starts with a delimiter ( / &lt; [ ( ). Then no space is needed
    /// before it, e.g. "/Type/Page" instead of "/Type /Page".
    /// </summary>
    public virtual bool StartsWithDelimiter => false;
}
