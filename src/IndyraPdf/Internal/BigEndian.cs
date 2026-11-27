// Reading and writing numbers in big-endian byte order.
//
// TrueType, PNG and JPEG files store multi-byte numbers with the most significant byte first
// ("big endian"): the number 0x1234 is stored as the bytes 12 34. Intel and ARM processors use
// the opposite order. These helpers hide the conversion, so the format readers only have to
// name byte positions.
//
// Naming: U16 = unsigned 16 bit, I16 = signed 16 bit, U32 / I32 accordingly.

using System.Buffers.Binary;

namespace IndyraPdf.Internal;

/// <summary>
/// Reads and writes big-endian values as found in TrueType, PNG and JPEG files.
/// Every method takes the position of the first byte of the number.
/// </summary>
internal static class BigEndian
{
    /// <summary>Reads an unsigned 16-bit number.</summary>
    public static ushort U16(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));

    /// <summary>Reads a signed 16-bit number.</summary>
    public static short I16(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset, 2));

    /// <summary>Reads an unsigned 32-bit number.</summary>
    public static uint U32(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));

    /// <summary>Reads a signed 32-bit number.</summary>
    public static int I32(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));

    /// <summary>Writes an unsigned 16-bit number. Only the lower 16 bits of <paramref name="value"/> are used.</summary>
    public static void WriteU16(Span<byte> data, int offset, int value)
        => BinaryPrimitives.WriteUInt16BigEndian(data.Slice(offset, 2), (ushort)value);

    /// <summary>Writes an unsigned 32-bit number.</summary>
    public static void WriteU32(Span<byte> data, int offset, uint value)
        => BinaryPrimitives.WriteUInt32BigEndian(data.Slice(offset, 4), value);
}
