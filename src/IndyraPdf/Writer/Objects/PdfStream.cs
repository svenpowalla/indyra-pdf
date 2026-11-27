// PDF stream object: a dictionary followed by binary data (page content, fonts, images).

using IndyraPdf.Internal;

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// A stream (ISO 32000-2, 7.3.8): dictionary, then the keyword "stream", the data and
/// "endstream". The dictionary must state the length of the data (/Length) and, if the data is
/// compressed, the filter (/Filter).
/// </summary>
/// <remarks>
/// If <see cref="Compress"/> is set and no filter is given, the data is compressed with Flate
/// while writing. Writing does not change the object: /Filter and /Length are only added to
/// the output.
/// </remarks>
internal sealed class PdfStream : PdfDictionary
{
    /// <summary>Creates a stream without /Type entry.</summary>
    public PdfStream(byte[] data) => Data = data;

    /// <summary>Creates a stream with a /Type entry, e.g. "XObject".</summary>
    public PdfStream(string type, byte[] data) : base(type) => Data = data;

    /// <summary>The uncompressed data, or data already compressed if a /Filter entry is set.</summary>
    public byte[] Data { get; }

    /// <summary>Compress the data when writing, unless a filter is already set. Default: true.</summary>
    public bool Compress { get; init; } = true;

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output)
    {
        // Streams are always indirect objects (ISO 32000-2, 7.3.8.1).
        if (Reference is null)
            throw new InvalidOperationException("A stream must be an indirect object.");

        bool compress = Compress && !ContainsKey("Filter");
        var data = compress ? Zlib.Compress(Data) : Data;

        output.Write("<<");
        WriteEntries(output);
        if (compress)
            WriteEntry(output, "Filter", new PdfName("FlateDecode"));
        WriteEntry(output, "Length", new PdfNumber(data.Length));
        output.Write(">>\nstream\n");
        output.Write(data);
        output.Write("\nendstream");
    }
}
