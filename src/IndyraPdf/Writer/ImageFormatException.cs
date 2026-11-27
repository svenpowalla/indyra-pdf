// Error type for unreadable or unsupported image files.

namespace IndyraPdf.Writer;

/// <summary>Raised when an image cannot be read or its format is not supported.</summary>
public sealed class ImageFormatException : Exception
{
    /// <summary>Creates the error with a message.</summary>
    public ImageFormatException(string message) : base(message) { }
}
