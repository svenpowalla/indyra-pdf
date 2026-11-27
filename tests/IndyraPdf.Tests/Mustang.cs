// Test helper: runs the Mustang validator (https://www.mustangproject.org, Apache 2.0), the common
// reference for ZUGFeRD / Factur-X. It checks the PDF/A part and the embedded XML (schema and
// Schematron rules of the profile).
//
// Location: environment variable INDYRAPDF_MUSTANG (path of Mustang-CLI.jar), otherwise
// ~/Tools/mustang/Mustang-CLI.jar. Java is taken from JAVA_HOME or ~/Tools/temurin-jre.

using System.Diagnostics;

namespace IndyraPdf.Tests;

/// <summary>Runs the Mustang ZUGFeRD validator.</summary>
internal static class Mustang
{
    /// <summary>Path of Mustang-CLI.jar, or null if not installed.</summary>
    public static string? Jar { get; } = FindJar();

    /// <summary>Validates a ZUGFeRD PDF; returns whether PDF and XML are valid, plus the report.</summary>
    public static (bool Valid, string Report) Validate(byte[] pdf)
    {
        var directory = Directory.CreateTempSubdirectory("indyrapdf-mustang-");
        var file = Path.Combine(directory.FullName, "invoice.pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            var start = new ProcessStartInfo(JavaExecutable(), ["-jar", Jar!, "--action", "validate", "--source", file, "--no-notices"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory.FullName,
            };
            using var process = Process.Start(start)!;
            var errorTask = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            string report = output + errorTask.Result;
            // The final summary of the report is the last <summary status="..."/> element.
            int last = output.LastIndexOf("<summary status=\"", StringComparison.Ordinal);
            bool valid = last >= 0 && output[last..].StartsWith("<summary status=\"valid\"", StringComparison.Ordinal);
            return (valid, report);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    static string? FindJar()
    {
        var configured = Environment.GetEnvironmentVariable("INDYRAPDF_MUSTANG");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            return configured;
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tools", "mustang", "Mustang-CLI.jar");
        return File.Exists(standard) ? standard : null;
    }

    /// <summary>The java executable: from JAVA_HOME, ~/Tools/temurin-jre or the system path.</summary>
    static string JavaExecutable()
    {
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(home))
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tools", "temurin-jre");
            home = Directory.Exists(root)
                ? Directory.GetDirectories(root).Select(d => Path.Combine(d, "Contents", "Home")).FirstOrDefault(Directory.Exists)
                : null;
        }
        return home is null ? "java" : Path.Combine(home, "bin", "java");
    }
}
