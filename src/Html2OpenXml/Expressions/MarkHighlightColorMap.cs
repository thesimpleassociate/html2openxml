// BP addition (not in upstream html2openxml): maps Briefpoint highlight-* CSS classes on <mark>
// to Word highlight colors so the response editor's color picker survives into the generated docx.
// Kept in its own file to keep the fork's delta from upstream small and easy to re-apply.
using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Expressions;

internal static class MarkHighlightColorMap
{
    private static readonly Dictionary<string, HighlightColorValues> ByClassName =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["highlight-yellow"] = HighlightColorValues.Yellow,
        ["highlight-red"]    = HighlightColorValues.Red,
        ["highlight-blue"]   = HighlightColorValues.Blue,
        ["highlight-green"]  = HighlightColorValues.Green,
        ["highlight-pink"]   = HighlightColorValues.Magenta, // Word has no pink; Magenta is closest
    };

    /// <summary>
    /// Returns a <see cref="Highlight"/> for the first recognized <c>highlight-*</c> class in
    /// <paramref name="classList"/>, or <c>null</c> when none match.
    /// </summary>
    public static Highlight? TryGetHighlight(IEnumerable<string> classList)
    {
        if (classList is null)
        {
            return null;
        }

        foreach (var className in classList)
        {
            if (ByClassName.TryGetValue(className, out var color))
            {
                return new Highlight { Val = color };
            }
        }

        return null;
    }
}
