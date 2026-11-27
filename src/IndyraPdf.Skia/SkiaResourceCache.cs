// Skia typefaces and decoded images, created once per font or image and shared by all pages.

using IndyraPdf.Drawing;
using IndyraPdf.Fonts;
using SkiaSharp;

namespace IndyraPdf.Skia;

/// <summary>
/// Creates Skia typefaces from <see cref="TrueTypeFont"/>s and decodes <see cref="ImageSource"/>s,
/// each only once. Must be disposed to release the native Skia objects.
/// </summary>
internal sealed class SkiaResourceCache : IDisposable
{
    /// <summary>Font manager that creates typefaces from font file data (no system fonts needed).</summary>
    readonly SKFontManager _fontManager = SKFontManager.CreateDefault();

    /// <summary>Typeface per font.</summary>
    readonly Dictionary<TrueTypeFont, SKTypeface> _typefaces = new(ReferenceEqualityComparer.Instance);

    /// <summary>Decoded image per image source.</summary>
    readonly Dictionary<ImageSource, SKImage> _images = new(ReferenceEqualityComparer.Instance);

    /// <summary>Decoded picture per color emoji glyph (the font hands out each bitmap object once).</summary>
    readonly Dictionary<ColorBitmap, SKImage> _colorGlyphs = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Returns the typeface for a font. The typeface is created from the same font file the layout
    /// engine measured with, so glyph numbers and widths match exactly.
    /// </summary>
    /// <exception cref="InvalidOperationException">Skia cannot read the font.</exception>
    public SKTypeface Typeface(TrueTypeFont font)
    {
        if (!_typefaces.TryGetValue(font, out var typeface))
        {
            using var data = SKData.CreateCopy(font.FileData.Span);
            typeface = _fontManager.CreateTypeface(data, 0)
                ?? throw new InvalidOperationException($"Skia cannot read the font \"{font.FamilyName}\".");
            _typefaces[font] = typeface;
        }
        return typeface;
    }

    /// <summary>Returns the decoded image.</summary>
    /// <exception cref="InvalidOperationException">Skia cannot decode the image.</exception>
    public SKImage Image(ImageSource source)
    {
        if (!_images.TryGetValue(source, out var image))
        {
            image = SKImage.FromEncodedData(source.Data)
                ?? throw new InvalidOperationException("Skia cannot decode the image.");
            _images[source] = image;
        }
        return image;
    }

    /// <summary>Returns the decoded picture of a color emoji glyph.</summary>
    /// <exception cref="InvalidOperationException">Skia cannot decode the picture.</exception>
    public SKImage ColorGlyph(ColorBitmap bitmap)
    {
        if (!_colorGlyphs.TryGetValue(bitmap, out var image))
        {
            image = SKImage.FromEncodedData(bitmap.Png)
                ?? throw new InvalidOperationException("Skia cannot decode the picture of an emoji.");
            _colorGlyphs[bitmap] = image;
        }
        return image;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var typeface in _typefaces.Values)
            typeface.Dispose();
        foreach (var image in _images.Values)
            image.Dispose();
        foreach (var image in _colorGlyphs.Values)
            image.Dispose();
        _fontManager.Dispose();
    }
}
