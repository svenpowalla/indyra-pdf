// Drawing surface of a page: produces the page content ("content stream").
//
// The content of a PDF page is a small program of drawing operators (ISO 32000-2, chapter 8 and 9).
// Operands come first, then the operator, e.g. "10 20 100 50 re f" draws a filled rectangle.
// Operators used here:
//
//   q / Q          save / restore the graphics state (colors, line style, clipping, transformation)
//   g / G          fill / stroke color as gray
//   rg / RG        fill / stroke color as RGB
//   k / K          fill / stroke color as CMYK
//   w              line width          J  line cap     j  line join     d  dash pattern
//   m              move to (start of a subpath)        l  line to       c  Bézier curve
//   h              close subpath       re rectangle (x y width height)
//   S              stroke              f  fill         B  fill and stroke
//   W n            use the current path as clipping region (and do not paint it)
//   BT / ET        begin / end text
//   Tf             font and size       Tm text position (matrix)   Tj show glyph codes
//   cm             change the coordinate system (matrix)
//   Do             draw an image (or other XObject)
//   gs             apply a graphics state parameter dictionary from the resources (here: opacity)
//
// Opacity: colors with an alpha below 1 switch the opacity through "/GSn gs" (see PageResources).
// The canvas remembers the current opacity, also across q/Q, and writes "gs" only when it changes.
// Documents without transparent colors therefore contain no "gs" at all.
//
// Coordinates: PDF places the origin in the lower left corner, y grows upwards. IndyraPdf uses
// the origin in the upper left corner with y growing downwards, like PDFsharp and SkiaSharp,
// because documents are laid out from top to bottom. As in FPDF, every coordinate is converted
// individually (y_pdf = page height − y). Text is therefore not mirrored.

using System.Globalization;
using System.Text;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>
/// Drawing surface of a <see cref="PdfPage"/>. Coordinates are in points, origin in the upper
/// left corner, y grows downwards. All drawing methods return the canvas, so calls can be chained.
/// </summary>
public sealed class PdfCanvas
{
    /// <summary>Factor 4/3·(√2−1) for approximating a quarter circle with a cubic Bézier curve.</summary>
    const double BezierCircleFactor = 0.5522847498;

    /// <summary>The content stream built so far.</summary>
    readonly StringBuilder _content = new();

    /// <summary>Page height, needed to convert y coordinates.</summary>
    readonly double _pageHeight;

    /// <summary>Number of currently open <see cref="SaveState"/> calls.</summary>
    int _stateDepth;

    /// <summary>Current opacity of fills, text and images in the graphics state (/ca).</summary>
    double _fillAlpha = 1;

    /// <summary>Current opacity of lines in the graphics state (/CA).</summary>
    double _strokeAlpha = 1;

    /// <summary>
    /// Opacities saved by <see cref="SaveState"/>. "Q" restores the opacity of the viewer's
    /// graphics state, so the canvas must restore its own record of it in step.
    /// </summary>
    readonly Stack<(double Fill, double Stroke)> _savedAlphas = new();

    /// <summary>Creates the canvas. Only <see cref="PdfPage"/> calls this.</summary>
    internal PdfCanvas(double pageHeight) => _pageHeight = pageHeight;

    /// <summary>Fonts and images used on this page.</summary>
    internal PageResources Resources { get; } = new();

    /// <summary>Whether a CMYK colour was set (not allowed in PDF/A with sRGB output intent).</summary>
    internal bool UsesCmyk { get; private set; }

    /// <summary>
    /// Returns the finished content stream. Graphics states left open are closed, so the content
    /// stays valid; the canvas itself is not changed.
    /// </summary>
    internal byte[] GetContent()
    {
        var content = new StringBuilder(_content.ToString());
        for (int i = 0; i < _stateDepth; i++)
            content.Append("Q\n");
        return Encoding.Latin1.GetBytes(content.ToString());
    }

    // ----- Graphics state -----

    /// <summary>Saves the graphics state (colors, line style, clipping).</summary>
    public PdfCanvas SaveState()
    {
        _stateDepth++;
        _savedAlphas.Push((_fillAlpha, _strokeAlpha));
        return Operator("q");
    }

