using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxyKit.Render.Contracts;
using OxyKit.Render.Core.Inputs;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Core;

internal sealed class DocumentRenderService(
    HtmlToPdfRenderer htmlToPdfRenderer,
    TableToPdfRenderer tableToPdfRenderer,
    TableToHtmlRenderer tableToHtmlRenderer,
    TableToExcelRenderer tableToExcelRenderer,
    TableToCsvRenderer tableToCsvRenderer,
    TemplateStore templateStore,
    IOptions<RenderingOptions> renderingOptions,
    ILogger<DocumentRenderService> logger
)
{
    private readonly RenderingOptions _options = renderingOptions.Value;
    private readonly SemaphoreSlim _renderGate = new(
        Math.Max(1, renderingOptions.Value.MaxConcurrentRenders),
        Math.Max(1, renderingOptions.Value.MaxConcurrentRenders)
    );

    public IReadOnlyCollection<string> SupportedFormats { get; } =
    [
        "csv/pdf",
        "csv/xlsx",
        "html/pdf",
        "table/csv",
        "table/html",
        "table/pdf",
        "table/xlsx",
        "template/html",
        "template/pdf",
    ];

    public IReadOnlyList<TemplateInfo> ListTemplates() => templateStore.List();

    public Task<RenderResult> RenderHtmlPdfAsync(
        HtmlPdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "html/pdf",
            () =>
            {
                var content = ContentField.Require(request.Content);
                ContentField.EnsureMaxLength(content, _options.MaxHtmlContentChars);
                var pageOptions = PdfPageOptionsResolver.Resolve(
                    request.PageSize,
                    request.PageOrientation
                );
                return htmlToPdfRenderer.RenderAsync(content, pageOptions, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTemplateHtmlAsync(
        TemplateHtmlRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "template/html",
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var html = RenderTemplateToHtml(request.TemplateId, request.Data);
                var bytes = System.Text.Encoding.UTF8.GetBytes(html);
                return Task.FromResult(new RenderResult(bytes, "text/html", "document.html"));
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTemplatePdfAsync(
        TemplatePdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "template/pdf",
            () =>
            {
                var html = RenderTemplateToHtml(request.TemplateId, request.Data);
                var pageOptions = PdfPageOptionsResolver.Resolve(
                    request.PageSize,
                    request.PageOrientation
                );
                return htmlToPdfRenderer.RenderAsync(html, pageOptions, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderCsvPdfAsync(
        CsvPdfRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "csv/pdf",
            () =>
            {
                var workbook = ParseCsvWorkbook(request.Content, request.HasHeader, request.Name);
                var pageOptions = PdfPageOptionsResolver.Resolve(
                    request.PageSize,
                    request.PageOrientation
                );
                return tableToPdfRenderer.RenderAsync(workbook, pageOptions, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderCsvXlsxAsync(
        CsvXlsxRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "csv/xlsx",
            () =>
            {
                var workbook = ParseCsvWorkbook(request.Content, request.HasHeader, request.Name);
                return tableToExcelRenderer.RenderAsync(workbook, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTablePdfAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "table/pdf",
            () =>
            {
                var workbook = TableWorkbookConstraints.Validate(request);
                var pageOptions = PdfPageOptionsResolver.Resolve(
                    request.PageSize,
                    request.PageOrientation
                );
                return tableToPdfRenderer.RenderAsync(workbook, pageOptions, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTableHtmlAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "table/html",
            () =>
            {
                var workbook = TableWorkbookConstraints.Validate(request);
                return tableToHtmlRenderer.RenderAsync(workbook, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTableXlsxAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "table/xlsx",
            () =>
            {
                var workbook = TableWorkbookConstraints.Validate(request);
                return tableToExcelRenderer.RenderAsync(workbook, cancellationToken);
            },
            cancellationToken
        );

    public Task<RenderResult> RenderTableCsvAsync(
        TableRenderRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ExecuteAsync(
            "table/csv",
            () =>
            {
                if (request.Sheets is null || request.Sheets.Count != 1)
                {
                    throw new ArgumentException("CSV export requires exactly one sheet.");
                }

                var workbook = TableWorkbookConstraints.Validate(request);
                return tableToCsvRenderer.RenderAsync(workbook, cancellationToken);
            },
            cancellationToken
        );

    private string RenderTemplateToHtml(string? templateId, JsonElement? data)
    {
        var template = templateStore.Load(templateId);
        var payload = data ?? default;
        TemplateDataValidator.Validate(template.ContractJson, payload, template.Id);

        var html = PrintBaselineInjector.Apply(
            ScribanTemplateRenderer.Render(template.Html, payload)
        );
        ContentField.EnsureMaxLength(html, _options.MaxHtmlContentChars);
        return html;
    }

    private ValidatedWorkbook ParseCsvWorkbook(string? content, bool? hasHeader, string? name)
    {
        var text = ContentField.Require(content);
        ContentField.EnsureMaxLength(text, _options.MaxCsvContentChars);
        return CsvTableParser.Parse(text, name, hasHeader ?? true);
    }

    private async Task<RenderResult> ExecuteAsync(
        string transform,
        Func<Task<RenderResult>> render,
        CancellationToken cancellationToken
    )
    {
        await _renderGate.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await render();
            logger.LogInformation(
                "Rendered {Transform} in {ElapsedMs}ms ({Bytes} bytes)",
                transform,
                stopwatch.ElapsedMilliseconds,
                result.Content.Length
            );
            return result;
        }
        catch (Exception ex)
            when (ex
                    is not OperationCanceledException
                        and not ArgumentException
                        and not NotSupportedException
            )
        {
            logger.LogError(
                ex,
                "Render failed for {Transform} after {ElapsedMs}ms",
                transform,
                stopwatch.ElapsedMilliseconds
            );
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            logger.LogWarning(
                ex,
                "Render rejected for {Transform}: {Message}",
                transform,
                ex.Message
            );
            throw;
        }
        finally
        {
            _renderGate.Release();
        }
    }
}
