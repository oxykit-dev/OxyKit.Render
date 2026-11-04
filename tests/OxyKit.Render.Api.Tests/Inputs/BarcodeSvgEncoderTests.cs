using System.Text.RegularExpressions;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class BarcodeSvgEncoderTests
{
    [Fact]
    public void EncodeToSvg_Code128_ReturnsInlineSvg()
    {
        var svg = BarcodeSvgEncoder.EncodeToSvg("code128", "ABC-123");
        Assert.StartsWith("<svg ", svg);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.Contains("fill=\"#000\"", svg);
        Assert.Contains("</svg>", svg);
        Assert.Contains("width=\"100%\"", svg);
    }

    [Fact]
    public void EncodeToSvg_Code128_ViewBoxUsesBarHeightNotOneModule()
    {
        var svg = BarcodeSvgEncoder.EncodeToSvg("code128", "INV-1", pixelsPerModule: 2, barHeightPx: 64);
        Assert.Contains("width=\"100%\"", svg);

        var vb = Regex.Match(svg, @"viewBox=""0 0 (\d+) (\d+)""");
        Assert.True(vb.Success);
        Assert.Equal(64, int.Parse(vb.Groups[2].Value));
        Assert.Contains("height=\"64\" fill=\"#000\"", svg);
        Assert.Contains("preserveAspectRatio=\"xMinYMid meet\"", svg);
    }

    [Fact]
    public void EncodeToSvg_Ean13_AcceptsValidDigits()
    {
        var svg = BarcodeSvgEncoder.EncodeToSvg("ean13", "5901234123457");
        Assert.Contains("<svg ", svg);
    }

    [Fact]
    public void EncodeToSvg_UnknownSymbology_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            BarcodeSvgEncoder.EncodeToSvg("datamatrix", "x")
        );
        Assert.Contains("symbology", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodeToSvg_EmptyPayload_Throws()
    {
        Assert.Throws<ArgumentException>(() => BarcodeSvgEncoder.EncodeToSvg("code128", " "));
    }
}
