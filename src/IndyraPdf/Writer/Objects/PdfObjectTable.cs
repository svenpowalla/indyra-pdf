// Collects the indirect objects of a PDF file and writes the complete file.
//
// Layout of a PDF file (ISO 32000-2, section 7.5):
//   %PDF-1.7            header with version
//   %âãÏÓ               comment with bytes above 127: marks the file as binary
//   1 0 obj ... endobj  the numbered objects
//   xref                cross-reference table: byte position of every object
//   trailer << ... >>   entry points: catalog (/Root), document info (/Info), file ID (/ID)
//   startxref n         position of the xref table
//   %%EOF
// A PDF reader starts at the end of the file, finds the xref table and from there every object.

using System.Globalization;
using System.Security.Cryptography;

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// Collects the indirect objects of a file and writes them together with the cross-reference
/// table (xref) and the trailer.
/// </summary>
internal sealed class PdfObjectTable
{
    /// <summary>The registered objects; object number = position + 1.</summary>
    readonly List<PdfObject> _objects = [];

    /// <summary>Number of registered objects.</summary>
    public int Count => _objects.Count;

    /// <summary>Registers an object as indirect object and returns it (for chaining).</summary>
    public T Add<T>(T obj) where T : PdfObject
    {
        ArgumentNullException.ThrowIfNull(obj);
        _objects.Add(obj);
        obj.AssignReference(new PdfReference(_objects.Count));
        return obj;
    }

    /// <summary>Writes the complete file: header, objects, xref table and trailer.</summary>
    /// <param name="stream">Target.</param>
    /// <param name="version">PDF version, e.g. "1.7".</param>
    /// <param name="catalog">Document catalog (must be registered).</param>
    /// <param name="info">Document information (optional, must be registered).</param>
    public void WriteFile(Stream stream, string version, PdfDictionary catalog, PdfDictionary? info)
    {
        // The objects are first written into a buffer so that the file identifier (/ID) can be
        // computed from their content. Identical input therefore produces an identical file.
        using var body = new MemoryStream();
        var output = new PdfOutput(body);

        // Header. The second line contains bytes above 127 and marks the file as binary
        // (required by PDF/A, ISO 19005-3, 6.1.2).
        output.Write($"%PDF-{version}\n");
        output.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        var offsets = new long[_objects.Count];
        for (int i = 0; i < _objects.Count; i++)
        {
            offsets[i] = output.Position;
            output.Write((i + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
            _objects[i].WriteTo(output);
            output.Write("\nendobj\n");
        }

        var id = MD5.HashData(body.GetBuffer().AsSpan(0, (int)body.Length));

        // Cross-reference table. Every entry is exactly 20 bytes: 10-digit byte offset,
        // 5-digit generation, "n" (in use) or "f" (free), and the two-byte line end "\r\n".
        // Entry 0 is always the head of the list of free objects.
        long xrefPosition = output.Position;
        output.Write($"xref\n0 {_objects.Count + 1}\n");
        output.Write("0000000000 65535 f\r\n");
        foreach (var offset in offsets)
            output.Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n");

        var trailer = new PdfDictionary();
        trailer["Size"] = new PdfNumber(_objects.Count + 1);
        trailer["Root"] = catalog.Reference ?? throw new InvalidOperationException("The catalog is not registered.");
        if (info is not null)
            trailer["Info"] = info.Reference ?? throw new InvalidOperationException("The document information is not registered.");
        // /ID: two identifiers; both are equal for a newly created file (ISO 32000-2, 14.4).
        trailer["ID"] = new PdfArray().Add(PdfString.FromBytes(id)).Add(PdfString.FromBytes(id));

        output.Write("trailer\n");
        trailer.WriteTo(output);
        output.Write($"\nstartxref\n{xrefPosition}\n%%EOF\n");

        body.Position = 0;
        body.CopyTo(stream);
    }
}
