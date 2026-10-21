using OxyKit.Render.Contracts;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Api.Tests.Outputs;

public sealed class PdfPageOptionsResolverTests
{
    [Fact]
    public void Resolve_DefaultsToA4Portrait()
    {
        var options = PdfPageOptionsResolver.Resolve(null, null);

        Assert.Equal(PdfPageSize.A4, options.PageSize);
        Assert.Equal(PdfPageOrientation.Portrait, options.PageOrientation);
    }

    [Fact]
    public void ToPeachPdf_MapsLetterAndLandscape()
    {
        Assert.Equal(
            PeachPDF.PageSize.Letter,
            PdfPageOptionsResolver.ToPeachPdf(PdfPageSize.Letter)
        );
        Assert.Equal(
            PeachPDF.PageOrientation.Landscape,
            PdfPageOptionsResolver.ToPeachPdf(PdfPageOrientation.Landscape)
        );
    }
}
