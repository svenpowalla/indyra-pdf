// Tests for LaidOutDocument.Overflows: content taller than the content area of a page or taller
// than the area of a fixed block.

using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class OverflowTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"));

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 10 };

    static PageTemplate Page() => new(PageSize.A4, new Box(50, 50, 495, 700));

    /// <summary>A block of fixed height, e.g. a picture or a table row that cannot be split.</summary>
    static Frame Solid(double height) => new() { Background = PdfColor.Gray(0.5), MinHeight = height };

    [Fact]
    public void ContentThatFits_HasNoOverflow()
    {
        var document = new LayoutDocument(Page())
            .Add(new Paragraph(Style).Add(string.Join(' ', Enumerable.Repeat("Text", 2000))))
            .Layout(Fonts);
        Assert.True(document.PageCount > 1);
        Assert.False(document.HasOverflow);
    }

    [Fact]
    public void BlockTallerThanThePage_IsReported()
    {
        var document = new LayoutDocument(Page())
            .Add(new Paragraph(Style).Add("Davor"))
            .Add(Solid(760))
            .Layout(Fonts);

        var overflow = Assert.Single(document.Overflows);
        Assert.Equal(1, overflow.PageIndex); // the block moves to page 2 and still does not fit
        Assert.Null(overflow.FixedBlock);
        Assert.Equal(60, overflow.Excess, 3);
    }

    [Fact]
    public void FixedBlockWithTooMuchText_IsReported()
    {
        var field = new FixedBlock(new Box(50, 760, 100, 20), new Paragraph(Style).Add("Viel zu viel Text für ein so kleines Feld"));
        var document = new LayoutDocument(Page().AddFixed(field)).Layout(Fonts);

        var overflow = Assert.Single(document.Overflows);
        Assert.Same(field, overflow.FixedBlock);
        Assert.Equal(760, overflow.FixedBlock!.Area.Y);
        Assert.True(overflow.Excess > 0); // two lines of about 11.5 pt in a field of 20 pt
    }

    [Fact]
    public void SpacingAfterTheLastParagraph_DoesNotCount()
    {
        // One line fits into 20 pt; the 30 pt spacing after it would not, but that is only space.
        var field = new FixedBlock(new Box(50, 760, 300, 20), new Paragraph(Style) { SpaceAfter = 30 }.Add("Passt"));
        Assert.False(new LayoutDocument(Page().AddFixed(field)).Layout(Fonts).HasOverflow);
    }

    [Fact]
    public void ClippedFixedBlock_IsReportedToo()
    {
        var field = new FixedBlock(new Box(50, 760, 100, 20), Solid(50), Clip: true);
        var overflow = Assert.Single(new LayoutDocument(Page().AddFixed(field)).Layout(Fonts).Overflows);
        Assert.Equal(30, overflow.Excess, 3);
    }
}
