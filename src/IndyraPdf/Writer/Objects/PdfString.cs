// PDF string object: text (title, author) or binary data (file identifier, color palette).

using System.Text;

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// A string (ISO 32000-2, 7.3.4). PDF knows two notations: literal "(Hello)" and hexadecimal
/// "&lt;48656C6C6F&gt;". Text containing only printable ASCII is written literally; other text
/// as UTF-16BE with byte order mark (7.9.2.2), binary data always as hex.
/// </summary>
internal sealed class PdfString : PdfObject
{
    /// <summary>The bytes of the string as they appear in the file.</summary>
    readonly byte[] _bytes;

    /// <summary>Whether to write the hexadecimal notation.</summary>
    readonly bool _hex;

    PdfString(byte[] bytes, bool hex)
    {
        _bytes = bytes;
        _hex = hex;
    }

    /// <summary>Creates a text string, e.g. for the document title.</summary>
    public static PdfString FromText(string text)
    {
        foreach (char c in text)
        {
            if (c < 0x20 || c > 0x7E)
            {
                // Not plain ASCII: UTF-16 big endian, preceded by the byte order mark FE FF.
                var utf16 = Encoding.BigEndianUnicode.GetBytes(text);
                var bytes = new byte[utf16.Length + 2];
                bytes[0] = 0xFE;
                bytes[1] = 0xFF;
                utf16.CopyTo(bytes, 2);
                return new PdfString(bytes, hex: true);
            }
        }
        return new PdfString(Encoding.ASCII.GetBytes(text), hex: false);
    }

    /// <summary>Creates a string from binary data (written as hex).</summary>
    public static PdfString FromBytes(byte[] bytes) => new(bytes, hex: true);

    /// <summary>Creates a byte string from ASCII text, written literally (e.g. dates, "Adobe").</summary>
    public static PdfString FromAscii(string text) => new(Encoding.ASCII.GetBytes(text), hex: false);

    /// <inheritdoc/>
    public override bool StartsWithDelimiter => true;

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output)
    {
        if (_hex)
        {
            output.Write("<" + Convert.ToHexString(_bytes) + ">");
            return;
        }

        // Literal notation: parentheses and backslash must be escaped with a backslash.
        var sb = new StringBuilder(_bytes.Length + 2);
        sb.Append('(');
        foreach (byte b in _bytes)
        {
            if (b is (byte)'(' or (byte)')' or (byte)'\\')
                sb.Append('\\');
            sb.Append((char)b);
        }
        sb.Append(')');
        output.Write(sb.ToString());
    }
}
