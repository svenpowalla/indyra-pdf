// Reads size and color space of JPEG images.
//
// PDF can display JPEG data directly (filter DCTDecode), so the file is embedded unchanged, as
// FPDF and PDFsharp do. Only the header is read to learn size and number of color components.
//
// A JPEG file is a sequence of "segments". Each starts with the byte FF and a marker byte;
// most are followed by a 2-byte length (which includes the length field itself).
// Relevant markers:
//   D8        start of image (no length)
//   C0–CF     start of frame (SOF): precision, height, width, number of components
//             (except C4, C8, CC, which are other segments)
//   EE        APP14: written by Adobe software; tells that CMYK data is stored inverted
//   DA        start of scan: the compressed image data follows
//
// Taken from PDFsharp 6.2.4: CMYK images with an Adobe APP14 segment are stored inverted by
// Photoshop, therefore /Decode [1 0 1 0 1 0 1 0] is set. FPDF shows such images with wrong colors.

using IndyraPdf.Internal;

namespace IndyraPdf.Writer.Images;

/// <summary>Reads the header of JPEG images.</summary>
internal static class JpegReader
{
    /// <summary>Decode array that inverts all four CMYK components.</summary>
    static readonly double[] InvertedCmyk = [1, 0, 1, 0, 1, 0, 1, 0];

    /// <summary>Whether the data starts like a JPEG file (FF D8 FF).</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> data)
        => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    /// <summary>Reads the header and returns the data for a PDF image object.</summary>
    /// <exception cref="ImageFormatException">The data is no supported JPEG image.</exception>
    public static ImageData Read(byte[] data)
    {
        if (!IsJpeg(data))
            throw new ImageFormatException("The data is not a JPEG image.");

        bool adobe = false;
        int pos = 2;
        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF)
                throw new ImageFormatException("The JPEG image is damaged.");
            byte marker = data[pos + 1];
            if (marker == 0xFF)
            {
                pos++; // fill byte
                continue;
            }
            if (marker == 0xD8 || marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                pos += 2; // markers without length
                continue;
            }

            int length = BigEndian.U16(data, pos + 2);
            int segment = pos + 4; // first byte after marker and length

            if (marker == 0xEE && IsAdobeSegment(data, segment))
                adobe = true;

            if (IsStartOfFrame(marker))
            {
                if (segment + 6 > data.Length)
                    break;
                if (marker is 0xC3 or 0xC7 or 0xCB or 0xCF)
                    throw new ImageFormatException("Lossless JPEG images are not supported by PDF.");

                // SOF segment: precision (1), height (2), width (2), number of components (1).
                int bits = data[segment];
                int height = BigEndian.U16(data, segment + 1);
                int width = BigEndian.U16(data, segment + 3);
                int components = data[segment + 5];

                // The Adobe segment may also follow the SOF segment.
                adobe = adobe || FindAdobeSegment(data, segment + length - 2);

                return new ImageData
                {
                    Width = width,
                    Height = height,
                    BitsPerComponent = bits,
                    ColorSpace = components switch
                    {
                        1 => ImageColorSpace.Gray,
                        3 => ImageColorSpace.Rgb,
                        4 => ImageColorSpace.Cmyk,
                        _ => throw new ImageFormatException($"JPEG images with {components} color components are not supported."),
                    },
                    Compression = ImageCompression.Jpeg,
                    Data = data,
                    Decode = components == 4 && adobe ? InvertedCmyk : null,
                };
            }

            if (marker == 0xDA)
                break; // image data starts without a SOF segment having been found
            pos = segment + length - 2;
        }
        throw new ImageFormatException("The JPEG image lacks size and color information (SOF segment).");
    }

    /// <summary>Start-of-frame markers C0–CF, except C4 (Huffman tables), C8 (reserved) and CC (arithmetic coding).</summary>
    static bool IsStartOfFrame(byte marker)
        => marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

    /// <summary>Whether an APP14 segment starting at <paramref name="segment"/> begins with "Adobe".</summary>
    static bool IsAdobeSegment(byte[] data, int segment)
        => segment + 5 <= data.Length && data.AsSpan(segment, 5).SequenceEqual("Adobe"u8);

    /// <summary>Searches the segments from <paramref name="pos"/> up to the image data for an Adobe APP14 segment.</summary>
    static bool FindAdobeSegment(byte[] data, int pos)
    {
        while (pos + 4 <= data.Length && data[pos] == 0xFF && data[pos + 1] != 0xDA)
        {
            if (data[pos + 1] == 0xEE && IsAdobeSegment(data, pos + 4))
                return true;
            pos += 2 + BigEndian.U16(data, pos + 2);
        }
        return false;
    }
}
