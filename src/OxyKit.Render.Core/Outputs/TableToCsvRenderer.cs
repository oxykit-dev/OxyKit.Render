using System.Text;

namespace OxyKit.Render.Core.Outputs;

internal sealed class TableToCsvRenderer
{
    public Task<RenderResult> RenderAsync(
        ValidatedWorkbook workbook,
        CancellationToken cancellationToken = default
    )
    {
        if (workbook.Sheets.Count != 1)
        {
            throw new ArgumentException("CSV export requires exactly one sheet.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var sheet = workbook.Sheets[0];
        var csv = new StringBuilder();

        if (sheet.Headers.Count > 0)
        {
            AppendRow(csv, sheet.Headers.Select(static h => h));
        }

        foreach (var row in sheet.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendRow(csv, row.Select(TableCellText.Format));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return Task.FromResult(new RenderResult(bytes, "text/csv", "document.csv"));
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> fields)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first)
            {
                csv.Append(',');
            }

            first = false;
            csv.Append(EscapeField(field));
        }

        csv.Append("\r\n");
    }

    private static string EscapeField(string value)
    {
        var needsQuotes =
            value.Contains('"')
            || value.Contains(',')
            || value.Contains('\r')
            || value.Contains('\n');

        if (!needsQuotes)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
