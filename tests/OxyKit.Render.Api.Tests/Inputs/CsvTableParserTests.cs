using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class CsvTableParserTests
{
    [Fact]
    public void Parse_ReadsHeadersQuotesAndTypedCells()
    {
        var workbook = CsvTableParser.Parse(
            "Id,Customer,Total,Paid,Day\n1,\"Acme, Inc\",120.5,true,2024-01-15\n2,Beta,,false,not-a-date\n",
            "Orders",
            hasHeader: true
        );

        var item = Assert.Single(workbook.Sheets);
        var sheet = item;
        Assert.Equal("Orders", sheet.Name);
        Assert.Equal(["Id", "Customer", "Total", "Paid", "Day"], sheet.Headers);
        Assert.Equal(2, sheet.Rows.Count);

        Assert.Equal(1L, sheet.Rows[0][0]);
        Assert.Equal("Acme, Inc", sheet.Rows[0][1]);
        Assert.Equal(120.5, Assert.IsType<double>(sheet.Rows[0][2]));
        Assert.Equal(true, sheet.Rows[0][3]);
        Assert.Equal(new DateOnly(2024, 1, 15), Assert.IsType<DateOnly>(sheet.Rows[0][4]));

        Assert.Null(sheet.Rows[1][2]);
        Assert.Equal(false, sheet.Rows[1][3]);
        Assert.Equal("not-a-date", Assert.IsType<string>(sheet.Rows[1][4]));
    }

    [Fact]
    public void Parse_WithoutHeader_TreatsAllRowsAsData()
    {
        var workbook = CsvTableParser.Parse("1,Acme\n2,Beta", sheetName: null, hasHeader: false);

        Assert.Empty(workbook.Sheets[0].Headers);
        Assert.Equal(2, workbook.Sheets[0].Rows.Count);
        Assert.Equal("Sheet1", workbook.Sheets[0].Name);
    }

    [Fact]
    public void Parse_RejectsRaggedRows()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CsvTableParser.Parse("Id,Customer\n1,Acme\n2", "Orders", hasHeader: true)
        );
        Assert.Contains("expected 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsUnclosedQuote()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CsvTableParser.Parse("Id,Note\n1,\"oops", "Orders", hasHeader: true)
        );
        Assert.Contains("unclosed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_StripsBom()
    {
        var workbook = CsvTableParser.Parse("\uFEFFId\n1", "Orders", hasHeader: true);
        Assert.Equal("Id", workbook.Sheets[0].Headers[0]);
        Assert.Equal(1L, workbook.Sheets[0].Rows[0][0]);
    }

    [Fact]
    public void Parse_KeepsLeadingZeroIdsAndNonFiniteAsText()
    {
        var workbook = CsvTableParser.Parse(
            "Code,Weird\n007,NaN\n1e2,Infinity",
            "Orders",
            hasHeader: true
        );

        Assert.Equal("007", Assert.IsType<string>(workbook.Sheets[0].Rows[0][0]));
        Assert.Equal("NaN", Assert.IsType<string>(workbook.Sheets[0].Rows[0][1]));
        Assert.Equal(100d, Assert.IsType<double>(workbook.Sheets[0].Rows[1][0]));
        Assert.Equal("Infinity", Assert.IsType<string>(workbook.Sheets[0].Rows[1][1]));
    }

    [Fact]
    public void Parse_RejectsGarbageAfterClosingQuote()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CsvTableParser.Parse("Id,Note\n1,\"x\"y", "Orders", hasHeader: true)
        );
        Assert.Contains("unexpected characters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_DropsTrailingAllEmptyRows()
    {
        var workbook = CsvTableParser.Parse("Id,Name\n1,Acme\n,,\n", "Orders", hasHeader: true);

        Assert.Single(workbook.Sheets[0].Rows);
        Assert.Equal(1L, workbook.Sheets[0].Rows[0][0]);
    }
}
