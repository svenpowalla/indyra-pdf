// Writes image data as PDF image object ("image XObject", ISO 32000-2, 8.9.5).
//
// Example of the written dictionary:
//   <</Type/XObject/Subtype/Image/Width 240/Height 160/ColorSpace/DeviceRGB
//     /BitsPerComponent 8/Filter/FlateDecode
//     /DecodeParms<</Predictor 15/Colors 3/BitsPerComponent 8/Columns 240>>
//     /SMask 12 0 R/Length 3456>>

using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Images;

/// <summary>Writes <see cref="ImageData"/> as PDF image object, including its soft mask.</summary>
internal static class ImageXObjectWriter
{
    /// <summary>PNG predictor value meaning "PNG filters, chosen per row" (ISO 32000-2, table 10).</summary>
    const int PngOptimumPredictor = 15;

    /// <summary>Registers the image (and its soft mask) in the table and returns the image stream.</summary>
    public static PdfStream Write(PdfObjectTable table, ImageData data)
    {
        // The data is already compressed, so the stream must not compress it again.
        var image = table.Add(new PdfStream("XObject", data.Data) { Compress = false });
        image["Subtype"] = new PdfName("Image");
        image["Width"] = new PdfNumber(data.Width);
        image["Height"] = new PdfNumber(data.Height);
        image["ColorSpace"] = ColorSpace(data);
        image["BitsPerComponent"] = new PdfNumber(data.BitsPerComponent);
        image["Filter"] = new PdfName(data.Compression == ImageCompression.Jpeg ? "DCTDecode" : "FlateDecode");

        if (data.PngPredictor)
        {
            var parameters = new PdfDictionary();
            parameters["Predictor"] = new PdfNumber(PngOptimumPredictor);
            parameters["Colors"] = new PdfNumber(data.Colors);
            parameters["BitsPerComponent"] = new PdfNumber(data.BitsPerComponent);
            parameters["Columns"] = new PdfNumber(data.Width);
            image["DecodeParms"] = parameters;
        }

        if (data.Decode is { } decode)
            image["Decode"] = new PdfArray([.. decode]);

        if (data.SoftMask is { } mask)
            image["SMask"] = Write(table, mask);

        return image;
    }

    /// <summary>
    /// Color space entry. Indexed images use [/Indexed /DeviceRGB highestIndex &lt;palette&gt;]
    /// (ISO 32000-2, 8.6.6.3); a palette has at most 256 entries.
    /// </summary>
    static PdfObject ColorSpace(ImageData data)
    {
        switch (data.ColorSpace)
        {
            case ImageColorSpace.Gray:
                return new PdfName("DeviceGray");
            case ImageColorSpace.Rgb:
                return new PdfName("DeviceRGB");
            case ImageColorSpace.Cmyk:
                return new PdfName("DeviceCMYK");
            default:
                var palette = data.Palette ?? throw new InvalidOperationException("Indexed image without palette.");
                int entries = Math.Min(palette.Length / 3, 256);
                return new PdfArray()
                    .Add(new PdfName("Indexed"))
                    .Add(new PdfName("DeviceRGB"))
                    .Add(new PdfNumber(entries - 1))
                    .Add(PdfString.FromBytes(palette[..(entries * 3)]));
        }
    }
}
