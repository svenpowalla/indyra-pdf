// PDF dictionary object, written as << /Key value ... >>.

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// A dictionary with names as keys (ISO 32000-2, 7.3.7). Most PDF structures are dictionaries,
/// e.g. a page: &lt;&lt;/Type/Page/MediaBox[0 0 595 842]/Contents 5 0 R&gt;&gt;.
/// The order of the entries is kept, which makes the output predictable.
/// </summary>
internal class PdfDictionary : PdfObject
{
    /// <summary>The entries in insertion order. Keys are names without slash.</summary>
    readonly List<KeyValuePair<string, PdfObject>> _entries = [];

    /// <summary>Creates an empty dictionary.</summary>
    public PdfDictionary() { }

    /// <summary>Creates a dictionary with a /Type entry, e.g. "Page" or "Font".</summary>
    public PdfDictionary(string type) => this["Type"] = new PdfName(type);

    /// <summary>Gets or sets an entry. Setting null removes it.</summary>
    public PdfObject? this[string key]
    {
        get
        {
            int index = IndexOf(key);
            return index >= 0 ? _entries[index].Value : null;
        }
        set
        {
            int index = IndexOf(key);
            if (value is null)
            {
                if (index >= 0)
                    _entries.RemoveAt(index);
            }
            else if (index >= 0)
            {
                _entries[index] = new(key, value);
            }
            else
            {
                _entries.Add(new(key, value));
            }
        }
    }

    /// <summary>Number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Whether the dictionary contains the key.</summary>
    public bool ContainsKey(string key) => IndexOf(key) >= 0;

    /// <inheritdoc/>
    public override bool StartsWithDelimiter => true;

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output)
    {
        output.Write("<<");
        WriteEntries(output);
        output.Write(">>");
    }

    /// <summary>Writes all entries without the surrounding brackets.</summary>
    protected void WriteEntries(PdfOutput output)
    {
        foreach (var (key, value) in _entries)
            WriteEntry(output, key, value);
    }

    /// <summary>
    /// Writes one entry "/Key value". A space between key and value is only needed if the value
    /// does not start with a delimiter (numbers, references, true/false).
    /// </summary>
    protected static void WriteEntry(PdfOutput output, string key, PdfObject value)
    {
        output.Write(PdfFormat.Name(key));
        bool delimited = value.Reference is null && value.StartsWithDelimiter;
        if (!delimited)
            output.Write(" ");
        value.WriteValueTo(output);
    }

    /// <summary>Position of a key in the entry list, or −1.</summary>
    int IndexOf(string key) => _entries.FindIndex(e => e.Key == key);
}
