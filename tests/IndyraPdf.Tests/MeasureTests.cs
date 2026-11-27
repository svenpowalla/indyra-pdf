// Tests for Block.Measure: the natural height of any block, e.g. to size header and footer areas.

using IndyraPdf.Drawing;
using IndyraPdf.Layout;
using IndyraPdf.Writer;

namespace IndyraPdf.Tests;

public class MeasureTests
{
    static readonly FontCollection Fonts = new FontCollection().Add(TestAssets.SansRegular);

    static readonly TextStyle Style = TextStyle.Default with { FontSize = 10 };

    static Paragraph Text(string text) => new Paragraph(Style).Add(text);

    [Fact]
    public void Paragraph_MatchesFormat()
    {
        var paragraph = Text(string.Join(' ', Enumerable.Repeat("Wort", 80)));
        Assert.Equal(paragraph.Format(Fonts, 200, FieldValues.Estimate).Height, paragraph.Measure(Fonts, 200), 6);
    }

    [Fact]
    public void Frame_AddsPaddingAndRespectsMinHeight()
    {
        double line = Text("x").Measure(Fonts, 300);
        var frame = new Frame { Padding = new Thickness(0, 5, 0, 7) }.Add(Text("eins")).Add(Text("zwei"));
        Assert.Equal(2 * line + 12, frame.Measure(Fonts, 300), 6);
        Assert.Equal(100, new Frame { MinHeight = 100 }.Add(Text("eins")).Measure(Fonts, 300), 6);
    }

    [Fact]
    public void Table_IsTheSumOfItsRows()
    {
        double line = Text("x").Measure(Fonts, 300);
        var table = new Table().AddColumn(TableColumn.Relative(1)).AddColumn(TableColumn.Relative(1));
        table.AddRow(new TableRow().Add(new Frame().Add(Text("a"))).Add(new Frame().Add(Text("b"))));
        table.AddRow(new TableRow().Add(new Frame().Add(Text("c")).Add(Text("d"))).Add(new Frame().Add(Text("e"))));
        Assert.Equal(3 * line, table.Measure(Fonts, 300), 6);
    }

    [Fact]
    public void MeasuredHeader_FitsExactly()
    {
        // Typical use: measure the header, then start the content area right below it.
        var header = new Frame { Padding = Thickness.Uniform(4) }.Add(Text("Firma")).Add(Text("Straße 1, 12345 Ort"));
        double height = header.Measure(Fonts, 495);
        var template = new PageTemplate(PageSize.A4, new Box(50, 40 + height, 495, 700))
            .AddFixed(new FixedBlock(new Box(50, 40, 495, height), header));
        Assert.False(new LayoutDocument(template).Add(Text("Inhalt")).Layout(Fonts).HasOverflow);
    }

    [Fact]
    public void EmptyFrame_HasNoHeight() => Assert.Equal(0, new Frame().Measure(Fonts, 100));
}
