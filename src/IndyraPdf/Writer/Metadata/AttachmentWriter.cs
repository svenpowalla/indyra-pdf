// Writes embedded files ("associated files", ISO 32000-2, 14.13; PDF/A-3: ISO 19005-3, 6.8).
//
// Structure for each file:
//   EmbeddedFile stream  /Type/EmbeddedFile /Subtype/text#2Fxml (MIME type as name)
//                        /Params <</ModDate (D:…) /Size n /CheckSum <MD5>>>
//   File specification   /Type/Filespec /F (name) /UF (name) /Desc (…) /AFRelationship/Alternative
//                        /EF <</F stream /UF stream>>
// The catalog references all file specifications twice:
//   /AF [ … ]                                   associated files of the document (PDF/A-3)
//   /Names <</EmbeddedFiles <</Names [(name) spec …]>>>>   so that PDF readers list them

using System.Security.Cryptography;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Metadata;

/// <summary>Writes the objects for embedded files.</summary>
internal static class AttachmentWriter
{
    /// <summary>
    /// Registers all attachments and adds /AF and /Names to the catalog.
    /// </summary>
    /// <param name="table">Object table.</param>
    /// <param name="catalog">Document catalog.</param>
    /// <param name="attachments">The files.</param>
    /// <param name="defaultDate">Modification date for files without their own date.</param>
    /// <param name="compress">Whether to compress the file contents.</param>
    public static void Write(PdfObjectTable table, PdfDictionary catalog, IReadOnlyList<PdfAttachment> attachments,
                             DateTimeOffset defaultDate, bool compress)
    {
        if (attachments.Count == 0)
            return;

        var associated = new PdfArray();
        var names = new PdfArray();
        // The name tree requires the keys in sorted order (ISO 32000-2, 7.9.6).
        foreach (var attachment in attachments.OrderBy(a => a.FileName, StringComparer.Ordinal))
        {
            var specification = WriteAttachment(table, attachment, defaultDate, compress);
            associated.Add(specification);
            names.Add(PdfString.FromText(attachment.FileName)).Add(specification);
        }

        catalog["AF"] = associated;
        var embeddedFiles = new PdfDictionary();
        embeddedFiles["Names"] = names;
        var nameDictionary = new PdfDictionary();
        nameDictionary["EmbeddedFiles"] = embeddedFiles;
        catalog["Names"] = nameDictionary;
    }

    /// <summary>Registers the stream and the file specification of one attachment.</summary>
    static PdfDictionary WriteAttachment(PdfObjectTable table, PdfAttachment attachment, DateTimeOffset defaultDate, bool compress)
    {
        var parameters = new PdfDictionary();
        parameters["ModDate"] = PdfString.FromAscii(PdfFormat.Date(attachment.ModificationDate ?? defaultDate));
        parameters["Size"] = new PdfNumber(attachment.Data.Length);
        parameters["CheckSum"] = PdfString.FromBytes(MD5.HashData(attachment.Data));

        var file = table.Add(new PdfStream("EmbeddedFile", attachment.Data) { Compress = compress });
        file["Subtype"] = new PdfName(attachment.MimeType);
        file["Params"] = parameters;

        var streams = new PdfDictionary();
        streams["F"] = file;
        streams["UF"] = file;

        var specification = table.Add(new PdfDictionary("Filespec"));
        specification["F"] = PdfString.FromText(attachment.FileName);
        specification["UF"] = PdfString.FromText(attachment.FileName);
        if (!string.IsNullOrEmpty(attachment.Description))
            specification["Desc"] = PdfString.FromText(attachment.Description);
        specification["AFRelationship"] = new PdfName(attachment.Relationship.ToString());
        specification["EF"] = streams;
        return specification;
    }
}
