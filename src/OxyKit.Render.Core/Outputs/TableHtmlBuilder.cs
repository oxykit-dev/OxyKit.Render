using System.Net;
using System.Text;

namespace OxyKit.Render.Core.Outputs;

internal static class TableHtmlBuilder
{
    public static string Build(ValidatedWorkbook workbook)
    {
        var html = new StringBuilder();
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

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}
