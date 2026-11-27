// A file embedded in a PDF document, e.g. the XML of a ZUGFeRD invoice.

namespace IndyraPdf.Writer;

/// <summary>
/// A file embedded in the PDF ("associated file"). PDF/A-3 allows any number of embedded files;
/// each needs a MIME type, a modification date and a relationship to the document.
/// </summary>
public sealed class PdfAttachment
{
    /// <summary>Creates an attachment.</summary>
    /// <param name="fileName">File name shown by PDF readers, e.g. "factur-x.xml".</param>
    /// <param name="data">Content of the file.</param>
    /// <param name="mimeType">MIME type, e.g. "text/xml" or "application/pdf".</param>
    public PdfAttachment(string fileName, byte[] data, string mimeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        FileName = fileName;
        Data = data;
        MimeType = mimeType;
    }

    /// <summary>File name.</summary>
    public string FileName { get; }

    /// <summary>Content of the file.</summary>
    public byte[] Data { get; }

    /// <summary>MIME type.</summary>
    public string MimeType { get; }

    /// <summary>Description shown by PDF readers, or null.</summary>
    public string? Description { get; init; }

    /// <summary>Relationship to the document. Default: <see cref="AttachmentRelationship.Unspecified"/>.</summary>
    public AttachmentRelationship Relationship { get; init; } = AttachmentRelationship.Unspecified;

    /// <summary>Modification date of the file. Default: the creation date of the document.</summary>
    public DateTimeOffset? ModificationDate { get; init; }
}
