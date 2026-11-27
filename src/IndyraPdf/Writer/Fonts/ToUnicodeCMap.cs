// Builds the ToUnicode table of a font.
//
// The page content only contains glyph codes. Without further information a PDF reader cannot
// tell which letter a code stands for, so copying or searching text would fail. The ToUnicode
// table ("CMap", a small PostScript program) maps every code back to Unicode text
// (ISO 32000-2, 9.10.3).

using System.Globalization;
using System.Text;

namespace IndyraPdf.Writer.Fonts;

/// <summary>
/// Creates ToUnicode CMaps: for CID fonts with two-byte codes and for Type3 fonts (color emoji)
/// with one-byte codes.
/// </summary>
internal static class ToUnicodeCMap
{
    /// <summary>A "bfchar" block may contain at most 100 entries (Adobe Technical Note #5411).</summary>
    const int MaxEntriesPerBlock = 100;

    /// <summary>Builds the CMap for the given code → text mapping.</summary>
    /// <param name="unicodeByCode">Text of every code; one code may stand for several characters (emoji sequences).</param>
    /// <param name="codeBytes">Length of the codes in the page content: 2 for CID fonts, 1 for simple fonts.</param>
    public static byte[] Build(IReadOnlyDictionary<int, string> unicodeByCode, int codeBytes = 2)
    {
        string codeFormat = "X" + (2 * codeBytes).ToString(CultureInfo.InvariantCulture);
        string lowest = new('0', 2 * codeBytes), highest = new('F', 2 * codeBytes);
        var sb = new StringBuilder();
        sb.Append("/CIDInit /ProcSet findresource begin\n");
        sb.Append("12 dict begin\n");
        sb.Append("begincmap\n");
        sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        sb.Append("/CMapName /Adobe-Identity-UCS def\n");
        sb.Append("/CMapType 2 def\n");
        // All codes have the same length: 0000 to FFFF (two bytes) or 00 to FF (one byte).
        sb.Append("1 begincodespacerange\n<").Append(lowest).Append("> <").Append(highest).Append(">\nendcodespacerange\n");

        // Each line: <code> <text as UTF-16BE>. Characters beyond U+FFFF become surrogate pairs.
        var entries = unicodeByCode.OrderBy(e => e.Key).ToList();
        for (int start = 0; start < entries.Count; start += MaxEntriesPerBlock)
        {
            var block = entries.Skip(start).Take(MaxEntriesPerBlock).ToList();
            sb.Append(block.Count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            foreach (var (code, text) in block)
            {
                sb.Append('<').Append(code.ToString(codeFormat, CultureInfo.InvariantCulture)).Append("> <");
                sb.Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text)));
                sb.Append(">\n");
            }
            sb.Append("endbfchar\n");
        }

        sb.Append("endcmap\n");
        sb.Append("CMapName currentdict /CMap defineresource pop\n");
        sb.Append("end\nend\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
