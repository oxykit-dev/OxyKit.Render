using System.Net;
using System.Text;

namespace OxyKit.Render.Core.Outputs;

internal static class TableHtmlBuilder
{
    // Fixed chrome around sheet content (doctype/head/style close/body open/close).
    private const int FixedChromeChars = 512;

    public static string Build(ValidatedWorkbook workbook)
    {
        var html = new StringBuilder(EstimateCapacity(workbook));
        html.Append("<!DOCTYPE html><html lang=\"en\"><head>");
        html.Append("<meta charset=\"utf-8\"/>");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        html.Append("<title>Document</title>");
        html.Append("<style>\n").Append(PrintBaselineCss.Content).Append("\n</style>");
        html.Append("</head><body class=\"pf-document\">");

        foreach (var sheet in workbook.Sheets)
        {
            html.Append("<section>");
            html.Append("<h1>").Append(HtmlEncode(sheet.Name)).Append("</h1>");
            html.Append("<table>");

            if (sheet.Headers.Count > 0)
            {
                html.Append("<thead><tr>");
                foreach (var header in sheet.Headers)
                {
                    html.Append("<th>").Append(HtmlEncode(header)).Append("</th>");
                }

                html.Append("</tr></thead>");
            }

            html.Append("<tbody>");
            foreach (var row in sheet.Rows)
            {
                html.Append("<tr>");
                foreach (var cell in row)
                {
                    html.Append("<td>")
                        .Append(HtmlEncode(TableCellText.Format(cell)))
                        .Append("</td>");
                }

                html.Append("</tr>");
            }

            html.Append("</tbody></table></section>");
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static int EstimateCapacity(ValidatedWorkbook workbook)
    {
        var cells = 0;
        foreach (var sheet in workbook.Sheets)
        {
            cells += sheet.Headers.Count;
            foreach (var row in sheet.Rows)
            {
                cells += row.Count;
            }
        }

        // ~24 chars of tags per cell + modest text payload; plus print CSS once.
        return FixedChromeChars + PrintBaselineCss.Content.Length + cells * 32;
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}
