// Rectangle around glyph outlines, used for the font metrics in TrueTypeFont.

namespace IndyraPdf.Fonts;

/// <summary>Rectangle in font units, given by its lower left and upper right corner.</summary>
/// <param name="XMin">Left edge.</param>
/// <param name="YMin">Bottom edge (negative below the baseline).</param>
/// <param name="XMax">Right edge.</param>
/// <param name="YMax">Top edge.</param>
public readonly record struct FontBoundingBox(int XMin, int YMin, int XMax, int YMax);
