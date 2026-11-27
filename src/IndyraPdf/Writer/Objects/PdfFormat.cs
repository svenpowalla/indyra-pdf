// Formatting rules of the PDF syntax for numbers, names and dates.

using System.Globalization;
using System.Text;

namespace IndyraPdf.Writer.Objects;

/// <summary>Formats numbers, names and dates according to the PDF syntax.</summary>
internal static class PdfFormat
{
    /// <summary>
    /// Formats a number without exponent, with a point as decimal separator and at most four
    /// decimals (ISO 32000-2, 7.3.3: PDF does not allow "1E-5"). Four decimals are 1/10000 pt,
    /// far below what is visible.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
    public static string Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), "PDF has no representation for NaN or infinity.");

        value = Math.Round(value, 4);
        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        var text = value.ToString("0.####", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>
    /// Formats a name with leading slash. Characters outside the printable ASCII range and PDF
    /// delimiters are written as #xx (ISO 32000-2, 7.3.5), e.g. "A B" → "/A#20B".
    /// </summary>
    public static string Name(string name)
    {
        var sb = new StringBuilder(name.Length + 1);
        sb.Append('/');
        foreach (byte b in Encoding.UTF8.GetBytes(name))
        {
            if (b < 0x21 || b > 0x7E || "#()<>[]{}/%".IndexOf((char)b) >= 0)
                sb.Append('#').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            else
                sb.Append((char)b);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats a date as PDF date string (ISO 32000-2, 7.9.4), e.g. D:20261122153000+01'00'.
    /// The suffix is the offset from UTC; "Z" means UTC.
    /// </summary>
    public static string Date(DateTimeOffset date)
    {
        var sb = new StringBuilder("D:");
        sb.Append(date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        var offset = date.Offset;
        if (offset == TimeSpan.Zero)
        {
            sb.Append('Z');
        }
        else
        {
            sb.Append(offset < TimeSpan.Zero ? '-' : '+');
            var abs = offset.Duration();
            sb.Append(abs.Hours.ToString("00", CultureInfo.InvariantCulture)).Append('\'');
            sb.Append(abs.Minutes.ToString("00", CultureInfo.InvariantCulture)).Append('\'');
        }
        return sb.ToString();
    }
}
