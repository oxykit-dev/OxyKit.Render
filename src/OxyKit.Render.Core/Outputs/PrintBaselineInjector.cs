using System.Text.RegularExpressions;

namespace OxyKit.Render.Core.Outputs;

/// <summary>
/// Inlines <see cref="PrintBaselineCss"/> into rendered HTML so preview and PeachPDF
/// see the same styles (linked/remote CSS is not loaded).
/// </summary>
internal static partial class PrintBaselineInjector
{
    private static readonly string StyleBlock =
        "<style>\n" + PrintBaselineCss.Content + "\n</style>";

    public static string Apply(string html)
    {
        ArgumentException.ThrowIfNullOrEmpty(html);

        var headOpen = HeadOpenTag().Match(html);
        if (headOpen.Success)
        {
            var insertAt = headOpen.Index + headOpen.Length;
            return html[..insertAt] + "\n" + StyleBlock + "\n" + html[insertAt..];
        }

        var bodyOpen = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyOpen >= 0)
        {
            return html[..bodyOpen] + StyleBlock + "\n" + html[bodyOpen..];
        }

        return StyleBlock + "\n" + html;
    }

    [GeneratedRegex(@"<head\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadOpenTag();
}
