using System.Text.Json;
using OxyKit.Render.Core;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TemplateAssetAndIncludeTests : IDisposable
{
    private readonly string _dir;

    public TemplateAssetAndIncludeTests()
    {
        _dir = Path.Combine(
            Path.GetTempPath(),
            "oxykit-template-assets-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Path.Combine(_dir, "assets"));
        Directory.CreateDirectory(Path.Combine(_dir, "partials"));
        File.WriteAllText(
            Path.Combine(_dir, "assets", "logo.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10" fill="#000"/></svg>"""
        );
        File.WriteAllText(
            Path.Combine(_dir, "partials", "chip.html"),
            "<span class=\"chip\">{{ label }}</span>"
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Asset_EmitsDataUriImg()
    {
        var html = ScribanTemplateRenderer.Render(
            """{{ asset "assets/logo.svg" width: "28mm" alt: "Logo" }}""",
            JsonDocument.Parse("{}").RootElement,
            _dir,
            new RenderingOptions()
        );

        Assert.StartsWith("<img src=\"data:image/svg+xml;base64,", html);
        Assert.Contains("width=\"28mm\"", html);
        Assert.Contains("alt=\"Logo\"", html);
    }

    [Fact]
    public void Asset_RejectsPathTraversal()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ScribanTemplateRenderer.Render(
                """{{ asset "../secrets.png" }}""",
                JsonDocument.Parse("{}").RootElement,
                _dir,
                new RenderingOptions()
            )
        );
        Assert.Contains("Template render failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Include_RendersPartialWithData()
    {
        var html = ScribanTemplateRenderer.Render(
            """{{ include "partials/chip.html" }}""",
            JsonDocument.Parse("""{"label":"OK"}""").RootElement,
            _dir,
            new RenderingOptions()
        );

        Assert.Equal("<span class=\"chip\">OK</span>", html);
    }

    [Fact]
    public void Include_RejectsNonHtml()
    {
        File.WriteAllText(Path.Combine(_dir, "assets", "x.txt"), "nope");
        var ex = Assert.Throws<ArgumentException>(() =>
            ScribanTemplateRenderer.Render(
                """{{ include "assets/x.txt" }}""",
                JsonDocument.Parse("{}").RootElement,
                _dir,
                new RenderingOptions()
            )
        );
        Assert.Contains("Template render failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
