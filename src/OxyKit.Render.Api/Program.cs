using System.Text.Json.Serialization;
using OxyKit.Render.Api.Endpoints;
using OxyKit.Render.Api.OpenApi;
using OxyKit.Render.Api.Security;
using OxyKit.Render.Core;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder
    .Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.ApiKey)
            && !string.Equals(options.ApiKey, "change-me-in-production", StringComparison.Ordinal),
        "Security:ApiKey must be set to a non-placeholder value."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<RenderingOptions>()
    .Bind(builder.Configuration.GetSection(RenderingOptions.SectionName))
    .Validate(
        options =>
            options.MaxHtmlContentChars > 0
            && options.MaxCsvContentChars > 0
            && options.MaxTemplateChars > 0
            && options.MaxContractChars > 0
            && options.MaxRequestBodyBytes > 0
            && options.MaxConcurrentRenders > 0
            && !string.IsNullOrWhiteSpace(options.TemplatesRoot),
        "Rendering limits must be positive and TemplatesRoot must be set."
    )
    .ValidateOnStart();

var renderingOptions =
    builder.Configuration.GetSection(RenderingOptions.SectionName).Get<RenderingOptions>()
    ?? new RenderingOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = renderingOptions.MaxRequestBodyBytes;
});

builder.Services.AddRenderCore();
builder.Services.AddRenderOpenApi();

var app = builder.Build();

app.UseMiddleware<InternalAccessMiddleware>();

// Suite OpenAPI convention: JSON + Scalar in Development only.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapRenderEndpoints();

// Cloud/Local compatibility: same handlers under /v1/*
app.MapGroup("/v1").MapHealthEndpoints();
app.MapGroup("/v1").MapRenderEndpoints();

app.Run();

public partial class Program;
