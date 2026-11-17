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
        var csv = new StringBuilder(EstimateCapacity(sheet));

        if (sheet.Headers.Count > 0)
        {
            for (var i = 0; i < sheet.Headers.Count; i++)
            {
                if (i > 0)
                    csv.Append(',');
                AppendEscaped(csv, sheet.Headers[i]);
            }

            csv.Append("\r\n");
        }

        foreach (var row in sheet.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < row.Count; i++)
            {
                if (i > 0)
                    csv.Append(',');
                AppendEscaped(csv, TableCellText.Format(row[i]));
            }

            csv.Append("\r\n");
        }

        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes);
        body.CopyTo(bytes.AsSpan(preamble.Length));

        return Task.FromResult(new RenderResult(bytes, "text/csv", "document.csv"));
    }

    private static int EstimateCapacity(ValidatedSheet sheet)
    {
        var cells = sheet.Headers.Count;
        foreach (var row in sheet.Rows)
            cells += row.Count;
        return Math.Max(64, cells * 12);
    }

    private static void AppendEscaped(StringBuilder csv, string value)
    {
        if (
            !value.Contains('"')
            && !value.Contains(',')
            && !value.Contains('\r')
            && !value.Contains('\n')
        )
        {
            csv.Append(value);
            return;
        }

        csv.Append('"');
        foreach (var ch in value)
        {
            if (ch == '"')
                csv.Append('"');
            csv.Append(ch);
        }

        csv.Append('"');
    }
}
