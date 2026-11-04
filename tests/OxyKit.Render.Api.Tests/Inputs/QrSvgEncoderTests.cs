using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class QrSvgEncoderTests
{
    [Fact]
    public void EncodeToSvg_ReturnsInlineSvg()
    {
        var svg = QrSvgEncoder.EncodeToSvg("INV-100");
        Assert.StartsWith("<svg ", svg);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.Contains("fill=\"#000\"", svg);
        Assert.Contains("viewBox=\"0 0 ", svg);
        Assert.Contains("</svg>", svg);
        Assert.DoesNotContain("data:image/png", svg);
    }

    [Fact]
    public void EncodeToSvg_EmptyPayload_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => QrSvgEncoder.EncodeToSvg("  "));
        Assert.Contains("non-empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodeToSvg_OversizePayload_Throws()
    {
        var payload = new string('A', QrSvgEncoder.MaxPayloadUtf8Bytes + 1);
        var ex = Assert.Throws<ArgumentException>(() => QrSvgEncoder.EncodeToSvg(payload));
        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodeToSvg_InvalidEcc_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => QrSvgEncoder.EncodeToSvg("x", ecc: "Z"));
        Assert.Contains("ecc", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodeToSvg_InvalidPixels_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            QrSvgEncoder.EncodeToSvg("x", pixelsPerModule: 1)
        );
        Assert.Contains("pixels", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
