// A frame: blocks inside a box with padding, background and borders.
// Used for input lines (bottom border only), checkboxes, highlighted areas and table cells.

using IndyraPdf.Drawing;
using IndyraPdf.Layout.Flow;
using IndyraPdf.Writer;

namespace IndyraPdf.Layout;

/// <summary>Blocks inside a box with padding, background, borders and minimum height.</summary>
/// <remarks>
/// A frame that does not fit is split; every part gets the full padding, background and borders.
/// </remarks>
public sealed class Frame : Block
{
    /// <summary>The content blocks.</summary>
    readonly List<Block> _blocks = [];

    /// <summary>The content blocks, from top to bottom.</summary>
    public IReadOnlyList<Block> Blocks => _blocks;

    /// <summary>Distance between border and content.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Border lines.</summary>
    public BoxBorders Borders { get; set; } = BoxBorders.None;

    /// <summary>Background color, or null for transparent.</summary>
    public PdfColor? Background { get; set; }

    /// <summary>Minimum height in points (including padding).</summary>
    public double MinHeight { get; set; }

    /// <summary>Fixed width in points, or null to use the available width.</summary>
    public double? Width { get; set; }

    /// <summary>Appends a block.</summary>
    public Frame Add(Block block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _blocks.Add(block);
        return this;
    }

    /// <inheritdoc/>
    internal override IBlockFormatter CreateFormatter(LayoutEnvironment environment, double width)
        => new FrameFormatter(this, environment, Width ?? width);

    /// <summary>Draws background, content and borders of a frame part of the given size.</summary>
    internal void DrawPart(IDrawingSurface surface, Box box, IFragment? content)
    {
        if (Background is { } background)
            surface.FillRectangle(box, background);
        content?.Draw(surface, box.X + Padding.Left, box.Y + Padding.Top);
        Borders.Draw(surface, box);
    }
}
