// Test attribute: marks a test as skipped when the Mustang validator is not installed.

namespace IndyraPdf.Tests;

/// <summary>A <see cref="FactAttribute"/> that skips the test if Mustang is not available.</summary>
public sealed class MustangFactAttribute : FactAttribute
{
    /// <summary>Sets the skip reason if Mustang is missing.</summary>
    public MustangFactAttribute()
    {
        if (Mustang.Jar is null)
            Skip = "Mustang is not installed (see tests/IndyraPdf.Tests/Mustang.cs).";
    }
}
