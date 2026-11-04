using System.Globalization;
using Scriban.Runtime;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Template-folder assets → <c>data:</c> <c>img</c> (PeachPDF blocks file/HTTP fetch).
/// Registered as static Scriban builtins; directory comes from a render-scoped <see cref="AsyncLocal{T}"/>.
/// </summary>
internal static class TemplateAssetFunctions
{
    private static readonly AsyncLocal<Scope?> Current = new();

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".svg",
    };

    private static readonly Dictionary<string, string> MediaTypes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
    };

    public static void Register(ScriptObject target) =>
        target.Import(typeof(TemplateAssetFunctions), member => member.Name == nameof(asset));

    public static IDisposable BeginScope(string templateDirectory, int maxAssetBytes)
    {
        var previous = Current.Value;
        Current.Value = new Scope(templateDirectory, maxAssetBytes);
        return new ScopePopper(previous);
    }

    /// <summary>
    /// Inline a template asset as <c>&lt;img src="data:…"&gt;</c>.
    /// Usage: <c>{{ asset "assets/logo.svg" }}</c>, optional <c>width:</c> / <c>height:</c> / <c>alt:</c>.
    /// </summary>
#pragma warning disable IDE1006
    public static string asset(
        string? path,
        string? width = null,
        string? height = null,
        string? alt = null
    )
    {
        var scope =
            Current.Value
            ?? throw new InvalidOperationException(
                "asset helper requires a template directory scope."
            );

        var fullPath = TemplatePathSafety.ResolveUnderTemplateRoot(
            scope.TemplateDirectory,
            path,
            "asset"
        );
        var ext = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException(
                "asset type must be one of: png, jpg, jpeg, gif, webp, svg."
            );
        }

        if (!File.Exists(fullPath))
            throw new ArgumentException($"asset '{path}' was not found in the template folder.");

        var info = new FileInfo(fullPath);
        if (info.Length <= 0)
            throw new ArgumentException($"asset '{path}' is empty.");
        if (info.Length > scope.MaxAssetBytes)
        {
            throw new ArgumentException(
                $"asset '{path}' exceeds the maximum size of {scope.MaxAssetBytes:N0} bytes."
            );
        }

        var bytes = File.ReadAllBytes(fullPath);
        var mediaType = MediaTypes[ext];
        var b64 = Convert.ToBase64String(bytes);
        var altText = string.IsNullOrWhiteSpace(alt) ? "" : alt.Trim();
        altText = System.Net.WebUtility.HtmlEncode(altText);

        var widthAttr = FormatSizeAttr("width", width);
        var heightAttr = FormatSizeAttr("height", height);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"<img src=\"data:{mediaType};base64,{b64}\" alt=\"{altText}\"{widthAttr}{heightAttr} />"
        );
    }
#pragma warning restore IDE1006

    private static string FormatSizeAttr(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var v = value.Trim();
        if (v.Length > 32 || v.Contains('"') || v.Contains('<') || v.Contains('>'))
            throw new ArgumentException($"asset {name} is invalid.");
        return $" {name}=\"{System.Net.WebUtility.HtmlEncode(v)}\"";
    }

    private sealed record Scope(string TemplateDirectory, int MaxAssetBytes);

    private sealed class ScopePopper(Scope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
