using System.Text.RegularExpressions;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Resolves relative paths under a template directory (no <c>..</c>, no absolute escapes).
/// </summary>
internal static partial class TemplatePathSafety
{
    public static string ResolveUnderTemplateRoot(
        string templateDirectory,
        string? relativePath,
        string what
    )
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException($"{what} path is required.");

        var relative = relativePath.Trim().Replace('\\', '/');
        if (relative.StartsWith('/') || relative.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{what} path must be a relative path under the template folder (no '..')."
            );
        }

        if (!SafeRelativePath().IsMatch(relative))
        {
            throw new ArgumentException(
                $"{what} path may only contain letters, digits, '.', '_', '-', and '/'."
            );
        }

        var root = Path.GetFullPath(templateDirectory);
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        if (
            !candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException($"{what} path escapes the template folder.");
        }

        return candidate;
    }

    [GeneratedRegex(
        @"^[A-Za-z0-9][A-Za-z0-9._/-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    )]
    private static partial Regex SafeRelativePath();
}
