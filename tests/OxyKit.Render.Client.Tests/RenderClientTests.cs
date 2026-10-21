using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OxyKit.Render.Contracts;

namespace OxyKit.Render.Client.Tests;

public sealed class RenderClientTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.UseSetting("Security:ApiKey", "test-key");
        builder.UseSetting("Security:RequireLocalRequests", "false");
    });

    private RenderClient CreateClient(bool useV1 = false)
    {
        var http = _factory.CreateClient();
        http.BaseAddress = new Uri(useV1 ? http.BaseAddress + "v1/" : http.BaseAddress!.ToString());
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-OxyKit-Render-Key", "test-key");
        return new RenderClient(http);
    }

    [Fact]
    public async Task GetHealth_WorksWithoutApiKey()
    {
        var http = _factory.CreateClient();
        using var client = new RenderClient(http);

        var health = await client.GetHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ok", health.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.Version));
        Assert.Equal(64, health.Fingerprint.Length);
    }

    [Fact]
    public async Task GetFormats_ReturnsKnownTransforms()
    {
        using var client = CreateClient();

        var formats = await client.GetFormatsAsync(TestContext.Current.CancellationToken);

        Assert.Contains("html/pdf", formats.Formats);
        Assert.Contains("table/csv", formats.Formats);
    }

    [Fact]
    public async Task GetFormats_ViaV1Alias()
    {
        using var client = CreateClient(useV1: true);

        var formats = await client.GetFormatsAsync(TestContext.Current.CancellationToken);

        Assert.Contains("html/pdf", formats.Formats);
    }

    [Fact]
    public async Task RenderTableHtml_ReturnsHtmlBytes()
    {
        using var client = CreateClient();

        var result = await client.RenderTableHtmlAsync(
            new TableRenderRequest
            {
                Sheets =
                [
                    new TableSheet
                    {
                        Name = "Orders",
                        Headers = ["Id"],
                        Rows =
                        [
                            [JsonSerializer.SerializeToElement(1)],
                        ],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Contains("text/html", result.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Content);
        Assert.False(string.IsNullOrWhiteSpace(result.ComputeSha256()));
        Assert.False(string.IsNullOrWhiteSpace(result.EngineVersion));
        Assert.Equal(64, result.EngineFingerprint!.Length);
    }

    [Fact]
    public async Task Render_WithoutKey_ThrowsApiException()
    {
        var http = _factory.CreateClient();
        using var client = new RenderClient(http);

        var ex = await Assert.ThrowsAsync<RenderApiException>(() =>
            client.RenderHtmlPdfAsync(
                new HtmlPdfRenderRequest { Content = "<p>x</p>" },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal((int)HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
