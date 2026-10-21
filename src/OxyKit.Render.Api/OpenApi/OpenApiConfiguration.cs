using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OxyKit.Render.Api.Security;

namespace OxyKit.Render.Api.OpenApi;

internal static class OpenApiConfiguration
{
    public static IServiceCollection AddRenderOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<RenderDocumentTransformer>();
            options.AddSchemaTransformer<RenderRequestSchemaTransformer>();
        });

        return services;
    }
}

internal sealed class RenderDocumentTransformer(IOptions<SecurityOptions> securityOptions)
    : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "OxyKit.Render API",
            Version = "v1",
            Description =
                "HTTP API that renders documents to PDF, Excel, CSV, and HTML (csv/pdf, csv/xlsx, html/pdf, table/html, table/pdf, table/xlsx, table/csv, template/html, template/pdf).",
        };

        var apiKeyHeader = securityOptions.Value.ApiKeyHeader;
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = apiKeyHeader,
            Description = $"API key required in the `{apiKeyHeader}` header.",
        };

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            var isAnonymous = AnonymousPaths.Matches(path);

            foreach (var operation in pathItem.Operations.Values)
            {
                if (isAnonymous)
                {
                    operation.Security = [];
                    continue;
                }

                operation.Security ??= [];
                operation.Security.Add(
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("ApiKey", document)] = [],
                    }
                );

                EnsureAuthErrorResponses(operation);
            }
        }

        return Task.CompletedTask;
    }

    private static void EnsureAuthErrorResponses(OpenApiOperation operation)
    {
        operation.Responses ??= [];
        operation.Responses.TryAdd(
            "401",
            new OpenApiResponse
            {
                Description = "Missing or invalid API key. Body: `{ \"error\": \"...\" }`.",
            }
        );
        operation.Responses.TryAdd(
            "403",
            new OpenApiResponse
            {
                Description =
                    "Non-local client while Security:RequireLocalRequests is true. Body: `{ \"error\": \"...\" }`.",
            }
        );
    }
}
