// Breaks a paragraph into lines ("first fit", as word processors, browsers, MigraDoc and FPDF do).
//
// Words are added to the current line as long as they fit; the next word that does not fit
// starts a new line. Spaces at a line break are dropped. A word wider than the whole line is
// split between characters. This greedy method gives the same breaks as text editors in web
// browsers, which is what users expect; optimizing whole paragraphs (Knuth–Plass, as in TeX)
// would not.

namespace IndyraPdf.Layout.Text;

/// <summary>Breaks the text of a paragraph into lines.</summary>
internal sealed class LineBreaker
{
    /// <summary>Widths within this tolerance still count as fitting (rounding errors).</summary>
    const double Tolerance = 0.0001;

    /// <summary>The paragraph.</summary>
    readonly Paragraph _paragraph;

    /// <summary>Splits the spans into pieces.</summary>
    readonly TextTokenizer _tokenizer;

    /// <summary>Finished lines.</summary>
    readonly List<TextLine> _lines = [];

    /// <summary>Segments of the line being built.</summary>
    List<LineSegment> _segments = [];

    /// <summary>Current horizontal position in the line being built.</summary>
    double _x;

    /// <summary>Width available for text, without indents.</summary>
    double _width;

    /// <summary>Creates a line breaker.</summary>
    public LineBreaker(Paragraph paragraph, FontCollection fonts, FieldValues fields)
    {
        _paragraph = paragraph;
        _tokenizer = new TextTokenizer(fonts, fields);
    }

    /// <summary>Width available for the line being built (the first line has its own indent).</summary>
    double LineWidth => Math.Max(0, _lines.Count == 0 ? _width - _paragraph.FirstLineIndent : _width);

    /// <summary>Whether the line being built contains no word yet.</summary>
    bool LineHasNoWord => !_segments.Exists(s => !s.IsSpace);

    /// <summary>Breaks the paragraph into lines.</summary>
    /// <param name="width">Width available for text, without left and right indent.</param>
    public List<TextLine> Break(double width)
    {
        _width = width;
        var pieces = _tokenizer.Tokenize(_paragraph.Spans);
        var pendingSpaces = new List<TextPiece>();

        int i = 0;
        while (i < pieces.Count)
        {
            var piece = pieces[i];
            if (piece.Kind == PieceKind.LineBreak)
            {
                AddPieces(pendingSpaces);
                pendingSpaces.Clear();
                FinishLine(justifiable: false);
                i++;
                continue;
            }
            if (piece.Kind == PieceKind.Space)
            {
                pendingSpaces.Add(piece);
                i++;
                continue;
            }

            // A word: consecutive word pieces up to the next break opportunity.
            int end = i;
            while (end + 1 < pieces.Count && pieces[end + 1].Kind == PieceKind.Word && !pieces[end].BreakAfter)
                end++;
            var word = pieces.GetRange(i, end - i + 1);
            double wordWidth = word.Sum(p => p.Width);
            double spaceWidth = pendingSpaces.Sum(p => p.Width);

            if (_x + spaceWidth + wordWidth <= LineWidth + Tolerance)
            {
                AddPieces(pendingSpaces);
                AddPieces(word);
                pendingSpaces.Clear();
                i = end + 1;
            }
            else if (LineHasNoWord)
            {
                // The word does not fit even into an empty line: split it between characters.
                AddPieces(pendingSpaces);
                pendingSpaces.Clear();
                AddWordInParts(word);
                i = end + 1;
            }
            else
            {
                // Break before the word; the spaces in between disappear.
                pendingSpaces.Clear();
                FinishLine(justifiable: true);
            }
        }

        AddPieces(pendingSpaces);
        FinishLine(justifiable: false); // the last line of a paragraph is never stretched
        return _lines;
    }

    /// <summary>Appends whole pieces to the current line.</summary>
    void AddPieces(IEnumerable<TextPiece> pieces)
    {
        foreach (var piece in pieces)
            AddGlyphs(piece, piece.Glyphs);
    }

    /// <summary>Appends glyphs of a piece as one segment.</summary>
    void AddGlyphs(TextPiece piece, IReadOnlyList<ShapedGlyph> glyphs)
    {
        if (glyphs.Count == 0)
            return;
        var segment = new LineSegment(piece.Style, piece.Font, piece.MetricsFont, glyphs, _x, piece.Kind == PieceKind.Space);
        _segments.Add(segment);
        _x += segment.Width;
    }

    /// <summary>
    /// Adds an overlong word character by character, starting new lines as needed. Every line
    /// gets at least one character, so even an extremely narrow area makes progress.
    /// </summary>
    void AddWordInParts(List<TextPiece> word)
    {
        foreach (var piece in word)
        {
            var part = new List<ShapedGlyph>();
            foreach (var glyph in piece.Glyphs)
            {
                bool lineHasContent = part.Count > 0 || !LineHasNoWord;
                if (lineHasContent && _x + part.Sum(g => g.Advance) + glyph.Advance > LineWidth + Tolerance)
                {
                    AddGlyphs(piece, part);
                    part = [];
                    FinishLine(justifiable: false);
                }
                part.Add(glyph);
            }
            AddGlyphs(piece, part);
        }
    }

    /// <summary>Completes the current line and starts a new one.</summary>
    void FinishLine(bool justifiable)
    {
        double indent = _lines.Count == 0 ? _paragraph.FirstLineIndent : 0;
        _lines.Add(new TextLine(_segments, indent, LineWidth, justifiable, EmptyLineMetrics(), _paragraph.LineSpacing));
        _segments = [];
        _x = 0;
    }

    /// <summary>A segment without glyphs in the paragraph's default style, for the height of empty lines.</summary>
    LineSegment EmptyLineMetrics()
    {
        var font = _tokenizer.ResolveFont(_paragraph.Style);
        return new(_paragraph.Style, font, font, [], 0, isSpace: false);
    }
}
