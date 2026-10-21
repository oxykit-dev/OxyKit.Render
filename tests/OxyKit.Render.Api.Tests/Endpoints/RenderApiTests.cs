using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OxyKit.Render.Api.Tests.Endpoints;

public sealed class RenderApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.UseSetting("Security:ApiKey", "test-key");
        // TestServer connections are not loopback; exercise API-key path only.
        builder.UseSetting("Security:RequireLocalRequests", "false");
    });

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("ok", payload!["status"].GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload["version"].GetString()));
        Assert.Equal(64, payload["fingerprint"].GetString()!.Length);
    }

    [Fact]
    public async Task TableHtml_IncludesEngineVersionHeaders()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "Orders",
                    headers = new[] { "Id" },
                    rows = new object[][] { [1] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/html",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-OxyKit-Render-Version", out var versions));
        Assert.True(
            response.Headers.TryGetValues("X-OxyKit-Render-Fingerprint", out var fingerprints)
        );
        Assert.False(string.IsNullOrWhiteSpace(versions.Single()));
        Assert.Equal(64, fingerprints.Single().Length);

        var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var payload = await health.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(versions.Single(), payload!["version"].GetString());
        Assert.Equal(fingerprints.Single(), payload["fingerprint"].GetString());
    }

    [Fact]
    public async Task Health_V1Alias_IsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/v1/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Formats_RequiresApiKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Render_RequiresApiKey()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/render/table/html",
            new
            {
                sheets = new[]
                {
                    new
                    {
                        name = "Orders",
                        headers = new[] { "Id" },
                        rows = new object[][] { [1] },
                    },
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("API key", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Render_RejectsWrongApiKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "wrong-key");

        var response = await client.PostAsJsonAsync(
            "/render/html/pdf",
            new { content = "<p>hi</p>" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Formats_SucceedsWithApiKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.GetAsync("/formats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<FormatsPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(payload);
        Assert.Contains("html/pdf", payload.Formats);
        Assert.Contains("csv/pdf", payload.Formats);
        Assert.Contains("csv/xlsx", payload.Formats);
        Assert.Contains("table/html", payload.Formats);
        Assert.Contains("table/pdf", payload.Formats);
        Assert.Contains("table/xlsx", payload.Formats);
        Assert.Contains("table/csv", payload.Formats);
        Assert.Contains("template/html", payload.Formats);
        Assert.Contains("template/pdf", payload.Formats);
    }

    [Fact]
    public async Task HtmlPdf_RejectsMissingContent()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/html/pdf",
            new { pageSize = "A4" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("content", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HtmlPdf_RejectsOversizedContent()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
            builder.UseSetting("Security:ApiKey", "test-key");
            builder.UseSetting("Security:RequireLocalRequests", "false");
            builder.UseSetting("Rendering:MaxHtmlContentChars", "32");
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/html/pdf",
            new { content = new string('x', 64) },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("maximum length", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HtmlPdf_ReturnsPdf()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/html/pdf",
            new { content = "<html><body><p>Hello OxyKit.Render</p></body></html>" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes.AsSpan(0, 4)));
    }

    [Fact]
    public async Task HtmlPdf_RejectsUnknownPageSize()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/html/pdf",
            new { content = "<p>x</p>", pageSize = "Undefined" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TableHtml_ReturnsStandaloneHtml()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "Orders",
                    headers = new[] { "Id", "Customer" },
                    rows = new object[][] { [1, "Acme"], [2, "Beta"] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/html",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("class=\"pf-document\"", html);
        Assert.Contains("<table>", html);
        Assert.Contains("Orders", html);
        Assert.Contains("Acme", html);
        Assert.Contains("body.pf-document", html);
        Assert.Contains("@page", html);
    }

    [Fact]
    public async Task TablePdf_ReturnsPdf()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "Orders",
                    headers = new[] { "Id", "Customer" },
                    rows = new object[][] { [1, "Acme"] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/pdf",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes.AsSpan(0, 4)));
    }

    [Fact]
    public async Task TableXlsx_ReturnsSpreadsheet()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "Orders",
                    headers = new[] { "Id", "Customer" },
                    rows = new object[][] { [1, "Acme"], [2, "Beta"] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/xlsx",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType
        );
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task CsvXlsx_ReturnsSpreadsheet()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/csv/xlsx",
            new
            {
                content = "Id,Customer,Day\n1,Acme,2024-01-15\n2,Beta,2024-02-01",
                name = "Orders",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType
        );

        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        using var stream = new MemoryStream(bytes);
        using var xl = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = xl.Worksheet("Orders");
        Assert.Equal(ClosedXML.Excel.XLDataType.DateTime, sheet.Cell(2, 3).DataType);
        Assert.Equal(new DateTime(2024, 1, 15), sheet.Cell(2, 3).GetDateTime());
    }

    [Fact]
    public async Task CsvPdf_ReturnsPdf()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/csv/pdf",
            new { content = "Id,Customer\n1,Acme\n2,Beta", name = "Orders" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes.AsSpan(0, 4)));
    }

    [Fact]
    public async Task CsvXlsx_RejectsMissingContent()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/csv/xlsx",
            new { name = "Orders" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("content", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TableCsv_ReturnsUtf8BomCsv()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "Orders",
                    headers = new[] { "Id", "Customer", "Note" },
                    rows = new object?[][] { [1, "Acme, Inc", "say \"hi\""], [2, "Beta", null] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/csv",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var text = Encoding.UTF8.GetString(bytes.AsSpan(3));
        Assert.Contains("Id,Customer,Note", text);
        Assert.Contains("\"Acme, Inc\"", text);
        Assert.Contains("\"say \"\"hi\"\"\"", text);
        Assert.Contains("2,Beta,", text);
    }

    [Fact]
    public async Task TableCsv_RejectsMultipleSheets()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var body = new
        {
            sheets = new[]
            {
                new
                {
                    name = "A",
                    headers = new[] { "Id" },
                    rows = new object[][] { [1] },
                },
                new
                {
                    name = "B",
                    headers = new[] { "Id" },
                    rows = new object[][] { [2] },
                },
            },
        };

        var response = await client.PostAsJsonAsync(
            "/render/table/csv",
            body,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("exactly one sheet", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Templates_ListsInvoiceSample()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.GetAsync("/templates", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TemplatesPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(payload);
        Assert.Contains(payload.Templates, t => t.Id == "invoice" && t.HasContract);
    }

    [Fact]
    public async Task TemplateHtml_ReturnsRenderedMarkup()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/template/html",
            new
            {
                templateId = "invoice",
                data = new
                {
                    invoiceNumber = "INV-100",
                    customerName = "Acme",
                    total = 120.5,
                    lines = new[] { new { description = "Widget", amount = 120.5 } },
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("INV-100", html);
        Assert.Contains("Acme", html);
        Assert.Contains("Widget", html);
        Assert.Contains("class=\"pf-document\"", html);
        Assert.Contains("<table>", html);
        Assert.Contains("body.pf-document", html);
        Assert.Contains("@page", html);
        Assert.Contains("pf-num", html);

        var baselineAt = html.IndexOf(
            OxyKit.Render.Core.Outputs.PrintBaselineCss.Content,
            StringComparison.Ordinal
        );
        var overrideAt = html.IndexOf("font-size: 18pt", StringComparison.Ordinal);
        Assert.True(baselineAt >= 0);
        Assert.True(overrideAt > baselineAt);
    }

    [Fact]
    public async Task TemplateHtml_RejectsAdditionalProperties()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/template/html",
            new
            {
                templateId = "invoice",
                data = new
                {
                    invoiceNumber = "INV-100",
                    customerName = "Acme",
                    total = 120.5,
                    lines = new[] { new { description = "Widget", amount = 120.5 } },
                    unexpected = true,
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("invoice", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TemplatePdf_ReturnsPdf()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/template/pdf",
            new
            {
                templateId = "invoice",
                data = new
                {
                    invoiceNumber = "INV-100",
                    customerName = "Acme",
                    total = 120.5,
                    lines = new[] { new { description = "Widget", amount = 120.5 } },
                },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes.AsSpan(0, 4)));
    }

    [Fact]
    public async Task TemplatePdf_RejectsInvalidData()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/template/pdf",
            new { templateId = "invoice", data = new { invoiceNumber = "INV-1" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("invoice", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TemplatePdf_RejectsUnknownTemplate()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-OxyKit-Render-Key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/render/template/pdf",
            new { templateId = "nope", data = new { } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>(
            TestContext.Current.CancellationToken
        );
        Assert.Contains("not found", payload!.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record FormatsPayload(IReadOnlyCollection<string> Formats);

    private sealed record TemplatesPayload(IReadOnlyList<TemplateItem> Templates);

    private sealed record TemplateItem(string Id, bool HasContract);

    private sealed record ErrorPayload(string Error);
}
