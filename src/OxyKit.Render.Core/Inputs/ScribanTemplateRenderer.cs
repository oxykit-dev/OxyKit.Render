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
    public static string Render(string templateHtml, JsonElement data)
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

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        try
        {
            return template.Render(context);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Template render failed: {Sanitize(ex.Message)}");
        }
    }

    private static object? ToScribanValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ToScriptObject(element),
            JsonValueKind.Array => ToScriptArray(element),
            JsonValueKind.String => WebUtility.HtmlEncode(element.GetString() ?? string.Empty),
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
