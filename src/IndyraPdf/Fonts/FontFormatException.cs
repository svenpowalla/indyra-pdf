// Error type for unreadable or unsupported font files.

namespace IndyraPdf.Fonts;

/// <summary>Raised when a font file cannot be read or is not supported.</summary>
public sealed class FontFormatException : Exception
{
    /// <summary>Creates the error with a message.</summary>
    public FontFormatException(string message) : base(message) { }

    /// <summary>Creates the error with a message and the error that caused it.</summary>
    public FontFormatException(string message, Exception inner) : base(message, inner) { }
}
