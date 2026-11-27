// One page after page breaking: its template and the part of the flowing content on it.

using IndyraPdf.Layout.Flow;

namespace IndyraPdf.Layout;

/// <summary>A page after page breaking.</summary>
/// <param name="Template">The template used for the page.</param>
/// <param name="Content">The flowing content placed on the page, or null.</param>
internal sealed record LaidOutPage(PageTemplate Template, IFragment? Content);
