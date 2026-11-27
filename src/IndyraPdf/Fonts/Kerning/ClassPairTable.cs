// Kerning values per pair of glyph classes (GPOS pair adjustment format 2).
//
// Instead of listing every pair, the font groups glyphs into classes (e.g. all forms of "A"
// with accents) and stores one value per class pair in a matrix. This keeps large kerning sets
// small.

namespace IndyraPdf.Fonts.Kerning;

/// <summary>Kerning values for pairs of glyph classes.</summary>
internal sealed class ClassPairTable : IPairAdjustment
{
    /// <summary>Glyphs this table applies to as left glyph.</summary>
    readonly HashSet<int> _coverage;

    /// <summary>Class of each left glyph (missing = class 0).</summary>
    readonly Dictionary<int, int> _leftClasses;

    /// <summary>Class of each right glyph (missing = class 0).</summary>
    readonly Dictionary<int, int> _rightClasses;

    /// <summary>Adjustment per [left class, right class].</summary>
    readonly int[,] _values;

    /// <summary>Creates the table.</summary>
    public ClassPairTable(HashSet<int> coverage, Dictionary<int, int> leftClasses, Dictionary<int, int> rightClasses, int[,] values)
    {
        _coverage = coverage;
        _leftClasses = leftClasses;
        _rightClasses = rightClasses;
        _values = values;
    }

    /// <inheritdoc/>
    public bool TryGetAdjustment(int left, int right, out int adjustment)
    {
        adjustment = 0;
        if (!_coverage.Contains(left))
            return false;
        int c1 = _leftClasses.GetValueOrDefault(left);
        int c2 = _rightClasses.GetValueOrDefault(right);
        if (c1 >= _values.GetLength(0) || c2 >= _values.GetLength(1))
            return false;
        adjustment = _values[c1, c2];
        // In OpenType a covered left glyph ends the search in this subtable, even with value 0.
        return true;
    }
}
