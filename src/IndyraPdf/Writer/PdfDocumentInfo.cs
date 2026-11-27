// Document information: title, author, dates. Shown by PDF readers in the document properties.

using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>Document information (info dictionary, ISO 32000-2, 14.3.3).</summary>
public sealed class PdfDocumentInfo
{
    /// <summary>Title of the document.</summary>
    public string? Title { get; set; }

    /// <summary>Author.</summary>
    public string? Author { get; set; }

    /// <summary>Subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Application that created the content.</summary>
    public string? Creator { get; set; }

    /// <summary>Application that produced the PDF file.</summary>
    public string Producer { get; set; } = "IndyraPdf";

    /// <summary>
    /// Creation time. Default: the time the document object was created, without fractions of
    /// a second (PDF dates have a resolution of one second).
    /// </summary>
    public DateTimeOffset CreationDate { get; set; } = TruncateToSeconds(DateTimeOffset.Now);

    /// <summary>Modification time. Default: equal to the creation time.</summary>
    public DateTimeOffset? ModificationDate { get; set; }

    /// <summary>Modification time actually written (the creation time if none is set).</summary>
    internal DateTimeOffset EffectiveModificationDate => ModificationDate ?? CreationDate;

    /// <summary>Creates the info dictionary. Empty values are left out.</summary>
    internal PdfDictionary Build()
    {
        var info = new PdfDictionary();
        AddText(info, "Title", Title);
        AddText(info, "Author", Author);
        AddText(info, "Subject", Subject);
        AddText(info, "Keywords", Keywords);
        AddText(info, "Creator", Creator);
        AddText(info, "Producer", Producer);
        info["CreationDate"] = PdfString.FromAscii(PdfFormat.Date(CreationDate));
        info["ModDate"] = PdfString.FromAscii(PdfFormat.Date(EffectiveModificationDate));
        return info;
    }

    /// <summary>Adds a text entry if the value is not empty.</summary>
    static void AddText(PdfDictionary info, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            info[key] = PdfString.FromText(value);
    }

    /// <summary>Removes fractions of a second.</summary>
    static DateTimeOffset TruncateToSeconds(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);
}
