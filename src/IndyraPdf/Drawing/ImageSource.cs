// An image file (PNG or JPEG) independent of any output format.

using IndyraPdf.Writer;
using IndyraPdf.Writer.Images;

namespace IndyraPdf.Drawing;

/// <summary>
/// An image file (PNG or JPEG) for the layout engine. Each drawing surface converts it into its
/// own representation (a PDF image object, a SkiaSharp bitmap).
/// </summary>
public sealed class ImageSource
{
    /// <summary>Reads the image header to learn the pixel size.</summary>
    /// <exception cref="ImageFormatException">The data is no supported PNG or JPEG image.</exception>
    public ImageSource(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        if (PngFile.IsPng(data))
        {
            var png = PngFile.Parse(data);
            (PixelWidth, PixelHeight) = (png.Width, png.Height);
        }
        else if (JpegReader.IsJpeg(data))
        {
            var jpeg = JpegReader.Read(data);
            (PixelWidth, PixelHeight) = (jpeg.Width, jpeg.Height);
        }
        else
        {
            throw new ImageFormatException("Unknown image format. PNG and JPEG are supported.");
        }
    }

    /// <summary>The bytes of the image file.</summary>
    public byte[] Data { get; }

    /// <summary>Width in pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>Height in pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>Reads an image from a file.</summary>
    public static ImageSource FromFile(string path) => new(File.ReadAllBytes(path));
}
