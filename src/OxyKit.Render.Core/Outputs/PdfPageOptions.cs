using OxyKit.Render.Contracts;

namespace OxyKit.Render.Core.Outputs;

internal sealed record PdfPageOptions(PdfPageSize PageSize, PdfPageOrientation PageOrientation)
{
    public static PdfPageOptions Default { get; } =
        new(PdfPageSize.A4, PdfPageOrientation.Portrait);
}

internal static class PdfPageOptionsResolver
{
    public static PdfPageOptions Resolve(
        PdfPageSize? pageSize,
        PdfPageOrientation? pageOrientation
    ) =>
        new(
            pageSize ?? PdfPageOptions.Default.PageSize,
            pageOrientation ?? PdfPageOptions.Default.PageOrientation
        );

    public static PeachPDF.PageSize ToPeachPdf(PdfPageSize pageSize) =>
        Enum.Parse<PeachPDF.PageSize>(pageSize.ToString());

    public static PeachPDF.PageOrientation ToPeachPdf(PdfPageOrientation pageOrientation) =>
        pageOrientation switch
        {
            PdfPageOrientation.Portrait => PeachPDF.PageOrientation.Portrait,
            PdfPageOrientation.Landscape => PeachPDF.PageOrientation.Landscape,
            _ => throw new NotSupportedException(
                $"Unsupported pageOrientation '{pageOrientation}'."
            ),
        };
}
