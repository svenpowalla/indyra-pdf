// Kinds of pieces the line breaker works with.

namespace IndyraPdf.Layout.Text;

/// <summary>Kind of a <see cref="TextPiece"/>.</summary>
internal enum PieceKind
{
    /// <summary>Visible characters without break opportunity inside.</summary>
    Word,

    /// <summary>One or more spaces: a possible line break.</summary>
    Space,

    /// <summary>Forced line break ("\n").</summary>
    LineBreak,
}