    /// <summary>Restores the graphics state saved last.</summary>
    /// <exception cref="InvalidOperationException">There is no saved state.</exception>
    public PdfCanvas RestoreState()
    {
        if (_stateDepth == 0)
            throw new InvalidOperationException("RestoreState called without a preceding SaveState.");
        _stateDepth--;
        (_fillAlpha, _strokeAlpha) = _savedAlphas.Pop();
        return Operator("Q");
    }

    /// <summary>Sets the color and opacity for lines and outlines.</summary>
    public PdfCanvas SetStrokeColor(PdfColor color) => SetColor(color, stroke: true);

    /// <summary>Sets the color and opacity for fills and text.</summary>
    public PdfCanvas SetFillColor(PdfColor color) => SetColor(color, stroke: false);

    /// <summary>Sets the line width in points.</summary>
    public PdfCanvas SetLineWidth(double width) => Operator(Number(width), "w");

    /// <summary>Sets the shape of line ends.</summary>
    public PdfCanvas SetLineCap(PdfLineCap cap) => Operator(((int)cap).ToString(CultureInfo.InvariantCulture), "J");

    /// <summary>Sets the shape of line corners.</summary>
    public PdfCanvas SetLineJoin(PdfLineJoin join) => Operator(((int)join).ToString(CultureInfo.InvariantCulture), "j");

