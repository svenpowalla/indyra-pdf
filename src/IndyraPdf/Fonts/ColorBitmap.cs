// The picture of one glyph in a color bitmap font (e.g. Noto Color Emoji).
//
// Color bitmap fonts contain no outlines. Every glyph is a small PNG image drawn for a fixed
// size, the "pixels per em" (ppem): at ppem 109, 109 pixels correspond to the font size.
// The metrics place the image relative to the glyph origin on the baseline, like a letter.

namespace IndyraPdf.Fonts;

/// <summary>PNG image and placement of one glyph of a color bitmap font.</summary>
/// <param name="Png">Bytes of the PNG file.</param>
/// <param name="PixelWidth">Width of the image in pixels.</param>
/// <param name="PixelHeight">Height of the image in pixels.</param>
/// <param name="BearingX">Distance from the glyph origin to the left edge of the image, in pixels.</param>
/// <param name="BearingY">Distance from the baseline up to the top edge of the image, in pixels.</param>
/// <param name="PixelsPerEm">Number of pixels that correspond to the font size.</param>
internal sealed record ColorBitmap(byte[] Png, int PixelWidth, int PixelHeight, int BearingX, int BearingY, int PixelsPerEm)
{
    /// <summary>
    /// Position and size of the image at a font size, in points: <c>Left</c> from the glyph
    /// origin to the right, <c>Top</c> from the baseline upwards, then width and height.
    /// PDF output and preview both use this, so the picture lands at the same spot.
    /// </summary>
    public (double Left, double Top, double Width, double Height) Placement(double fontSize)
    {
        double scale = fontSize / PixelsPerEm;
        return (BearingX * scale, BearingY * scale, PixelWidth * scale, PixelHeight * scale);
    }
}
