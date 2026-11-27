// Test attribute: marks a test as skipped when veraPDF is not installed.

namespace IndyraPdf.Tests;

/// <summary>A <see cref="FactAttribute"/> that skips the test if veraPDF is not available.</summary>
public sealed class VeraPdfFactAttribute : FactAttribute
{
    /// <summary>Sets the skip reason if veraPDF is missing.</summary>
    public VeraPdfFactAttribute()
    {
        if (VeraPdf.Executable is null)
            Skip = "veraPDF is not installed (see tests/IndyraPdf.Tests/VeraPdf.cs).";
    }
}
