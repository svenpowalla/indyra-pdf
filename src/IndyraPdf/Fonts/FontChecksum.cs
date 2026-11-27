// Checksum used in TrueType files for every table and for the whole file.

using IndyraPdf.Internal;

namespace IndyraPdf.Fonts;

/// <summary>Computes TrueType checksums (OpenType specification, "Calculating checksums").</summary>
internal static class FontChecksum
{
    /// <summary>
    /// Sum of all big-endian 32-bit words, ignoring overflow. A trailing incomplete word is
    /// padded with zero bytes.
    /// </summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int i = 0;
        for (; i + 4 <= data.Length; i += 4)
            sum = unchecked(sum + BigEndian.U32(data, i));
        if (i < data.Length)
        {
            Span<byte> rest = stackalloc byte[4];
            rest.Clear();
            data[i..].CopyTo(rest);
            sum = unchecked(sum + BigEndian.U32(rest, 0));
        }
        return sum;
    }

    /// <summary>Rounds a length up to the next multiple of 4 (tables are 4-byte aligned).</summary>
    public static int Pad4(int length) => (length + 3) & ~3;
}
