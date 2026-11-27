// Tests for the order in which fixed blocks and flowing content are drawn (PageDrawOrder).
// The order of the text in the content stream is the reading order of untagged PDFs, so a header
// must come before the body text, a footer after it – without changing what lies on top.

using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class DrawOrderTests
{
    static readonly FontCollection Fonts = new FontCollection()
        .Add(TestAssets.Font("LiberationSans-Regular.ttf"));

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 10 };

    /// <summary>Content area from y = 100 to y = 700.</summary>
    static readonly Box ContentArea = new(50, 100, 495, 600);

    static Paragraph Text(string text) => new Paragraph(Style).Add(text);

    /// <summary>Lays out a page with the body text "Body" and returns the drawn text in order.</summary>
    static string DrawnText(params FixedBlock[] blocks)
    {
        var template = new PageTemplate(PageSize.A4, ContentArea);
        foreach (var block in blocks)
            template.AddFixed(block);
        var surface = new RecordingSurface();
        new LayoutDocument(template).Add(Text("Body")).Layout(Fonts).DrawPage(0, surface);
        return surface.Text;
    }

    [Fact]
    public void ForegroundHeader_IsDrawnBeforeTheBody()
        => Assert.Equal("HeadBodyFoot", DrawnText(
            new FixedBlock(new Box(50, 750, 495, 20), Text("Foot")),
            new FixedBlock(new Box(50, 40, 495, 20), Text("Head"))));

    [Fact]
    public void BackgroundFooter_IsDrawnAfterTheBody()
        => Assert.Equal("HeadBodyFoot", DrawnText(
            new FixedBlock(new Box(50, 750, 495, 20), Text("Foot"), FixedLayer.Background),
            new FixedBlock(new Box(50, 40, 495, 20), Text("Head"), FixedLayer.Background)));

    [Fact]
    public void BlocksOverlappingTheContentArea_KeepTheirLayer()
        => Assert.Equal("UnderBodyOver", DrawnText(
            new FixedBlock(new Box(50, 650, 495, 100), Text("Over")),
            new FixedBlock(new Box(50, 50, 495, 100), Text("Under"), FixedLayer.Background)));

    [Fact]
    public void HeaderOverlappingAnEarlierForegroundBlock_StaysOnTopOfIt()
        // "Mark" covers the whole page and is drawn first; the header lies on it and must stay
        // on top, so it cannot move before the body text.
        => Assert.Equal("BodyMarkHead", DrawnText(
            new FixedBlock(new Box(0, 0, 595, 842), Text("Mark")),
            new FixedBlock(new Box(50, 40, 495, 20), Text("Head"))));

    [Fact]
    public void BlocksWithEqualValues_AreAllDrawn()
    {
        // The same block twice: the first moves before the body; the second lies on the first,
        // so it must stay on top and is drawn after the body.
        var header = new FixedBlock(new Box(50, 40, 495, 20), Text("Head"));
        Assert.Equal("HeadBodyHead", DrawnText(header, header));
    }
}
