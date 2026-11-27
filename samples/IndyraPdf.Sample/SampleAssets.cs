// Locations of the sample's input files (fonts, images) and output folder.

namespace IndyraPdf.Sample;

/// <summary>Paths below the repository root used by the sample program.</summary>
internal sealed class SampleAssets
{
    /// <summary>Repository root folder.</summary>
    readonly string _root;

    /// <summary>Creates the paths for the given repository root.</summary>
    public SampleAssets(string root) => _root = root;

    /// <summary>Folder for the generated PDF ("out", ignored by Git).</summary>
    public string OutputFolder => Path.Combine(_root, "out");

    /// <summary>Path of a font file in assets/fonts.</summary>
    public string Font(string fileName) => Path.Combine(_root, "assets", "fonts", fileName);

    /// <summary>Path of an invoice XML in assets/invoices.</summary>
    public string Invoice(string fileName) => Path.Combine(_root, "assets", "invoices", fileName);

    /// <summary>Path of an image file in assets/images.</summary>
    public string Image(string fileName) => Path.Combine(_root, "assets", "images", fileName);
}
