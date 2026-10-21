using System.Text.Json;
using OxyKit.Render.Contracts;
using OxyKit.Render.Core.Inputs;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Api.Tests.Outputs;

public sealed class TableHtmlBuilderTests
{
    [Fact]
    public void Build_EmbedsPrintBaselineAndEscapesCells()
    {
        var workbook = TableWorkbookConstraints.Validate(
            new TableRenderRequest
            {
                Sheets =
                [
                    new TableSheet
                    {
                        Name = "Q1",
                        Headers = ["Name", "Note"],
                        Rows =
                        [
                            [
                                JsonSerializer.SerializeToElement("Acme"),
                                JsonSerializer.SerializeToElement("<script>"),
                            ],
                        ],
                    },
                ],
            }
        );

        var html = TableHtmlBuilder.Build(workbook);

        Assert.Contains(PrintBaselineCss.Content.Split('\n', 2)[0], html);
        Assert.Contains("class=\"pf-document\"", html);
        Assert.Contains("<section>", html);
        Assert.Contains("<h1>Q1</h1>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("pf-sheet", html);
        Assert.DoesNotContain("pf-table", html);
    }

    [Fact]
    public void Build_EmitsSemanticMarkupPerSheet()
    {
        var workbook = TableWorkbookConstraints.Validate(
            new TableRenderRequest
            {
                Sheets =
                [
                    new TableSheet
                    {
                        Name = "Alpha",
                        Headers = ["A"],
                        Rows =
                        [
                            [JsonSerializer.SerializeToElement(1)],
                        ],
                    },
                    new TableSheet
                    {
                        Name = "Beta",
                        Headers = ["B"],
                        Rows =
                        [
                            [JsonSerializer.SerializeToElement(2)],
                        ],
                    },
                ],
            }
        );

        var html = TableHtmlBuilder.Build(workbook);

        Assert.Equal(2, CountOccurrences(html, "<section>"));
        Assert.Contains("<h1>Alpha</h1>", html);
        Assert.Contains("<h1>Beta</h1>", html);
        Assert.Equal(2, CountOccurrences(html, "<table>"));
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<th>B</th>", html);
        Assert.Contains("<td>1</td>", html);
        Assert.Contains("<td>2</td>", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (
            var index = 0;
            (index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0;
            index += needle.Length
        )
        {
            count++;
        }

        return count;
    }
}
