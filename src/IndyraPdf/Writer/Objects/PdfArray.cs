// PDF array object, written as [ ... ].

namespace IndyraPdf.Writer.Objects;

/// <summary>An array of PDF objects (ISO 32000-2, 7.3.6), e.g. a page size [0 0 595 842].</summary>
internal sealed class PdfArray : PdfObject
{
    /// <summary>The elements in order.</summary>
    readonly List<PdfObject> _items = [];

    /// <summary>Creates an empty array.</summary>
    public PdfArray() { }

    /// <summary>Creates an array of numbers.</summary>
    public PdfArray(params double[] numbers)
    {
        foreach (var n in numbers)
            _items.Add(new PdfNumber(n));
    }

    /// <summary>Number of elements.</summary>
    public int Count => _items.Count;

    /// <summary>Element at a position.</summary>
    public PdfObject this[int index] => _items[index];

    /// <summary>Appends an element and returns the array, so calls can be chained.</summary>
    public PdfArray Add(PdfObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        return this;
    }

    /// <inheritdoc/>
    public override bool StartsWithDelimiter => true;

    /// <inheritdoc/>
    public override void WriteTo(PdfOutput output)
    {
        output.Write("[");
        for (int i = 0; i < _items.Count; i++)
        {
            if (i > 0)
                output.Write(" ");
            _items[i].WriteValueTo(output);
        }
        output.Write("]");
    }
}
