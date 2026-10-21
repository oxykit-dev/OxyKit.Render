using Microsoft.OpenApi;

namespace OxyKit.Render.Api.OpenApi;

internal static class OpenApiSchemaHelpers
{
    public static void SetPageOptionDescriptions(IDictionary<string, IOpenApiSchema> properties)
    {
        SetDescription(properties, "pageSize", "PDF page size. Defaults to A4 when omitted.");
        SetDescription(
            properties,
            "pageOrientation",
            "PDF page orientation. Defaults to Portrait when omitted."
        );
    }

    public static void SetDescription(
        IDictionary<string, IOpenApiSchema> properties,
        string name,
        string description
    )
    {
        if (properties.TryGetValue(name, out var property) && property is OpenApiSchema concrete)
        {
            concrete.Description = description;
        }
    }
}
