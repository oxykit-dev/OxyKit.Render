using System.Text.Json;
using OxyKit.Render.Contracts;
using OxyKit.Render.Core.Inputs;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Api.Tests;

public sealed class TableToExcelRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesIsoDatesAsExcelDateValues()
    {
        var workbook = TableWorkbookConstraints.Validate(
            JsonSerializer.Deserialize<TableRenderRequest>(
                """
                {
                  "sheets": [
                    {
                      "name": "Orders",
                      "headers": ["Day", "At"],
                      "rows": [["2024-01-15", "2024-01-15T14:30:00Z"]]
                    }
                  ]
                }
                """,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!
        );

        var renderer = new TableToExcelRenderer();
        var result = await renderer.RenderAsync(workbook, TestContext.Current.CancellationToken);

        using var stream = new MemoryStream(result.Content);
        using var xl = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = xl.Worksheet("Orders");

        Assert.Equal(ClosedXML.Excel.XLDataType.DateTime, sheet.Cell(2, 1).DataType);
        Assert.Equal(new DateTime(2024, 1, 15), sheet.Cell(2, 1).GetDateTime());
        Assert.Equal(14, sheet.Cell(2, 1).Style.NumberFormat.NumberFormatId);

        Assert.Equal(ClosedXML.Excel.XLDataType.DateTime, sheet.Cell(2, 2).DataType);
        Assert.Equal(new DateTime(2024, 1, 15, 14, 30, 0), sheet.Cell(2, 2).GetDateTime());
        Assert.Equal(22, sheet.Cell(2, 2).Style.NumberFormat.NumberFormatId);
    }
}
