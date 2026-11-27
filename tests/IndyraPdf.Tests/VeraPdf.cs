// Test helper: runs the veraPDF validator (https://verapdf.org) if it is installed.
//
// Location: environment variable INDYRAPDF_VERAPDF (path of the "verapdf" script), otherwise
// ~/Tools/verapdf/verapdf. Java: JAVA_HOME, otherwise the first Temurin runtime in ~/Tools/temurin-jre.
// Tests that need veraPDF are skipped when it is missing (see VeraPdfFactAttribute).

using System.Diagnostics;

namespace IndyraPdf.Tests;

/// <summary>Runs veraPDF on PDF files.</summary>
internal static class VeraPdf
{
    /// <summary>Path of the veraPDF start script, or null if not installed.</summary>
    public static string? Executable { get; } = FindExecutable();

    /// <summary>Java installation for veraPDF, or null to rely on the system.</summary>
    static string? JavaHome { get; } = FindJavaHome();

    /// <summary>
    /// Validates a PDF file against a PDF/A flavour ("3b", "3u") and returns whether it passed,
    /// together with the full report for error messages.
    /// </summary>
    public static (bool Passed, string Report) Validate(byte[] pdf, string flavour)
    {
        var file = Path.Combine(Path.GetTempPath(), $"indyrapdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            var start = new ProcessStartInfo(Executable!, ["--flavour", flavour, "--format", "text", "--verbose", file])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (JavaHome is not null)
                start.Environment["JAVA_HOME"] = JavaHome;
            using var process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (output.Contains($"PASS {file}"), output);
        }
        finally
        {
            File.Delete(file);
        }
    }

    static string? FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("INDYRAPDF_VERAPDF");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var standard = Path.Combine(home, "Tools", "verapdf", "verapdf");
        return File.Exists(standard) ? standard : null;
    }

    static string? FindJavaHome()
    {
        var configured = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(configured))
            return configured;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tools", "temurin-jre");
        if (!Directory.Exists(root))
            return null;
        return Directory.GetDirectories(root)
            .Select(d => Path.Combine(d, "Contents", "Home"))
            .FirstOrDefault(Directory.Exists);
    }
}
