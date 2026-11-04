using System.Net;
using System.Text.Json;
using Scriban;
using Scriban.Runtime;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Renders Scriban HTML templates. String values from <c>data</c> are HTML-encoded
/// on import so interpolations are safe by default (template authors are trusted; data is not).
/// </summary>
internal static class ScribanTemplateRenderer
{
    public static string Render(string templateHtml, JsonElement data) =>
        Render(templateHtml, data, templateDirectory: null, options: null);

    public static string Render(
        string templateHtml,
        JsonElement data,
        string? templateDirectory,
        RenderingOptions? options
    )
    {
        Template template;
        try
        {
            template = Template.Parse(templateHtml);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Template parse failed: {Sanitize(ex.Message)}");
        }

        if (template.HasErrors)
        {
            var message = string.Join("; ", template.Messages.Select(m => m.Message));
            throw new ArgumentException($"Template parse failed: {Sanitize(message)}");
        }

        var scriptObject = new ScriptObject();
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in data.EnumerateObject())
            {
                scriptObject[property.Name] = ToScribanValue(property.Value);
            }
        }

        // Builtins last so data fields cannot shadow helpers (e.g. a property named `qr`).
        TemplateFormatFunctions.Register(scriptObject);
        TemplateQrFunctions.Register(scriptObject);
        TemplateAssetFunctions.Register(scriptObject);

        var context = new TemplateContext();
        IDisposable? assetScope = null;
        if (!string.IsNullOrWhiteSpace(templateDirectory))
        {
            var maxAsset = options?.MaxTemplateAssetBytes ?? 512_000;
            var maxPartial = options?.MaxTemplatePartialChars ?? 200_000;
            assetScope = TemplateAssetFunctions.BeginScope(templateDirectory, maxAsset);
            context.TemplateLoader = new TemplateFileLoader(templateDirectory, maxPartial);
        }

        context.PushGlobal(scriptObject);

        try
        {
            return template.Render(context);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Template render failed: {Sanitize(ex.Message)}");
        }
        finally
        {
            assetScope?.Dispose();
        }
    }

    private static object? ToScribanValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ToScriptObject(element),
            JsonValueKind.Array => ToScriptArray(element),
            JsonValueKind.String => WebUtility.HtmlEncode(element.GetString() ?? string.Empty),
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => WebUtility.HtmlEncode(element.ToString()),
        };

    private static ScriptObject ToScriptObject(JsonElement element)
    {
        var obj = new ScriptObject();
        foreach (var property in element.EnumerateObject())
        {
            obj[property.Name] = ToScribanValue(property.Value);
        }

        return obj;
    }

    private static ScriptArray ToScriptArray(JsonElement element)
    {
        var array = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(ToScribanValue(item));
        }

        return array;
    }

    private static string Sanitize(string message)
    {
        // Keep messages useful but avoid leaking full paths from the host.
        return message.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
