// The abstract drawing surface: the interface between the layout engine and its outputs.
//
// The layout engine (IndyraPdf.Layout) computes positions and draws only through this interface.
// Two implementations exist (see the architecture in the README):
//   - PdfDrawingSurface: draws onto a page of the own PDF writer,
//   - a SkiaSharp surface for PNG previews (stage 4).
// Both receive exactly the same calls, so PDF and preview are identical.
//
// Coordinates are in points, origin in the upper left corner of the page, y grows downwards.

using IndyraPdf.Writer;

namespace IndyraPdf.Drawing;

/// <summary>Output-independent drawing operations used by the layout engine.</summary>
public interface IDrawingSurface
{
    /// <summary>Width of the page in points.</summary>
    double Width { get; }

    /// <summary>Height of the page in points.</summary>
    double Height { get; }

    /// <summary>Saves the drawing state (clipping).</summary>
    void SaveState();

    /// <summary>Restores the state saved last.</summary>
    void RestoreState();

    /// <summary>Restricts all following output to a rectangle until <see cref="RestoreState"/>.</summary>
    void Clip(Box box);

    /// <summary>Fills a rectangle with a color.</summary>
    void FillRectangle(Box box, PdfColor color);

    /// <summary>Draws the outline of a rectangle.</summary>
    void StrokeRectangle(Box box, LineStyle style);

    /// <summary>Draws a straight line.</summary>
    void DrawLine(double x1, double y1, double x2, double y2, LineStyle style);

    /// <summary>Draws positioned glyphs.</summary>
    void DrawGlyphs(GlyphRun run);

    /// <summary>Draws an image scaled into a rectangle.</summary>
    void DrawImage(ImageSource image, Box box);

    // ----- Structure for tagged PDF -----
    // Surfaces without a structure concept (e.g. the PNG preview) ignore these calls, hence the
    // empty default implementations.

    /// <summary>
    /// Opens a structure element. Text and images drawn until <see cref="EndTag"/> belong to it.
    /// Lines and filled areas are always treated as decoration (artifacts).
    /// </summary>
    /// <param name="tag">The element.</param>
    /// <param name="top">Y position where the element starts on the current page (for the reading order).</param>
    void BeginTag(StructureTag tag, double top) { }

    /// <summary>Closes the structure element opened last.</summary>
    void EndTag() { }

    /// <summary>
    /// Starts content that is pure decoration or repetition (an artifact), e.g. page numbers or
    /// table headers repeated on following pages. Screen readers skip it.
    /// </summary>
    void BeginArtifact() { }

    /// <summary>Ends the artifact started last.</summary>
    void EndArtifact() { }
}
