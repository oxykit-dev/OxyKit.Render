using PeachPDF;

namespace OxyKit.Render.Core.Outputs;

internal sealed class HtmlToPdfRenderer
{
    public async Task<RenderResult> RenderAsync(
        string content,
        PdfPageOptions pageOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var generator = new PdfGenerator();
        var config = new PdfGenerateConfig
        {
            PageSize = PdfPageOptionsResolver.ToPeachPdf(pageOptions.PageSize),
            PageOrientation = PdfPageOptionsResolver.ToPeachPdf(pageOptions.PageOrientation),
            // Default NetworkLoader only allows data: URIs (no HTTP). Still block file: reads.
            AllowLocalFileAccess = false,
        };
        var document = await generator.GeneratePdf(content, config);

        await using var stream = new MemoryStream();
        document.Save(stream);

        return new RenderResult(stream.ToArray(), "application/pdf", "document.pdf");
    }
}
