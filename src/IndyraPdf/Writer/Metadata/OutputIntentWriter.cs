// Writes the PDF/A output intent: the colour profile that defines what DeviceRGB/DeviceGray mean.
//
// Without an output intent, device colours have no defined appearance, which PDF/A forbids
// (ISO 19005-3, 6.2.3 and 6.2.4.3). IndyraPdf uses the sRGB profile of the ICC, as PDFsharp 6.2.4
// does. Consequence: CMYK colours are not allowed in PDF/A documents of IndyraPdf.

using System.Reflection;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer.Metadata;

/// <summary>Creates the sRGB output intent (ISO 32000-2, 14.11.5).</summary>
internal static class OutputIntentWriter
{
    /// <summary>Name of the embedded profile resource (see IndyraPdf.csproj).</summary>
    const string ProfileResource = "IndyraPdf.sRGB2014.icc";

    /// <summary>Standard name of the sRGB colour space in the ICC registry.</summary>
    const string ConditionIdentifier = "sRGB IEC61966-2.1";

    /// <summary>The profile bytes, loaded once.</summary>
    static readonly Lazy<byte[]> Profile = new(LoadProfile);

    /// <summary>Registers the profile stream and returns the output intent dictionary.</summary>
    public static PdfDictionary Write(PdfObjectTable table, bool compress)
    {
        var profile = table.Add(new PdfStream(Profile.Value) { Compress = compress });
        profile["N"] = new PdfNumber(3); // number of colour components: R, G, B

        var intent = new PdfDictionary("OutputIntent");
        intent["S"] = new PdfName("GTS_PDFA1"); // subtype for PDF/A (all parts use this name)
        intent["OutputConditionIdentifier"] = PdfString.FromAscii(ConditionIdentifier);
        intent["RegistryName"] = PdfString.FromAscii("http://www.color.org");
        intent["Info"] = PdfString.FromAscii(ConditionIdentifier);
        intent["DestOutputProfile"] = profile;
        return intent;
    }

    /// <summary>Reads the profile from the assembly resources.</summary>
    static byte[] LoadProfile()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ProfileResource)
            ?? throw new InvalidOperationException("The embedded sRGB colour profile is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
