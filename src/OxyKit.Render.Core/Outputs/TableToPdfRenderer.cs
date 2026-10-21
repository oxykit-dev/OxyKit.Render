namespace OxyKit.Render.Core.Outputs;

internal sealed class TableToPdfRenderer(HtmlToPdfRenderer htmlToPdfRenderer)
{
    public Task<RenderResult> RenderAsync(
        ValidatedWorkbook workbook,
        PdfPageOptions pageOptions,
        CancellationToken cancellationToken = default
    )
    {
        var html = TableHtmlBuilder.Build(workbook);
        return htmlToPdfRenderer.RenderAsync(html, pageOptions, cancellationToken);
    }
}
