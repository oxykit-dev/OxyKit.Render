using System.Text;

namespace OxyKit.Render.Core.Outputs;

internal sealed class TableToHtmlRenderer
{
    public Task<RenderResult> RenderAsync(
        ValidatedWorkbook workbook,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var html = TableHtmlBuilder.Build(workbook);
        var bytes = Encoding.UTF8.GetBytes(html);
        return Task.FromResult(new RenderResult(bytes, "text/html", "document.html"));
    }
}
