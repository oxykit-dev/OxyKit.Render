using System.Text.Json;
using OxyKit.Render.Contracts;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TableWorkbookConstraintsTests
{
    [Fact]
    public void Validate_AcceptsRectangularSheet()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                {
                  "name": "Orders",
                  "headers": ["Id", "Customer", "Total"],
                  "rows": [[1, "Acme", 120.5], [2, "Beta", 80]]
                }
              ]
            }
            """
        );

        var workbook = TableWorkbookConstraints.Validate(request);

        Assert.Single(workbook.Sheets);
        Assert.Equal("Orders", workbook.Sheets[0].Name);
        Assert.Equal(3, workbook.Sheets[0].Headers.Count);
        Assert.Equal(2, workbook.Sheets[0].Rows.Count);
    }

    [Fact]
    public void Validate_RejectsEmptySheets()
    {
        var request = new TableRenderRequest { Sheets = [] };

        var ex = Assert.Throws<ArgumentException>(() => TableWorkbookConstraints.Validate(request));
        Assert.Contains("sheets", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsDuplicateSheetNames()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                { "name": "A", "headers": ["Id"], "rows": [[1]] },
                { "name": "a", "headers": ["Id"], "rows": [[2]] }
              ]
            }
            """
        );

        var ex = Assert.Throws<ArgumentException>(() => TableWorkbookConstraints.Validate(request));
        Assert.Contains("Duplicate sheet name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsRaggedRows()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                {
                  "name": "Orders",
                  "headers": ["Id", "Customer"],
                  "rows": [[1, "Acme"], [2]]
                }
              ]
            }
            """
        );

        var ex = Assert.Throws<ArgumentException>(() => TableWorkbookConstraints.Validate(request));
        Assert.Contains("expected 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsObjectCells()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                {
                  "name": "Orders",
                  "headers": ["Meta"],
                  "rows": [[{ "nested": true }]]
                }
              ]
            }
            """
        );

        var ex = Assert.Throws<ArgumentException>(() => TableWorkbookConstraints.Validate(request));
        Assert.Contains(
            "string, number, boolean, or null",
            ex.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Validate_ParsesIsoDateAndDateTimeStrings()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                {
                  "name": "Orders",
                  "headers": ["Day", "At", "Utc", "Offset", "Label"],
                  "rows": [[
                    "2024-01-15",
                    "2024-01-15T14:30:00",
                    "2024-01-15T14:30:00Z",
                    "2024-01-15T14:30:00+08:00",
                    "not-a-date"
                  ]]
                }
              ]
            }
            """
        );

        var workbook = TableWorkbookConstraints.Validate(request);
        var row = workbook.Sheets[0].Rows[0];

        Assert.Equal(new DateOnly(2024, 1, 15), Assert.IsType<DateOnly>(row[0]));
        Assert.Equal(
            new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Unspecified),
            Assert.IsType<DateTime>(row[1])
        );
        Assert.Equal(
            new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            Assert.IsType<DateTime>(row[2])
        );
        Assert.Equal(
            new DateTime(2024, 1, 15, 6, 30, 0, DateTimeKind.Utc),
            Assert.IsType<DateTime>(row[3])
        );
        Assert.Equal("not-a-date", Assert.IsType<string>(row[4]));
    }

    [Fact]
    public void Validate_LeavesAmbiguousDateLikeStringsAsText()
    {
        var request = CreateRequest(
            """
            {
              "sheets": [
                {
                  "name": "Orders",
                  "headers": ["A", "B"],
                  "rows": [["01/15/2024", "2024/01/15"]]
                }
              ]
            }
            """
        );

        var workbook = TableWorkbookConstraints.Validate(request);
        var row = workbook.Sheets[0].Rows[0];

        Assert.Equal("01/15/2024", Assert.IsType<string>(row[0]));
        Assert.Equal("2024/01/15", Assert.IsType<string>(row[1]));
    }

    private static TableRenderRequest CreateRequest(string json) =>
        JsonSerializer.Deserialize<TableRenderRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;
}
