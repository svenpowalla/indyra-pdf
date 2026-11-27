// Output 1 of the layout engine: draws onto a page of the own PDF writer.
//
// For tagged documents (PdfDocument.Tagged) every drawing operation is marked:
//   - text and images inside an open structure element become marked content with an MCID,
//   - lines, filled areas and everything inside BeginArtifact/EndArtifact become artifacts.
// So no content stays untagged, as PDF/UA and PDF/A-3a require.

using IndyraPdf.Drawing;
using IndyraPdf.Writer.Tagging;

namespace IndyraPdf.Writer;

/// <summary>
/// Implements <see cref="IDrawingSurface"/> on a <see cref="PdfPage"/>. Fonts and images are
/// added to the <see cref="PdfDocument"/> on first use and shared by all pages.
/// </summary>
public sealed class PdfDrawingSurface : IDrawingSurface
{
    /// <summary>Document that owns fonts, images and the structure tree.</summary>
    readonly PdfDocument _document;

    /// <summary>Canvas of the page.</summary>
    readonly PdfCanvas _canvas;

    /// <summary>Index of the page in the document.</summary>
    readonly int _pageIndex;

    /// <summary>Structure tree of the document, or null if the document is not tagged.</summary>
    readonly StructureTree? _tree;

    /// <summary>Open structure elements; null entries are tags opened inside an artifact (ignored).</summary>
    readonly Stack<StructureElement?> _openElements = new();

    /// <summary>Number of open artifacts.</summary>
    int _artifactDepth;

    /// <summary>Creates a surface for one page of a document.</summary>
    /// <exception cref="ArgumentException">The page does not belong to the document.</exception>
    public PdfDrawingSurface(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        _document = document;
        _canvas = page.Canvas;
        _pageIndex = document.Pages.ToList().IndexOf(page);
        if (_pageIndex < 0)
            throw new ArgumentException("The page does not belong to the document.", nameof(page));
        _tree = document.StructureTree;
        Width = page.Width;
        Height = page.Height;
    }

    /// <inheritdoc/>
    public double Width { get; }

    /// <inheritdoc/>
    public double Height { get; }

    /// <inheritdoc/>
    public void SaveState() => _canvas.SaveState();

    /// <inheritdoc/>
    public void RestoreState() => _canvas.RestoreState();

    /// <inheritdoc/>
    public void Clip(Box box) => _canvas.ClipRectangle(box.X, box.Y, box.Width, box.Height);

    /// <inheritdoc/>
    public void FillRectangle(Box box, PdfColor color)
        => Mark(isContent: false, () => _canvas.SetFillColor(color).DrawRectangle(box.X, box.Y, box.Width, box.Height, PdfPaint.Fill));

    /// <inheritdoc/>
    public void StrokeRectangle(Box box, LineStyle style) => Mark(isContent: false, () =>
    {
        ApplyLineStyle(style);
        _canvas.DrawRectangle(box.X, box.Y, box.Width, box.Height, PdfPaint.Stroke);
    });

    /// <inheritdoc/>
    public void DrawLine(double x1, double y1, double x2, double y2, LineStyle style) => Mark(isContent: false, () =>
    {
        ApplyLineStyle(style);
        _canvas.DrawLine(x1, y1, x2, y2);
    });

    /// <inheritdoc/>
    /// <remarks>
    /// Glyphs of a color bitmap font (emoji) are written through Type3 fonts whose glyphs draw
    /// the pictures; they remain text for copying, searching and screen readers.
    /// </remarks>
    public void DrawGlyphs(GlyphRun run) => Mark(isContent: true, () =>
    {
        _canvas.SetFillColor(run.Color);
        if (run.Font.HasColorBitmaps)
            DrawColorGlyphs(run);
        else
            _canvas.DrawGlyphs(_document.AddFont(run.Font), run);
    });

    /// <inheritdoc/>
    public void DrawImage(ImageSource image, Box box)
        => Mark(isContent: true, () => _canvas.DrawImage(_document.AddImage(image), box.X, box.Y, box.Width, box.Height));

    /// <inheritdoc/>
    public void BeginTag(StructureTag tag, double top)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (_tree is null)
            return;
        if (_artifactDepth > 0)
        {
            _openElements.Push(null); // structure inside an artifact is not part of the tree
            return;
        }
        var parent = _openElements.FirstOrDefault(e => e is not null) ?? _tree.Root;
        _openElements.Push(_tree.GetOrCreate(tag, parent, _pageIndex, top));
    }

    /// <inheritdoc/>
    public void EndTag()
    {
        if (_tree is null)
            return;
        if (_openElements.Count == 0)
            throw new InvalidOperationException("EndTag called without a matching BeginTag.");
        _openElements.Pop();
    }

    /// <inheritdoc/>
    public void BeginArtifact() => _artifactDepth++;

    /// <inheritdoc/>
    public void EndArtifact()
    {
        if (_artifactDepth == 0)
            throw new InvalidOperationException("EndArtifact called without a matching BeginArtifact.");
        _artifactDepth--;
    }

    /// <summary>
    /// Draws with marking: real content inside an open element gets an MCID, everything else
    /// becomes an artifact. Untagged documents are drawn without marks.
    /// </summary>
    void Mark(bool isContent, Action draw)
    {
        if (_tree is null)
        {
            draw();
            return;
        }
        var owner = _openElements.Count > 0 ? _openElements.Peek() : null;
        if (isContent && _artifactDepth == 0 && owner is not null)
            _canvas.BeginMarkedContent(owner.Type, _tree.AddContent(owner, _pageIndex));
        else
            _canvas.BeginArtifactContent();
        draw();
        _canvas.EndMarkedContent();
    }

    /// <summary>
    /// Draws the glyphs of a color font. Each glyph gets a code in one of the document's Type3
    /// fonts; neighbouring glyphs in the same Type3 font are written as one text object.
    /// </summary>
    void DrawColorGlyphs(GlyphRun run)
    {
        Fonts.Type3Font? current = null;
        var codes = new List<GlyphCode>();
        foreach (var glyph in run.Glyphs)
        {
            var (font, code) = _document.UseColorGlyph(run.Font, glyph.GlyphId, glyph.Text);
            if (current is not null && !ReferenceEquals(font, current))
            {
                _canvas.DrawType3Glyphs(current, run.Size, run.X, run.Baseline, codes);
                codes = [];
            }
            current = font;
            codes.Add(new GlyphCode(code.ToString("X2", System.Globalization.CultureInfo.InvariantCulture), glyph.X, run.AdvanceOf(glyph.GlyphId)));
        }
        if (current is not null)
            _canvas.DrawType3Glyphs(current, run.Size, run.X, run.Baseline, codes);
    }

    /// <summary>Sets width, color and dash pattern for the next line.</summary>
    void ApplyLineStyle(LineStyle style)
    {
        _canvas.SetLineWidth(style.Width).SetStrokeColor(style.Color);
        if (style.DashPattern is { Count: > 0 } dash)
            _canvas.SetDashPattern([.. dash]);
        else
            _canvas.SetSolidLine();
    }
}
