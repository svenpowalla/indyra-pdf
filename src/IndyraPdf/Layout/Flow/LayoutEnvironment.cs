// Information that formatters need besides the block itself: fonts and page number values.

namespace IndyraPdf.Layout.Flow;

/// <summary>Shared information for all formatters of one layout pass.</summary>
internal sealed class LayoutEnvironment
{
    /// <summary>Creates the environment.</summary>
    /// <param name="fonts">Fonts to choose from.</param>
    /// <param name="fields">Returns the field values for the page being filled at the moment of the call.</param>
    public LayoutEnvironment(FontCollection fonts, Func<FieldValues> fields)
    {
        Fonts = fonts;
        _fields = fields;
    }

    /// <summary>Source of the current field values.</summary>
    readonly Func<FieldValues> _fields;

    /// <summary>Fonts to choose from.</summary>
    public FontCollection Fonts { get; }

    /// <summary>Field values for the page being filled right now.</summary>
    public FieldValues CurrentFields => _fields();
}
