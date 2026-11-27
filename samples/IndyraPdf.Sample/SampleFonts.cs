// The three fonts used by the sample program.

using IndyraPdf.Writer;

namespace IndyraPdf.Sample;

/// <summary>The fonts used by the sample program.</summary>
/// <param name="Regular">Body text.</param>
/// <param name="Bold">Headings and table header.</param>
/// <param name="SerifItalic">Emphasis.</param>
internal sealed record SampleFonts(PdfFont Regular, PdfFont Bold, PdfFont SerifItalic);
