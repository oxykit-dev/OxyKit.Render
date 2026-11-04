using System.Text.Json;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TemplateDataValidatorTests
{
    private const string Contract = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["name"],
          "properties": {
            "name": { "type": "string", "minLength": 1 }
          }
        }
        """;

    [Fact]
    public void Validate_AcceptsMatchingObject()
    {
        using var doc = JsonDocument.Parse("""{"name":"Acme"}""");
        TemplateDataValidator.Validate(Contract, doc.RootElement, "invoice");
    }

    [Fact]
    public void Validate_RejectsMissingRequiredField()
    {
        using var doc = JsonDocument.Parse("{}");
        var ex = Assert.Throws<ArgumentException>(() =>
            TemplateDataValidator.Validate(Contract, doc.RootElement, "invoice")
        );
        Assert.Contains("invoice", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsNonObject()
    {
        using var doc = JsonDocument.Parse("[1,2]");
        var ex = Assert.Throws<ArgumentException>(() =>
            TemplateDataValidator.Validate(Contract, doc.RootElement, "invoice")
        );
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ScribanTemplateRendererTests
{
    [Fact]
    public void Render_HtmlEncodesStringData()
    {
        var html = ScribanTemplateRenderer.Render(
            "<p>{{ name }}</p>",
            JsonDocument.Parse("""{"name":"<b>Acme</b>"}""").RootElement
        );

        Assert.Equal("<p>&lt;b&gt;Acme&lt;/b&gt;</p>", html);
    }

    [Fact]
    public void Render_InterpolatesNestedValues()
    {
        var html = ScribanTemplateRenderer.Render(
            "<p>{{ customer.name }}-{{ total }}</p>",
            JsonDocument.Parse("""{"customer":{"name":"Beta"},"total":12}""").RootElement
        );

        Assert.Equal("<p>Beta-12</p>", html);
    }

    [Fact]
    public void Render_QrHelper_EmitsInlineSvg()
    {
        var html = ScribanTemplateRenderer.Render(
            "<div>{{ qr code }}</div>",
            JsonDocument.Parse("""{"code":"INV-100"}""").RootElement
        );

        Assert.Contains("<svg ", html);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", html);
        Assert.DoesNotContain("data:image/png", html);
        Assert.DoesNotContain("{{ qr", html);
    }

    [Fact]
    public void Render_QrHelper_NamedPixelsAndEcc()
    {
        var html = ScribanTemplateRenderer.Render(
            "{{ qr code pixels: 6 ecc: \"H\" }}",
            JsonDocument.Parse("""{"code":"ABC"}""").RootElement
        );

        Assert.Contains("<svg ", html);
    }

    [Fact]
    public void Render_QrHelper_StillHtmlEncodesPlainInterpolations()
    {
        var html = ScribanTemplateRenderer.Render(
            "<p>{{ name }}</p>{{ qr name }}",
            JsonDocument.Parse("""{"name":"<b>Acme</b>"}""").RootElement
        );

        Assert.Contains("<p>&lt;b&gt;Acme&lt;/b&gt;</p>", html);
        Assert.Contains("<svg ", html);
        Assert.DoesNotContain("<p><b>Acme</b></p>", html);
    }

    [Fact]
    public void Render_QrBuiltin_WinsOverDataFieldNamedQr()
    {
        var html = ScribanTemplateRenderer.Render(
            "{{ qr code }}",
            JsonDocument.Parse("""{"qr":"shadow","code":"OK"}""").RootElement
        );

        Assert.Contains("<svg ", html);
    }

    [Fact]
    public void Render_BarcodeHelper_EmitsInlineSvg()
    {
        var html = ScribanTemplateRenderer.Render(
            "{{ barcode \"code128\" sku }}",
            JsonDocument.Parse("""{"sku":"ABC-123"}""").RootElement
        );

        Assert.Contains("<svg ", html);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", html);
    }
}
