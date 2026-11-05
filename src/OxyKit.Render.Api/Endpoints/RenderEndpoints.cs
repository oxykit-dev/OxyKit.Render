using OxyKit.Render.Contracts;
using OxyKit.Render.Core;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Endpoints;

internal static class RenderEndpoints
{
    public static IEndpointRouteBuilder MapRenderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        string Name(string baseName) => endpoints is RouteGroupBuilder ? baseName + "V1" : baseName;

        endpoints
            .MapGet(
                "/formats",
                (DocumentRenderService renderService) =>
                    Results.Ok(new FormatsResponse(renderService.SupportedFormats))
            )
            .WithName(Name("GetFormats"))
            .WithSummary("List supported transforms")
            .WithDescription(
                "Returns available input/output transforms (csv/pdf, csv/xlsx, html/pdf, table/html, table/pdf, table/xlsx, table/csv, template/html, template/pdf)."
            )
            .Produces<FormatsResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden);

        endpoints
            .MapGet(
                "/templates",
                (DocumentRenderService renderService) =>
                {
                    var items = renderService
                        .ListTemplates()
                        .Select(t => new TemplateListItem(t.Id, t.HasContract))
                        .ToArray();
                    return Results.Ok(new TemplatesResponse(items));
                }
            )
            .WithName(Name("GetTemplates"))
            .WithSummary("List local HTML templates")
            .WithDescription(
                "Returns template ids under Rendering:TemplatesRoot. Each entry notes whether contract.json is present."
            )
            .Produces<TemplatesResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden);

        endpoints
            .MapGet(
                "/templates/{templateId}",
                (string templateId, TemplateStore templateStore) =>
                {
                    try
                    {
                        var detail = templateStore.Describe(templateId);
                        return Results.Ok(
                            new TemplateDetailResponse(detail.Id, detail.HasContract, detail.Files)
                        );
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                }
            )
            .WithName(Name("GetTemplate"))
            .WithSummary("Get template pack metadata")
            .WithDescription(
                "Returns file list for a template id under Rendering:TemplatesRoot. "
                    + "Does not return file contents — use GET /templates/{id}/pack for a zip download."
            )
            .Produces<TemplateDetailResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        endpoints
            .MapGet(
                "/templates/{templateId}/pack",
                async (
                    string templateId,
                    TemplateStore templateStore,
                    CancellationToken cancellationToken
                ) =>
                {
                    try
                    {
                        var stream = new MemoryStream();
                        await templateStore.WriteZipAsync(templateId, stream, cancellationToken);
                        stream.Position = 0;
                        return Results.File(
                            stream,
                            "application/zip",
                            fileDownloadName: $"{templateId}.zip"
                        );
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                }
            )
            .WithName(Name("GetTemplatePack"))
            .WithSummary("Download template pack zip")
            .WithDescription(
                "Zips template.html, contract.json, and optional assets/, partials/, sample-data.json."
            )
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        endpoints
            .MapPut(
                "/templates/{templateId}",
                async (
                    string templateId,
                    HttpRequest request,
                    TemplateStore templateStore,
                    CancellationToken cancellationToken
                ) =>
                {
                    try
                    {
                        var contentType = request.ContentType ?? "";
                        if (
                            !contentType.Contains("application/zip", StringComparison.OrdinalIgnoreCase)
                            && !contentType.Contains(
                                "application/x-zip-compressed",
                                StringComparison.OrdinalIgnoreCase
                            )
                            && !contentType.Contains(
                                "application/octet-stream",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            return Results.BadRequest(
                                new ErrorResponse(
                                    "Content-Type must be application/zip (or application/octet-stream)."
                                )
                            );
                        }

                        var info = await templateStore.SaveFromZipAsync(
                            templateId,
                            request.Body,
                            cancellationToken
                        );
                        return Results.Ok(new TemplateListItem(info.Id, info.HasContract));
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                    catch (InvalidDataException ex)
                    {
                        return Results.BadRequest(new ErrorResponse(ex.Message));
                    }
                }
            )
            .WithName(Name("PutTemplate"))
            .WithSummary("Upload or replace a template pack")
            .WithDescription(
                "Accepts a zip whose root (or single top-level folder) contains template.html + contract.json; "
                    + "optional sample-data.json, partials/*.html, assets/* (png/jpg/gif/webp/svg). "
                    + "Replace-in-place under Rendering:TemplatesRoot. Trusted template markup — API key required."
            )
            .Produces<TemplateListItem>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .DisableAntiforgery();

        endpoints
            .MapDelete(
                "/templates/{templateId}",
                async (
                    string templateId,
                    TemplateStore templateStore,
                    CancellationToken cancellationToken
                ) =>
                {
                    try
                    {
                        await templateStore.DeleteAsync(templateId, cancellationToken);
                        return Results.NoContent();
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                }
            )
            .WithName(Name("DeleteTemplate"))
            .WithSummary("Delete a template pack")
            .WithDescription("Removes the template directory under Rendering:TemplatesRoot.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        endpoints
            .MapPut(
                "/templates/{templateId}/assets/{*assetPath}",
                async (
                    string templateId,
                    string assetPath,
                    HttpRequest request,
                    TemplateStore templateStore,
                    CancellationToken cancellationToken
                ) =>
                {
                    try
                    {
                        var path = await templateStore.SaveAssetAsync(
                            templateId,
                            assetPath,
                            request.Body,
                            cancellationToken
                        );
                        return Results.Ok(new TemplateAssetResponse(templateId, path));
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                }
            )
            .WithName(Name("PutTemplateAsset"))
            .WithSummary("Upload or replace a template asset")
            .WithDescription(
                "Writes an image under the pack's assets/ folder. Path is a label or relative name "
                    + "(e.g. logo.svg or brand/logo.png); optional assets/ prefix is normalized away. "
                    + "Use the returned path with {{ asset \"assets/…\" }}. Allowlisted: png/jpg/gif/webp/svg. "
                    + "Max size: Rendering:MaxTemplateAssetBytes."
            )
            .Produces<TemplateAssetResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        endpoints
            .MapDelete(
                "/templates/{templateId}/assets/{*assetPath}",
                async (
                    string templateId,
                    string assetPath,
                    TemplateStore templateStore,
                    CancellationToken cancellationToken
                ) =>
                {
                    try
                    {
                        await templateStore.DeleteAssetAsync(
                            templateId,
                            assetPath,
                            cancellationToken
                        );
                        return Results.NoContent();
                    }
                    catch (ArgumentException ex)
                    {
                        return TemplateError(ex);
                    }
                }
            )
            .WithName(Name("DeleteTemplateAsset"))
            .WithSummary("Delete a template asset")
            .WithDescription(
                "Removes one file under assets/. Same path rules as PUT (label or assets/-prefixed)."
            )
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        endpoints
            .MapPost(
                "/render/template/html",
                async (
                    TemplateHtmlRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTemplateHtmlAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTemplateHtml"))
            .WithSummary("Render a local HTML template to HTML")
            .WithDescription(
                "Loads templateId from TemplatesRoot (template.html + required contract.json), "
                    + "validates data against the contract, and returns the Scriban-rendered HTML for browser preview. "
                    + "String data values are HTML-encoded before template interpolation. "
                    + "The shared print baseline CSS is inlined into the result. "
                    + "No PDF conversion — same markup path as template/pdf before PeachPDF."
            )
            .Accepts<TemplateHtmlRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/template/pdf",
                async (
                    TemplatePdfRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTemplatePdfAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTemplatePdf"))
            .WithSummary("Render a local HTML template to PDF")
            .WithDescription(
                "Loads templateId from TemplatesRoot (template.html + required contract.json), "
                    + "validates data against the contract, renders with Scriban, inlines the print baseline CSS, "
                    + "then converts to PDF. "
                    + "String data values are HTML-encoded before template interpolation."
            )
            .Accepts<TemplatePdfRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/csv/pdf",
                async (
                    CsvPdfRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderCsvPdfAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderCsvPdf"))
            .WithSummary("Render CSV to PDF")
            .WithDescription(
                "Parses RFC 4180 CSV in content into a single sheet, then renders PDF. "
                    + "hasHeader defaults to true; optional name/pageSize/pageOrientation."
            )
            .Accepts<CsvPdfRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/csv/xlsx",
                async (
                    CsvXlsxRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderCsvXlsxAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderCsvXlsx"))
            .WithSummary("Render CSV to Excel")
            .WithDescription(
                "Parses RFC 4180 CSV in content into a single sheet, then renders .xlsx. "
                    + "hasHeader defaults to true; optional sheet name. "
                    + "ISO dates, true/false, and canonical numbers are typed."
            )
            .Accepts<CsvXlsxRenderRequest>("application/json")
            .Produces(
                StatusCodes.Status200OK,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            )
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/html/pdf",
                async (
                    HtmlPdfRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderHtmlPdfAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderHtmlPdf"))
            .WithSummary("Render HTML to PDF")
            .WithDescription(
                "Converts HTML in content to PDF. Optional pageSize/pageOrientation default to A4/Portrait. "
                    + "Remote HTTP resources are not fetched; local file: URIs are blocked."
            )
            .Accepts<HtmlPdfRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/table/pdf",
                async (
                    TableRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTablePdfAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTablePdf"))
            .WithSummary("Render table data to PDF")
            .WithDescription(
                "Converts a constrained tabular workbook to PDF. Optional pageSize/pageOrientation default to A4/Portrait."
            )
            .Accepts<TableRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/table/html",
                async (
                    TableRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTableHtmlAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTableHtml"))
            .WithSummary("Render table data to HTML")
            .WithDescription(
                "Converts a constrained tabular workbook to a standalone HTML document using the shared print baseline stylesheet. "
                    + "Same sheet constraints as table/xlsx. pageSize/pageOrientation are ignored."
            )
            .Accepts<TableRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/table/xlsx",
                async (
                    TableRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTableXlsxAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTableXlsx"))
            .WithSummary("Render table data to Excel")
            .WithDescription(
                "Converts a constrained tabular workbook (headers/rows of scalars) to .xlsx. "
                    + "pageSize/pageOrientation are ignored."
            )
            .Accepts<TableRenderRequest>("application/json")
            .Produces(
                StatusCodes.Status200OK,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            )
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        endpoints
            .MapPost(
                "/render/table/csv",
                async (
                    TableRenderRequest request,
                    DocumentRenderService renderService,
                    EngineIdentity identity,
                    CancellationToken cancellationToken
                ) =>
                    await ExecuteRender(
                        () => renderService.RenderTableCsvAsync(request, cancellationToken),
                        identity
                    )
            )
            .WithName(Name("RenderTableCsv"))
            .WithSummary("Render table data to CSV")
            .WithDescription(
                "Converts a single constrained sheet to UTF-8 CSV (RFC 4180, BOM). Exactly one sheet is required. "
                    + "pageSize/pageOrientation are ignored."
            )
            .Accepts<TableRenderRequest>("application/json")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> ExecuteRender(
        Func<Task<RenderResult>> render,
        EngineIdentity identity
    )
    {
        try
        {
            var result = await render();
            return new EngineMetadataResult(
                Results.File(result.Content, result.ContentType, result.FileName),
                identity
            );
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // DocumentRenderService already logged unexpected failures.
            return Results.Json(
                new ErrorResponse("Document rendering failed."),
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static IResult TemplateError(ArgumentException ex)
    {
        if (ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(new ErrorResponse(ex.Message));
        }

        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
}
