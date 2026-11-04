using Scriban.Runtime;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Scriban helpers for trusted templates (markup returned is Core-generated, not user HTML).
/// </summary>
internal static class TemplateQrFunctions
{
    /// <summary>
    /// Registers template builtins (<c>qr</c>, <c>barcode</c>) on <paramref name="target"/>.
    /// </summary>
    public static void Register(ScriptObject target) =>
        target.Import(typeof(TemplateQrFunctions));

    /// <summary>
    /// Inline SVG QR via QRCoder matrix.
    /// Named args: <c>pixels</c>, <c>ecc</c>.
    /// </summary>
#pragma warning disable IDE1006 // Scriban builtin names are lowercase
    public static string qr(object? payload, int? pixels = null, string? ecc = null)
    {
        var text = Coerce(payload);
        var ppm = pixels ?? QrSvgEncoder.DefaultPixelsPerModule;
        return QrSvgEncoder.EncodeToSvg(text, ppm, ecc);
    }

    /// <summary>
    /// Inline SVG barcode via Barcoder (MIT).
    /// Usage: <c>{{ barcode "code128" sku }}</c>, optional <c>pixels:</c> / <c>height:</c>.
    /// Symbologies: <c>code128</c>, <c>code39</c>, <c>ean13</c>.
    /// </summary>
    public static string barcode(
        object? symbology,
        object? payload,
        int? pixels = null,
        int? height = null
    )
    {
        var kind = Coerce(symbology) ?? "";
        var text = Coerce(payload);
        var ppm = pixels ?? BarcodeSvgEncoder.DefaultPixelsPerModule;
        var barHeight = height ?? BarcodeSvgEncoder.DefaultBarHeightPx;
        return BarcodeSvgEncoder.EncodeToSvg(kind, text, ppm, barHeight);
    }
#pragma warning restore IDE1006

    private static string? Coerce(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            _ => value.ToString(),
        };
}
