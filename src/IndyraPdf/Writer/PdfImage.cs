// An image (PNG or JPEG) as used inside one PDF document.
// It is stored once in the file, however often it is drawn.

using IndyraPdf.Writer.Images;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>An image (PNG or JPEG) in a <see cref="PdfDocument"/>.</summary>
public sealed class PdfImage
{
    /// <summary>The prepared image data.</summary>
    readonly ImageData _data;

    /// <summary>Reads the image. Only <see cref="PdfDocument.AddImage(byte[])"/> calls this.</summary>
    /// <param name="bytes">Bytes of a PNG or JPEG file.</param>
    /// <param name="resourceName">Name in the page resources, e.g. "Im1".</param>
    /// <exception cref="ImageFormatException">The format is unknown or the file is damaged.</exception>
    internal PdfImage(byte[] bytes, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (PngReader.IsPng(bytes))
            _data = PngReader.Read(bytes);
        else if (JpegReader.IsJpeg(bytes))
            _data = JpegReader.Read(bytes);
        else
            throw new ImageFormatException("Unknown image format. PNG and JPEG are supported.");
        ResourceName = resourceName;
    }

    /// <summary>Width in pixels.</summary>
    public int PixelWidth => _data.Width;

    /// <summary>Height in pixels.</summary>
    public int PixelHeight => _data.Height;

    /// <summary>Whether the image contains transparency.</summary>
    public bool HasTransparency => _data.SoftMask is not null;

    /// <summary>Whether the image is stored in CMYK (not allowed in PDF/A with sRGB output intent).</summary>
    public bool IsCmyk => _data.ColorSpace == ImageColorSpace.Cmyk;

    /// <summary>Name under which pages refer to the image, e.g. "Im1" (written as /Im1).</summary>
    internal string ResourceName { get; }

    /// <summary>Creates the PDF image object (and its soft mask).</summary>
    internal PdfStream Build(PdfObjectTable table) => ImageXObjectWriter.Write(table, _data);
}
