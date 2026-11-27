// Entry point of the PDF writer: a document with pages, fonts and images.
//
// Usage: create a document, add fonts, images and pages, draw on the pages' canvases, save.
// Saving builds the complete object structure (ISO 32000-2, 7.7):
//
//   Trailer ─ /Root → Catalog ─ /Pages → Page tree ─ /Kids → Page, Page, …
//           │                 │                              each with /Contents and /Resources
//           │                 ├ /Metadata → XMP metadata (always written)
//           │                 ├ /OutputIntents → sRGB colour profile (PDF/A only)
//           │                 └ /AF, /Names → embedded files
//           └ /Info → document information
//
// The objects are built anew on every save, because only then it is known which glyphs every
// subset font must contain.

using IndyraPdf.Fonts;
using IndyraPdf.Writer.Metadata;
using IndyraPdf.Writer.Objects;

namespace IndyraPdf.Writer;

/// <summary>A PDF document: pages, fonts, images and document information.</summary>
/// <example>
/// <code>
/// var document = new PdfDocument();
/// var font = document.AddFont("LiberationSans-Regular.ttf");
/// var page = document.AddPage(PageSize.A4);
/// page.Canvas.DrawText(font, 12, 72, 72, "Hello world");
/// document.Save("hello.pdf");
/// </code>
/// </example>
public sealed class PdfDocument
{
    /// <summary>PDF version written to the file header. PDF/A-3 is based on PDF 1.7.</summary>
    const string PdfVersion = "1.7";

    /// <summary>Pages in order.</summary>
    readonly List<PdfPage> _pages = [];

    /// <summary>All fonts added, used or not.</summary>
    readonly List<PdfFont> _fonts = [];

    /// <summary>All images added, used or not.</summary>
    readonly List<PdfImage> _images = [];

    /// <summary>Images added as <see cref="Drawing.ImageSource"/>, to store each only once.</summary>
    readonly Dictionary<Drawing.ImageSource, PdfImage> _imagesBySource = new(ReferenceEqualityComparer.Instance);

    /// <summary>Type3 fonts holding the color emoji drawn so far (254 glyphs each).</summary>
    readonly List<Fonts.Type3Font> _type3Fonts = [];

    /// <summary>Document information such as title and author.</summary>
    public PdfDocumentInfo Info { get; } = new();

    /// <summary>Embedded files.</summary>
    readonly List<PdfAttachment> _attachments = [];

    /// <summary>Additional XMP schemas.</summary>
    readonly List<XmpExtension> _xmpExtensions = [];

    /// <summary>Compress page contents, fonts and the ToUnicode tables (default: true).</summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Archive standard to write (default: plain PDF). For PDF/A, IndyraPdf adds the sRGB output
    /// intent and the PDF/A identification; CMYK colours and CMYK images are then not allowed.
    /// </summary>
    public PdfConformance Conformance { get; set; } = PdfConformance.None;

    /// <summary>Whether tagging was switched on explicitly.</summary>
    bool _tagged;

    /// <summary>Structure tree, created when the first page of a tagged document is drawn.</summary>
    Tagging.StructureTree? _structureTree;

    /// <summary>
    /// Writes a tagged PDF: a logical structure (headings, paragraphs, lists, tables, figures) for
    /// screen readers. Must be set before drawing. Always on for <see cref="PdfConformance.PdfA3A"/>
    /// and <see cref="UniversalAccessibility"/>. Requires <see cref="Language"/>.
    /// </summary>
    public bool Tagged
    {
        get => _tagged || Conformance == PdfConformance.PdfA3A || UniversalAccessibility;
        set => _tagged = value;
    }

    /// <summary>
    /// Conform to PDF/UA-1 (ISO 14289-1, universal accessibility). Implies <see cref="Tagged"/>;
    /// requires <see cref="Language"/> and a title in <see cref="Info"/>. Must be set before drawing.
    /// </summary>
    public bool UniversalAccessibility { get; set; }

    /// <summary>Natural language of the document as BCP 47 code, e.g. "de-DE" (catalog /Lang).</summary>
    public string? Language { get; set; }

    /// <summary>Structure tree for tagged documents, or null.</summary>
    internal Tagging.StructureTree? StructureTree => Tagged ? _structureTree ??= new Tagging.StructureTree() : null;

    /// <summary>Embedded files.</summary>
    public IReadOnlyList<PdfAttachment> Attachments => _attachments;

