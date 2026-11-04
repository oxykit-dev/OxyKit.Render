using System.Collections;
using System.Globalization;
using System.Text;
using QRCoder;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Encodes a payload as an inline SVG QR code (QRCoder matrix + custom SVG; no PNG).
/// </summary>
internal static class QrSvgEncoder
{
    public const int DefaultPixelsPerModule = 4;
    public const int MinPixelsPerModule = 2;
    public const int MaxPixelsPerModule = 16;
    public const int MaxPayloadUtf8Bytes = 2048;
    public const int MaxDisplayEdgePx = 512;
    public const string DefaultEcc = "M";

    public static string EncodeToSvg(
        string? payload,
        int pixelsPerModule = DefaultPixelsPerModule,
        string? ecc = DefaultEcc
    )
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("QR payload must be a non-empty string.");

        var utf8Length = Encoding.UTF8.GetByteCount(payload);
        if (utf8Length > MaxPayloadUtf8Bytes)
        {
            throw new ArgumentException(
                $"QR payload exceeds maximum length of {MaxPayloadUtf8Bytes} UTF-8 bytes."
            );
        }

        if (pixelsPerModule < MinPixelsPerModule || pixelsPerModule > MaxPixelsPerModule)
        {
            throw new ArgumentException(
                $"QR pixels must be between {MinPixelsPerModule} and {MaxPixelsPerModule}."
            );
        }

        var level = ParseEcc(ecc);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, level);
        return ToSvg(data.ModuleMatrix, pixelsPerModule);
    }

    private static string ToSvg(List<BitArray> matrix, int pixelsPerModule)
    {
        var modules = matrix.Count;
        var edgePx = Math.Min(modules * pixelsPerModule, MaxDisplayEdgePx);

        var sb = new StringBuilder(256 + modules * modules);
        sb.Append(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{edgePx}\" height=\"{edgePx}\" viewBox=\"0 0 {modules} {modules}\" shape-rendering=\"crispEdges\" role=\"img\">"
        );
        sb.Append(
            CultureInfo.InvariantCulture,
            $"<rect width=\"{modules}\" height=\"{modules}\" fill=\"#fff\"/>"
        );

        for (var y = 0; y < modules; y++)
        {
            var row = matrix[y];
            var runStart = -1;
            for (var x = 0; x <= modules; x++)
            {
                var black = x < modules && row[x];
                if (black && runStart < 0)
                    runStart = x;
                else if (!black && runStart >= 0)
                {
                    var runWidth = x - runStart;
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"<rect x=\"{runStart}\" y=\"{y}\" width=\"{runWidth}\" height=\"1\" fill=\"#000\"/>"
                    );
                    runStart = -1;
                }
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static QRCodeGenerator.ECCLevel ParseEcc(string? ecc)
    {
        var value = string.IsNullOrWhiteSpace(ecc) ? DefaultEcc : ecc.Trim();
        return value.ToUpperInvariant() switch
        {
            "L" => QRCodeGenerator.ECCLevel.L,
            "M" => QRCodeGenerator.ECCLevel.M,
            "Q" => QRCodeGenerator.ECCLevel.Q,
            "H" => QRCodeGenerator.ECCLevel.H,
            _ => throw new ArgumentException(
                "QR ecc must be one of L, M, Q, or H."
            ),
        };
    }
}
