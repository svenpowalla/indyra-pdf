// Link from a structure element to a piece of content on a page.

namespace IndyraPdf.Writer.Tagging;

/// <summary>
/// A marked-content sequence on a page: content between "BDC" and "EMC" with the marked-content
/// identifier (MCID) <paramref name="Mcid"/>. Structure elements list these as their kids.
/// </summary>
/// <param name="PageIndex">Page of the content, starting at 0.</param>
/// <param name="Mcid">Number of the sequence on that page, starting at 0.</param>
internal sealed record MarkedContentReference(int PageIndex, int Mcid);