    /// <summary>Additional XMP schemas written to the metadata.</summary>
    public IReadOnlyList<XmpExtension> XmpExtensions => _xmpExtensions;

    /// <summary>Embeds a file.</summary>
    /// <exception cref="ArgumentException">A file with the same name is already embedded.</exception>
    public PdfDocument Attach(PdfAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (_attachments.Exists(a => a.FileName == attachment.FileName))
            throw new ArgumentException($"A file named \"{attachment.FileName}\" is already embedded.", nameof(attachment));
        _attachments.Add(attachment);
        return this;
    }

    /// <summary>Adds an XMP schema to the metadata (described as PDF/A extension schema).</summary>
    /// <exception cref="ArgumentException">A schema with the same namespace already exists.</exception>
    public PdfDocument AddXmpExtension(XmpExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (_xmpExtensions.Exists(e => e.NamespaceUri == extension.NamespaceUri))
            throw new ArgumentException($"The XMP namespace \"{extension.NamespaceUri}\" is already used.", nameof(extension));
        _xmpExtensions.Add(extension);
        return this;
    }

    /// <summary>All pages in order.</summary>
    public IReadOnlyList<PdfPage> Pages => _pages;

    /// <summary>Appends a page of the given size.</summary>
    public PdfPage AddPage(PageSize size)
    {
        var page = new PdfPage(size);
        _pages.Add(page);
        return page;
    }

    /// <summary>Appends an A4 portrait page.</summary>
    public PdfPage AddPage() => AddPage(PageSize.A4);

    /// <summary>Loads a TrueType font from a file.</summary>
    public PdfFont AddFont(string path) => AddFont(TrueTypeFont.Load(path));

    /// <summary>Loads a TrueType font from the bytes of a .ttf file.</summary>
    public PdfFont AddFont(byte[] data) => AddFont(TrueTypeFont.Load(data));

    /// <summary>
    /// Uses a font that has already been loaded. Adding the same font object twice returns the
    /// same <see cref="PdfFont"/>, so it is embedded only once.
    /// </summary>
    public PdfFont AddFont(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        var existing = _fonts.Find(f => ReferenceEquals(f.Font, font));
        if (existing is not null)
            return existing;
        var pdfFont = new PdfFont(font, $"F{_fonts.Count + 1}");
        _fonts.Add(pdfFont);
        return pdfFont;
    }

    /// <summary>
    /// Returns the Type3 font and one-byte code under which a glyph of a color bitmap font is
    /// drawn. Glyphs keep their code for the whole document; a new Type3 font is started when the
    /// existing ones of this font are full.
    /// </summary>
    /// <param name="font">The color bitmap font (e.g. Noto Color Emoji).</param>
    /// <param name="glyphId">Glyph number in that font.</param>
    /// <param name="text">Text the glyph stands for (for copying and searching).</param>
    internal (Fonts.Type3Font Font, int Code) UseColorGlyph(TrueTypeFont font, int glyphId, string text)
    {
        foreach (var candidate in _type3Fonts)
        {
            if (ReferenceEquals(candidate.Font, font) && candidate.TryUse(glyphId, text, out int code))
                return (candidate, code);
        }
        var type3 = new Fonts.Type3Font(font, $"T{_type3Fonts.Count + 1}");
        _type3Fonts.Add(type3);
        type3.TryUse(glyphId, text, out int newCode);
        return (type3, newCode);
    }

