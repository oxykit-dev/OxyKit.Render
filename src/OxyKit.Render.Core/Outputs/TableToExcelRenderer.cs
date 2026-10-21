using ClosedXML.Excel;

namespace OxyKit.Render.Core.Outputs;

internal sealed class TableToExcelRenderer
{
    /// <summary>Skip expensive auto-fit above this data-row count.</summary>
    internal const int MaxAutoFitRows = 500;

    public Task<RenderResult> RenderAsync(
        ValidatedWorkbook workbook,
        CancellationToken cancellationToken = default
    )
    {
        using var xlWorkbook = new XLWorkbook();
        foreach (var sheet in workbook.Sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSheet(xlWorkbook, sheet);
        }

        using var stream = new MemoryStream();
        xlWorkbook.SaveAs(stream);

        return Task.FromResult(
            new RenderResult(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "document.xlsx"
            )
        );
    }

    private static void WriteSheet(XLWorkbook workbook, ValidatedSheet sheet)
    {
        var worksheet = workbook.Worksheets.Add(sheet.Name);
        var startRow = 1;

        if (sheet.Headers.Count > 0)
        {
            for (var col = 0; col < sheet.Headers.Count; col++)
            {
                var cell = worksheet.Cell(1, col + 1);
                cell.Value = sheet.Headers[col];
                cell.Style.Font.Bold = true;
            }

            startRow = 2;
        }

        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            for (var col = 0; col < row.Count; col++)
            {
                var cell = worksheet.Cell(startRow + rowIndex, col + 1);
                SetCellValue(cell, row[col]);
            }
        }

        if (sheet.Headers.Count > 0 || sheet.Rows.Count > 0)
        {
            // Full auto-fit is expensive on large sheets; keep it for typical exports.
            if (sheet.Rows.Count <= MaxAutoFitRows)
            {
                worksheet.Columns().AdjustToContents();
            }
        }
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear();
                break;
            case string text:
                cell.Value = text;
                break;
            case DateOnly dateOnly:
                // Built-in format 14 = Excel "Short Date" (follows the viewer's locale).
                cell.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.SetNumberFormatId(14);
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                // Built-in format 22 = Excel short date + time (locale-aware in Excel).
                cell.Style.NumberFormat.SetNumberFormatId(22);
                break;
            case long number:
                cell.Value = number;
                break;
            case double number:
                cell.Value = number;
                break;
            case bool flag:
                cell.Value = flag;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
