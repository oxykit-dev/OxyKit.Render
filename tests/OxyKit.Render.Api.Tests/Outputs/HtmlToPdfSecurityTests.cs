using System.Text;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Api.Tests;

public sealed class HtmlToPdfSecurityTests
{
    [Fact]
    public async Task RenderAsync_DoesNotThrow_ForRemoteImageMarkup()
    {
        // PeachPDF skips remote HTTP by default; AllowLocalFileAccess=false blocks file: URIs.
        var renderer = new HtmlToPdfRenderer();
        var html = """
            <html><body>
            <img src="https://example.invalid/secret.png" />
            <img src="file:///etc/passwd" />
            <p>ok</p>
            </body></html>
            """;

        var result = await renderer.RenderAsync(
            html,
            PdfPageOptions.Default,
            CancellationToken.None
        );

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.Content.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.Content.AsSpan(0, 4)));
    }
}