    /// <summary>
    /// Sets a dashed line: alternating dash and gap lengths in points, e.g. [4, 2].
    /// <paramref name="phase"/> shifts the start of the pattern.
    /// </summary>
    public PdfCanvas SetDashPattern(double[] pattern, double phase = 0)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        string array = "[" + string.Join(' ', pattern.Select(Number)) + "]";
        return Operator(array, Number(phase), "d");
    }

    /// <summary>Sets a solid line (empty dash pattern).</summary>
    public PdfCanvas SetSolidLine() => Operator("[]", "0", "d");

    // ----- Lines and shapes -----

    /// <summary>Draws a straight line.</summary>
    public PdfCanvas DrawLine(double x1, double y1, double x2, double y2)
        => MoveTo(x1, y1).LineTo(x2, y2).Operator("S");

    /// <summary>Draws a rectangle. (x, y) is the upper left corner.</summary>
    public PdfCanvas DrawRectangle(double x, double y, double width, double height, PdfPaint paint = PdfPaint.Stroke)
    {
        // "re" expects the lower left corner in PDF coordinates.
        Operator(Number(x), Number(ToPdfY(y + height)), Number(width), Number(height), "re");
        return Paint(paint);
    }

    /// <summary>Draws an ellipse inside the given rectangle. (x, y) is the upper left corner.</summary>
    public PdfCanvas DrawEllipse(double x, double y, double width, double height, PdfPaint paint = PdfPaint.Stroke)
    {
        // Four Bézier curves, one per quarter, starting at the rightmost point.
        double rx = width / 2, ry = height / 2;
        double cx = x + rx, cy = y + ry;
        double kx = BezierCircleFactor * rx, ky = BezierCircleFactor * ry;
        MoveTo(cx + rx, cy);
        CurveTo(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry);
        CurveTo(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy);
        CurveTo(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry);
        CurveTo(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy);
        ClosePath();
        return Paint(paint);
    }

    // ----- Paths -----

    /// <summary>Starts a new subpath at the given point.</summary>
    public PdfCanvas MoveTo(double x, double y) => Operator(Number(x), Number(ToPdfY(y)), "m");

    /// <summary>Adds a straight line to the given point.</summary>
    public PdfCanvas LineTo(double x, double y) => Operator(Number(x), Number(ToPdfY(y)), "l");

    /// <summary>Adds a cubic Bézier curve with two control points to the end point (x3, y3).</summary>
    public PdfCanvas CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        => Operator(Number(x1), Number(ToPdfY(y1)), Number(x2), Number(ToPdfY(y2)), Number(x3), Number(ToPdfY(y3)), "c");

    /// <summary>Closes the current subpath with a line back to its start.</summary>
    public PdfCanvas ClosePath() => Operator("h");

    /// <summary>Paints the path built so far.</summary>
    public PdfCanvas Paint(PdfPaint paint) => Operator(paint switch
    {
        PdfPaint.Fill => "f",
        PdfPaint.FillAndStroke => "B",
        _ => "S",
    });

    /// <summary>
    /// Restricts all following output to a rectangle. To lift the restriction, call
    /// <see cref="SaveState"/> before and <see cref="RestoreState"/> afterwards.
    /// </summary>
    public PdfCanvas ClipRectangle(double x, double y, double width, double height)
        => Operator(Number(x), Number(ToPdfY(y + height)), Number(width), Number(height), "re", "W", "n");

    // ----- Text -----

    /// <summary>
    /// Writes one line of text. (x, y) is the start of the baseline. The text color is the fill
    /// color (<see cref="SetFillColor"/>).
    /// </summary>
    public PdfCanvas DrawText(PdfFont font, double size, double x, double y, string text)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);
        Resources.Use(font);
        Operator("BT");
        Operator(PdfFormat.Name(font.ResourceName), Number(size), "Tf");
        // Text matrix "1 0 0 1 x y": no rotation or scaling, origin at (x, y).
        Operator("1 0 0 1", Number(x), Number(ToPdfY(y)), "Tm");
        Operator("<" + font.EncodeAsHex(text) + ">", "Tj");
        return Operator("ET");
    }

    /// <summary>
    /// Draws glyphs at exact positions, as computed by the layout engine. The text color is the
    /// fill color.
    /// </summary>
    /// <remarks>
    /// Uses the operator TJ (ISO 32000-2, 9.4.3): an array of glyph strings and numbers. A number
    /// moves the next glyph to the left by number/1000 of the font size (negative = to the right).
    /// Only where a glyph deviates from its natural position (justification, later kerning) a
    /// number is written, so normal text stays compact.
    /// </remarks>
    public PdfCanvas DrawGlyphs(PdfFont font, Drawing.GlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(run);
        if (run.Glyphs.Count == 0)
            return this;
        Resources.Use(font);
        var codes = run.Glyphs
            .Select(g => new GlyphCode(font.EncodeGlyphAsHex(g.GlyphId, g.Text), g.X, run.AdvanceOf(g.GlyphId)))
            .ToList();
        return DrawCodes(font.ResourceName, run.Size, run.X, run.Baseline, codes);
    }

    /// <summary>
    /// Draws glyphs of a Type3 font (color emoji) at exact positions, like
    /// <see cref="DrawGlyphs(PdfFont, Drawing.GlyphRun)"/>. The codes are one byte each.
    /// </summary>
    /// <param name="font">The Type3 font holding the glyphs.</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="x">Start of the run on the baseline.</param>
    /// <param name="baseline">Y coordinate of the baseline.</param>
    /// <param name="codes">Codes with positions relative to <paramref name="x"/>.</param>
    internal PdfCanvas DrawType3Glyphs(Fonts.Type3Font font, double size, double x, double baseline, IReadOnlyList<GlyphCode> codes)
    {
        if (codes.Count == 0)
            return this;
        Resources.Use(font);
        return DrawCodes(font.ResourceName, size, x, baseline, codes);
    }

    /// <summary>
    /// Writes one text object "BT … TJ ET" with glyph codes at exact positions. Only where a glyph
    /// deviates from the position its predecessor's advance would give, a TJ number is written.
    /// </summary>
    PdfCanvas DrawCodes(string fontResource, double size, double x, double baseline, IReadOnlyList<GlyphCode> codes)
    {
        // Deviations below 1/1000 pt are invisible and are not written.
        const double tolerance = 0.001;
        var array = new StringBuilder("[<");
        double naturalX = codes[0].X; // where the next glyph would land without adjustment
        foreach (var code in codes)
        {
            double shift = code.X - naturalX;
            if (Math.Abs(shift) > tolerance)
                array.Append("> ").Append(Number(-shift * 1000 / size)).Append(" <");
            array.Append(code.Hex);
            naturalX = code.X + code.Advance;
        }
        array.Append(">]");

        Operator("BT");
        Operator(PdfFormat.Name(fontResource), Number(size), "Tf");
        Operator("1 0 0 1", Number(x + codes[0].X), Number(ToPdfY(baseline)), "Tm");
        Operator(array.ToString(), "TJ");
        return Operator("ET");
    }

    // ----- Images -----

    /// <summary>
    /// Draws an image into the given rectangle. (x, y) is the upper left corner. The image is
    /// always drawn opaque (apart from its own transparency), even after a transparent fill color.
    /// </summary>
    public PdfCanvas DrawImage(PdfImage image, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(image);
        Resources.Use(image);
        // An image always fills the unit square 1 × 1. The matrix "w 0 0 h x y" scales it to
        // w × h and moves it to (x, y), the lower left corner in PDF coordinates.
        SaveState();
        // Images are painted with the fill opacity (/ca); a transparent text color set before
        // must not make the image transparent. "Q" below restores the previous opacity.
        SetOpacity(1, _strokeAlpha);
        Operator(Number(width), "0 0", Number(height), Number(x), Number(ToPdfY(y + height)), "cm");
        Operator(PdfFormat.Name(image.ResourceName), "Do");
        return RestoreState();
    }

    // ----- Marked content (tagged PDF, ISO 32000-1, 14.6) -----

    /// <summary>
    /// Starts content belonging to a structure element: "/P &lt;&lt;/MCID 3&gt;&gt; BDC". The MCID
    /// (marked-content identifier) links the content to the element in the structure tree.
    /// </summary>
    internal PdfCanvas BeginMarkedContent(string structureType, int mcid)
        => Operator(PdfFormat.Name(structureType), "<</MCID " + mcid.ToString(CultureInfo.InvariantCulture) + ">>", "BDC");

    /// <summary>Starts decoration that is not part of the structure: "/Artifact BMC".</summary>
    internal PdfCanvas BeginArtifactContent() => Operator("/Artifact", "BMC");

    /// <summary>Ends marked content or an artifact: "EMC".</summary>
    internal PdfCanvas EndMarkedContent() => Operator("EMC");

    // ----- Helpers -----

    /// <summary>Converts a y coordinate measured from the top into PDF coordinates (from the bottom).</summary>
    double ToPdfY(double y) => _pageHeight - y;

    /// <summary>Formats a number for the content stream.</summary>
    static string Number(double value) => PdfFormat.Number(value);

    /// <summary>
    /// Writes a color operator (g/G for gray, rg/RG for RGB, k/K for CMYK) and, if the opacity
    /// differs from the current one, the matching "gs".
    /// </summary>
    PdfCanvas SetColor(PdfColor color, bool stroke)
    {
        switch (color.Space)
        {
            case PdfColorSpace.Gray:
                Operator(Number(color.C1), stroke ? "G" : "g");
                break;
            case PdfColorSpace.Rgb:
                Operator(Number(color.C1), Number(color.C2), Number(color.C3), stroke ? "RG" : "rg");
                break;
            default:
                UsesCmyk = true;
                Operator(Number(color.C1), Number(color.C2), Number(color.C3), Number(color.C4), stroke ? "K" : "k");
                break;
        }
        // Rounded like every number in the content stream, so nearly equal values share one entry.
        double alpha = Math.Round(color.Alpha, 4);
        return stroke ? SetOpacity(_fillAlpha, alpha) : SetOpacity(alpha, _strokeAlpha);
    }

    /// <summary>Switches the opacity with "/GSn gs" if it differs from the current one.</summary>
    PdfCanvas SetOpacity(double fill, double stroke)
    {
        if (fill == _fillAlpha && stroke == _strokeAlpha)
            return this;
        _fillAlpha = fill;
        _strokeAlpha = stroke;
        return Operator(PdfFormat.Name(Resources.UseOpacity(fill, stroke)), "gs");
    }

    /// <summary>Appends operands and operator as one line to the content stream.</summary>
    PdfCanvas Operator(params string[] parts)
    {
        _content.AppendJoin(' ', parts).Append('\n');
        return this;
    }
}
