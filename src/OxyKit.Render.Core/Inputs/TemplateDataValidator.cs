using System.Collections.Concurrent;
using System.Text.Json;
using Json.Schema;

namespace OxyKit.Render.Core.Inputs;

internal static class TemplateDataValidator
{
    // JsonSchema.FromText registers $id into a global registry and rejects overwrites.
    // Cache by contract text so the same on-disk contract can be validated repeatedly.
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> SchemaCache = new(
        StringComparer.Ordinal
    );

    public static void Validate(string contractJson, JsonElement data, string templateId)
    {
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new ArgumentException("Field 'data' is required.");
        }

        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Field 'data' must be a JSON object.");
        }

        var schema = GetSchema(contractJson, templateId);

        var results = schema.Evaluate(
            data,
            new EvaluationOptions { OutputFormat = OutputFormat.List }
        );

        if (results.IsValid)
        {
            return;
        }

        var errors = CollectErrors(results);
        var detail =
            errors.Count == 0 ? "does not match the template contract." : string.Join("; ", errors);
        throw new ArgumentException($"Template '{templateId}' data {detail}");
    }

    private static JsonSchema GetSchema(string contractJson, string templateId)
    {
        try
        {
            return SchemaCache
                .GetOrAdd(
                    contractJson,
                    static json => new Lazy<JsonSchema>(() => JsonSchema.FromText(json))
                )
                .Value;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Template '{templateId}' has an invalid contract.json: {ex.Message}",
                ex
            );
        }
    }

    private static List<string> CollectErrors(EvaluationResults results)
    {
        var errors = new List<string>();
        CollectErrors(results, errors);
        return errors;
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (results.Errors is { Count: > 0 })
        {
            foreach (var error in results.Errors)
            {
                var path = results.InstanceLocation.ToString();
                if (string.IsNullOrEmpty(path) || path == "#")
                {
                    path = "$";
                }

                errors.Add($"{path}: {error.Value}");
            }
        }

        if (results.Details is null)
        {
            return;
        }

        foreach (var detail in results.Details)
        {
            CollectErrors(detail, errors);
        }
    }
}
