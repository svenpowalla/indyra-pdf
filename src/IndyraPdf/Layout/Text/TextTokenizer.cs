// Splits the spans of a paragraph into pieces for line breaking.
//
// Break opportunities (simplified from Unicode UAX #14, "Line Breaking Algorithm"):
//   - at spaces and tabs (the spaces themselves disappear at a line end),
//   - after a hyphen ("-" and U+2010), e.g. "Rechnungs-|nummer",
//   - forced at "\n".
// The no-break space U+00A0 is treated like a letter, so "10 €" with U+00A0 never breaks.
//
// The text is read in grapheme clusters (what a reader sees as one character, e.g. "é" written
// as e + combining accent, or an emoji sequence like 👨‍👩‍👧). For every cluster the font
// collection chooses the font; where it changes to a fallback font (emoji), a new piece starts
// without a break opportunity, so the emoji stays attached to the neighbouring word.

using System.Globalization;
using System.Text;
using IndyraPdf.Fonts;

namespace IndyraPdf.Layout.Text;

/// <summary>Converts the spans of a paragraph into a sequence of <see cref="TextPiece"/>s.</summary>
internal sealed class TextTokenizer
{
    /// <summary>Fonts to choose from.</summary>
    readonly FontCollection _fonts;

    /// <summary>Values for page number fields.</summary>
    readonly FieldValues _fields;

    /// <summary>The pieces produced so far.</summary>
    readonly List<TextPiece> _pieces = [];

    /// <summary>Characters of the piece currently being collected.</summary>
    readonly StringBuilder _pending = new();

    /// <summary>Kind of the piece currently being collected.</summary>
    PieceKind _pendingKind = PieceKind.Word;

    /// <summary>Font of the piece currently being collected (style font or fallback font).</summary>
    TrueTypeFont? _pendingFont;

    /// <summary>Creates a tokenizer.</summary>
    public TextTokenizer(FontCollection fonts, FieldValues fields)
    {
        _fonts = fonts;
        _fields = fields;
    }

    /// <summary>Splits the spans into pieces.</summary>
    public List<TextPiece> Tokenize(IEnumerable<TextSpan> spans)
    {
        _pieces.Clear();
        foreach (var span in spans)
        {
            var style = span.Style;
            var styleFont = ResolveFont(style);
            var clusters = StringInfo.GetTextElementEnumerator(span.Resolve(_fields));
            while (clusters.MoveNext())
            {
                string cluster = clusters.GetTextElement();
                if (cluster == "\r")
                    continue;
                if (cluster is "\n" or "\r\n")
                {
                    Flush(style, styleFont);
                    _pieces.Add(new TextPiece(PieceKind.LineBreak, style, styleFont, styleFont, []));
                    continue;
                }

                var kind = cluster is " " or "\t" ? PieceKind.Space : PieceKind.Word;
                var font = kind == PieceKind.Space ? styleFont : _fonts.ChooseFont(cluster, styleFont);
                if (kind != _pendingKind || !ReferenceEquals(font, _pendingFont))
                    Flush(style, styleFont);
                _pendingKind = kind;
                _pendingFont = font;
                _pending.Append(cluster);

                if (cluster is "-" or "‐")
                    Flush(style, styleFont, breakAfter: true);
            }
            Flush(style, styleFont); // a span boundary always ends a piece (the style may change)
        }
        return [.. _pieces];
    }

    /// <summary>The font for a style.</summary>
    public TrueTypeFont ResolveFont(TextStyle style) => _fonts.Resolve(style.FontFamily, style.Bold, style.Italic);

    /// <summary>Turns the collected characters into a piece.</summary>
    /// <param name="style">Character format of the piece.</param>
    /// <param name="styleFont">Font of the style (determines line height and decorations).</param>
    /// <param name="breakAfter">Whether a line may break after the piece.</param>
    void Flush(TextStyle style, TrueTypeFont styleFont, bool breakAfter = false)
    {
        if (_pending.Length == 0)
            return;
        var font = _pendingFont ?? styleFont;
        // Ligatures only for fallback fonts: they compose emoji sequences (see TextShaper).
        bool isFallback = !ReferenceEquals(font, styleFont);
        var glyphs = TextShaper.Shape(_pending.ToString(), font, style.FontSize, style.Kerning, ligatures: isFallback);
        _pending.Clear();
        if (glyphs.Count > 0)
            _pieces.Add(new TextPiece(_pendingKind, style, font, styleFont, glyphs, breakAfter));
    }
}