    /// <summary>
    /// Uses an image of the layout engine. Adding the same image object twice returns the same
    /// <see cref="PdfImage"/>, so it is stored only once.
    /// </summary>
    public PdfImage AddImage(Drawing.ImageSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_imagesBySource.TryGetValue(image, out var existing))
            return existing;
        var pdfImage = AddImage(image.Data);
        _imagesBySource[image] = pdfImage;
        return pdfImage;
    }

    /// <summary>Loads an image (PNG or JPEG) from a file.</summary>
    public PdfImage AddImage(string path) => AddImage(File.ReadAllBytes(path));

    /// <summary>Loads an image (PNG or JPEG) from its bytes.</summary>
    public PdfImage AddImage(byte[] data)
    {
        var image = new PdfImage(data, $"Im{_images.Count + 1}");
        _images.Add(image);
        return image;
    }

    /// <summary>Saves the document to a file.</summary>
    public void Save(string path)
    {
        using var file = File.Create(path);
        Save(file);
    }

    /// <summary>Writes the document to a stream.</summary>
    /// <exception cref="InvalidOperationException">
    /// The document has no pages, or it violates the chosen <see cref="Conformance"/>.
    /// </exception>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_pages.Count == 0)
            throw new InvalidOperationException("The document has no pages.");
        if (Conformance != PdfConformance.None)
            CheckPdfARules();
        CheckAccessibilityRules();

        var table = new PdfObjectTable();

        var catalog = table.Add(new PdfDictionary("Catalog"));
        var pageTree = table.Add(new PdfDictionary("Pages"));
        catalog["Pages"] = pageTree;

        // Only fonts and images that appear on a page are written.
        var fontObjects = _fonts
            .Where(font => _pages.Any(page => page.Canvas.Resources.Contains(font)))
            .ToDictionary(font => font, font => font.Build(table, Compress));
        var imageObjects = _images
            .Where(image => _pages.Any(page => page.Canvas.Resources.Contains(image)))
            .ToDictionary(image => image, image => image.Build(table));
        var type3Objects = _type3Fonts
            .Where(font => _pages.Any(page => page.Canvas.Resources.Contains(font)))
            .ToDictionary(font => font, font => new Fonts.Type3FontWriter(font, Compress).Write(table));
        var resources = new ResourceObjects(fontObjects, type3Objects, imageObjects);

        var pageObjects = _pages.Select(page => page.Build(table, pageTree, resources, Compress)).ToList();
        var kids = new PdfArray();
        foreach (var page in pageObjects)
            kids.Add(page);
        pageTree["Kids"] = kids;
        pageTree["Count"] = new PdfNumber(_pages.Count);

        if (!string.IsNullOrEmpty(Language))
            catalog["Lang"] = PdfString.FromText(Language);
        if (StructureTree is { } tree)
        {
            var markInfo = new PdfDictionary();
            markInfo["Marked"] = new PdfBoolean(true);
            catalog["MarkInfo"] = markInfo;
            catalog["StructTreeRoot"] = tree.Write(table, pageObjects);
        }
        if (UniversalAccessibility)
        {
            // PDF/UA: viewers show the title instead of the file name.
            var preferences = new PdfDictionary();
            preferences["DisplayDocTitle"] = new PdfBoolean(true);
            catalog["ViewerPreferences"] = preferences;
        }

        // XMP metadata. PDF/A requires it unfiltered, so tools can read it without decompressing.
        var metadata = table.Add(new PdfStream("Metadata", XmpWriter.Build(Info, Conformance, UniversalAccessibility, _xmpExtensions)) { Compress = false });
        metadata["Subtype"] = new PdfName("XML");
        catalog["Metadata"] = metadata;

        if (Conformance != PdfConformance.None)
            catalog["OutputIntents"] = new PdfArray().Add(OutputIntentWriter.Write(table, Compress));

        AttachmentWriter.Write(table, catalog, _attachments, Info.EffectiveModificationDate, Compress);

        var info = table.Add(Info.Build());
        table.WriteFile(stream, PdfVersion, catalog, info);
    }

    /// <summary>Checks the information tagged and accessible documents need from the caller.</summary>
    void CheckAccessibilityRules()
    {
        if (Tagged && string.IsNullOrWhiteSpace(Language))
            throw new InvalidOperationException("Tagged documents need a document language (PdfDocument.Language, e.g. \"de-DE\").");
        if (UniversalAccessibility && string.IsNullOrWhiteSpace(Info.Title))
            throw new InvalidOperationException("PDF/UA documents need a title (PdfDocument.Info.Title).");
    }

    /// <summary>
    /// Checks rules of PDF/A that IndyraPdf cannot fulfil automatically. The output intent is
    /// sRGB, so device CMYK colours have no defined meaning (ISO 19005-3, 6.2.4.3).
    /// </summary>
    void CheckPdfARules()
    {
        if (_pages.Any(p => p.Canvas.UsesCmyk))
            throw new InvalidOperationException("PDF/A documents of IndyraPdf use an sRGB output intent; CMYK colours are not allowed. Use RGB or gray colours.");
        var cmykImage = _images.FirstOrDefault(i => i.IsCmyk && _pages.Any(p => p.Canvas.Resources.Contains(i)));
        if (cmykImage is not null)
            throw new InvalidOperationException("PDF/A documents of IndyraPdf use an sRGB output intent; CMYK images are not allowed. Convert the image to RGB.");
    }

    /// <summary>Returns the document as bytes.</summary>
    public byte[] ToArray()
    {
        using var buffer = new MemoryStream();
        Save(buffer);
        return buffer.ToArray();
    }
}
