using System.Globalization;
using System.Text;
using Barcoder;
using Barcoder.Code128;
using Barcoder.Code39;
using Barcoder.Ean;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Encodes 1D barcodes with Barcoder and emits inline SVG (no ImageSharp / SvgLib).
/// </summary>
internal static class BarcodeSvgEncoder
{
    public const int DefaultPixelsPerModule = 2;
    public const int MinPixelsPerModule = 1;
    public const int MaxPixelsPerModule = 8;
    public const int DefaultBarHeightPx = 48;
    public const int MinBarHeightPx = 16;
    public const int MaxBarHeightPx = 256;
    public const int MaxPayloadChars = 256;
    public const int MaxSvgWidthPx = 1024;

    public static string EncodeToSvg(
        string? symbology,
        string? payload,
        int pixelsPerModule = DefaultPixelsPerModule,
        int barHeightPx = DefaultBarHeightPx
    )
    {
        if (string.IsNullOrWhiteSpace(symbology))
            throw new ArgumentException("Barcode symbology is required (e.g. code128).");
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Barcode payload must be a non-empty string.");
        if (payload.Length > MaxPayloadChars)
        {
            throw new ArgumentException(
                $"Barcode payload exceeds maximum length of {MaxPayloadChars} characters."
            );
        }

        if (pixelsPerModule < MinPixelsPerModule || pixelsPerModule > MaxPixelsPerModule)
        {
            throw new ArgumentException(
                $"Barcode pixels must be between {MinPixelsPerModule} and {MaxPixelsPerModule}."
            );
        }

        if (barHeightPx < MinBarHeightPx || barHeightPx > MaxBarHeightPx)
        {
            throw new ArgumentException(
                $"Barcode height must be between {MinBarHeightPx} and {MaxBarHeightPx} pixels."
            );
        }

        IBarcode barcode;
        try
        {
            barcode = EncodeSymbology(symbology.Trim(), payload);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Barcode encode failed: {Sanitize(ex.Message)}");
        }

        return ToSvg(barcode, pixelsPerModule, barHeightPx);
    }

    private static IBarcode EncodeSymbology(string symbology, string payload) =>
        symbology.ToLowerInvariant() switch
        {
            "code128" or "c128" => Code128Encoder.Encode(payload),
            "code39" or "c39" => Code39Encoder.Encode(payload, includeChecksum: false, fullAsciiMode: false),
            "ean13" => EanEncoder.Encode(payload),
            _ => throw new ArgumentException(
                "Barcode symbology must be one of code128, code39, or ean13."
            ),
        };

    private static string ToSvg(IBarcode barcode, int pixelsPerModule, int barHeightPx)
    {
        var moduleWidth = barcode.Bounds.X;
        var moduleHeight = Math.Max(barcode.Bounds.Y, 1);
        // 1D barcodes are 1 module tall; stretch viewBox Y to barHeightPx so CSS
        // height:auto / PeachPDF keep a scannable aspect (not moduleWidth:1).
        // Use width="100%" (not module×pixels) so the bars fill the content box;
        // pixelsPerModule only sizes multi-row matrices.
        var is1D = moduleHeight == 1;
        var heightPx = is1D
            ? barHeightPx
            : Math.Min(moduleHeight * pixelsPerModule, MaxBarHeightPx);
        var widthPx = Math.Min(moduleWidth * pixelsPerModule, MaxSvgWidthPx);
        var viewHeight = is1D ? barHeightPx : moduleHeight;
        var barDrawHeight = is1D ? barHeightPx : 1;
        var sizeAttrs = is1D
            ? $"width=\"100%\" viewBox=\"0 0 {moduleWidth} {viewHeight}\" preserveAspectRatio=\"xMinYMid meet\""
            : $"width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {moduleWidth} {viewHeight}\"";

        var sb = new StringBuilder(256 + moduleWidth * 24);
        sb.Append(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" {sizeAttrs} shape-rendering=\"crispEdges\" role=\"img\">"
        );
        sb.Append(
            CultureInfo.InvariantCulture,
            $"<rect width=\"{moduleWidth}\" height=\"{viewHeight}\" fill=\"#fff\"/>"
        );

        for (var y = 0; y < moduleHeight; y++)
        {
            var runStart = -1;
            for (var x = 0; x <= moduleWidth; x++)
            {
                var black = x < moduleWidth && barcode.At(x, y);
                if (black && runStart < 0)
                    runStart = x;
                else if (!black && runStart >= 0)
                {
                    var runWidth = x - runStart;
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"<rect x=\"{runStart}\" y=\"{y}\" width=\"{runWidth}\" height=\"{barDrawHeight}\" fill=\"#000\"/>"
                    );
                    runStart = -1;
                }
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Sanitize(string message) =>
        message.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
