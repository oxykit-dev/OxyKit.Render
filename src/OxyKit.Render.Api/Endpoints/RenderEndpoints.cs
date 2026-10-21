using OxyKit.Render.Contracts;
using OxyKit.Render.Core;

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
}
