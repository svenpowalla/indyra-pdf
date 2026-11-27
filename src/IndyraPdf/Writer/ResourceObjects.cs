// The PDF objects of all fonts and images of a document, built once when saving.
// Every page then refers to the objects it uses (see PageResources).

using IndyraPdf.Writer.Fonts;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>Written font and image objects, looked up by the pages that use them.</summary>
/// <param name="Fonts">Font dictionary of every TrueType font used.</param>
/// <param name="Type3Fonts">Font dictionary of every Type3 font (color emoji) used.</param>
/// <param name="Images">Image object of every image used.</param>
internal sealed record ResourceObjects(
    IReadOnlyDictionary<PdfFont, PdfDictionary> Fonts,
    IReadOnlyDictionary<Type3Font, PdfDictionary> Type3Fonts,
    IReadOnlyDictionary<PdfImage, PdfStream> Images);
