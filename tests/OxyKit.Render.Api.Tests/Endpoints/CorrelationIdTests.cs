using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OxyKit.Render.Api.Tests.Endpoints;

public sealed class CorrelationIdTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.UseSetting("Security:ApiKey", "test-key");
        builder.UseSetting("Security:RequireLocalRequests", "false");
    });

    [Fact]
    public async Task Health_WithoutCorrelationHeader_GeneratesAndEchoes()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var id = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public async Task Health_WithCorrelationHeader_EchoesSameValue()
    {
        var client = _factory.CreateClient();
        const string expected = "render-corr-test-001";
        client.DefaultRequestHeaders.Add("X-Correlation-Id", expected);

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(expected, Assert.Single(values));
    }
}
