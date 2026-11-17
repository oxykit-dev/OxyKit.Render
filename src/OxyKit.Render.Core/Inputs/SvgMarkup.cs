using System.Text;

namespace OxyKit.Render.Core.Inputs;

/// <summary>Shared SVG fragment helpers for QR / barcode encoders.</summary>
internal static class SvgMarkup
{
    public static void AppendBlackRect(StringBuilder sb, int x, int y, int width, int height)
    {
        sb.Append("<rect x=\"");
        sb.Append(x);
        sb.Append("\" y=\"");
        sb.Append(y);
        sb.Append("\" width=\"");
        sb.Append(width);
        sb.Append("\" height=\"");
        sb.Append(height);
        sb.Append("\" fill=\"#000\"/>");
    }
}
