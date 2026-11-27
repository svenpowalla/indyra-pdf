// Byte output for writing PDF files, tracking the current position.

using System.Text;

namespace IndyraPdf.Writer.Objects;

/// <summary>
/// Writes bytes to a stream and keeps track of the position. The positions of all objects are
/// needed for the cross-reference table (xref) at the end of the file.
/// </summary>
internal sealed class PdfOutput
{
    /// <summary>Target stream.</summary>
    readonly Stream _stream;

    /// <summary>Creates the output for a target stream.</summary>
    public PdfOutput(Stream stream) => _stream = stream;

    /// <summary>Number of bytes written so far.</summary>
    public long Position { get; private set; }

    /// <summary>
    /// Writes text. PDF syntax consists of bytes; every character must be in the range 0–255
    /// and is written as one byte (Latin-1).
    /// </summary>
    public void Write(string text) => Write(Encoding.Latin1.GetBytes(text));

    /// <summary>Writes bytes.</summary>
    public void Write(ReadOnlySpan<byte> bytes)
    {
        _stream.Write(bytes);
        Position += bytes.Length;
    }
}
