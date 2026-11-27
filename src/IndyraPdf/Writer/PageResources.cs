// Fonts, images and opacity settings used on one page.
//
// Every PDF page lists the resources its content refers to (ISO 32000-2, 7.8.3), e.g.
//   /Resources <</Font<</F1 7 0 R>>/XObject<</Im1 9 0 R>>/ExtGState<</GS1 <</ca 0.5/CA 1>>>>>>
// The content stream then uses the short names: "/F1 12 Tf", "/Im1 Do" or "/GS1 gs".
// Type3 fonts for color emoji ("/T1") are listed under /Font as well.

using IndyraPdf.Writer.Fonts;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>Tracks the fonts, images and opacity settings used on a page and builds its resource dictionary.</summary>
internal sealed class PageResources
{
    /// <summary>Fonts used on the page.</summary>
    readonly HashSet<PdfFont> _fonts = [];

    /// <summary>Type3 fonts (color emoji) used on the page.</summary>
    readonly HashSet<Type3Font> _type3Fonts = [];

    /// <summary>Images used on the page.</summary>
    readonly HashSet<PdfImage> _images = [];

    /// <summary>
    /// Opacity settings used on the page: pair of fill and stroke opacity → resource name
    /// ("GS1", "GS2", …, numbered in the order of first use, so the output is reproducible).
    /// </summary>
    readonly Dictionary<(double Fill, double Stroke), string> _opacities = [];

    /// <summary>Records that the page uses a font.</summary>
    public void Use(PdfFont font) => _fonts.Add(font);

    /// <summary>Records that the page uses a Type3 font.</summary>
    public void Use(Type3Font font) => _type3Fonts.Add(font);

    /// <summary>Records that the page uses an image.</summary>
    public void Use(PdfImage image) => _images.Add(image);

    /// <summary>Whether the page uses the Type3 font.</summary>
    public bool Contains(Type3Font font) => _type3Fonts.Contains(font);

    /// <summary>
    /// Records that the page uses an opacity setting and returns its resource name for the
    /// operator "gs". Equal pairs share one entry.
    /// </summary>
    /// <param name="fill">Opacity of fills, text and images (/ca).</param>
    /// <param name="stroke">Opacity of lines (/CA).</param>
    public string UseOpacity(double fill, double stroke)
    {
        if (!_opacities.TryGetValue((fill, stroke), out var name))
        {
            name = "GS" + (_opacities.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _opacities.Add((fill, stroke), name);
        }
        return name;
    }

    /// <summary>Whether the page uses the font.</summary>
    public bool Contains(PdfFont font) => _fonts.Contains(font);

    /// <summary>Whether the page uses the image.</summary>
    public bool Contains(PdfImage image) => _images.Contains(image);

    /// <summary>
    /// Builds the resource dictionary. The entries are sorted by name, so the output does not
    /// depend on the order of a hash set.
    /// </summary>
    /// <param name="objects">The written font and image objects of the document.</param>
    public PdfDictionary Build(ResourceObjects objects)
    {
        var resources = new PdfDictionary();
        if (_fonts.Count + _type3Fonts.Count > 0)
        {
            var fonts = new PdfDictionary();
            foreach (var font in _fonts.OrderBy(f => f.ResourceName, StringComparer.Ordinal))
                fonts[font.ResourceName] = objects.Fonts[font];
            foreach (var font in _type3Fonts.OrderBy(f => f.ResourceName, StringComparer.Ordinal))
                fonts[font.ResourceName] = objects.Type3Fonts[font];
            resources["Font"] = fonts;
        }
        if (_images.Count > 0)
        {
            var xobjects = new PdfDictionary();
            foreach (var image in _images.OrderBy(i => i.ResourceName, StringComparer.Ordinal))
                xobjects[image.ResourceName] = objects.Images[image];
            resources["XObject"] = xobjects;
        }
        if (_opacities.Count > 0)
        {
            // Graphics state parameter dictionaries (ISO 32000-2, 8.4.5), written directly into
            // the resources because they are tiny: /ca = opacity of fills, /CA = of strokes.
            var states = new PdfDictionary();
            foreach (var ((fill, stroke), name) in _opacities)
            {
                var state = new PdfDictionary("ExtGState");
                state["ca"] = new PdfNumber(fill);
                state["CA"] = new PdfNumber(stroke);
                states[name] = state;
            }
            resources["ExtGState"] = states;
        }
        return resources;
    }
}
