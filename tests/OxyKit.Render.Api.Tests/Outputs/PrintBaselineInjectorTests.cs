using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Api.Tests.Outputs;

public sealed class PrintBaselineInjectorTests
{
    [Fact]
    public void Apply_InsertsBaselineAtStartOfHead()
    {
        var html = PrintBaselineInjector.Apply(
            """
            <!DOCTYPE html><html><head>
            <title>Doc</title>
            <style>body.pf-document h1{font-size:18pt}</style>
            </head><body class="pf-document"><h1>Hi</h1></body></html>
            """
        );

        var baselineAt = html.IndexOf(PrintBaselineCss.Content, StringComparison.Ordinal);
        var overrideAt = html.IndexOf("font-size:18pt", StringComparison.Ordinal);
        Assert.True(baselineAt >= 0);
        Assert.True(overrideAt > baselineAt);
        Assert.Equal(1, CountOccurrences(html, PrintBaselineCss.Content));
    }

    [Fact]
    public void Apply_MatchesHeadCaseInsensitively()
    {
        var html = PrintBaselineInjector.Apply(
            """<HTML><HEAD meta="x"><title>t</title></HEAD><BODY class="pf-document">x</BODY></HTML>"""
        );

        Assert.Contains("<HEAD meta=\"x\">\n<style>\n", html, StringComparison.Ordinal);
        Assert.Contains(PrintBaselineCss.Content, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_PrependsStyleWhenHeadMissing()
    {
        var html = PrintBaselineInjector.Apply("""<body class="pf-document"><p>x</p></body>""");

        Assert.StartsWith("<style>", html, StringComparison.Ordinal);
        Assert.Contains(PrintBaselineCss.Content.Split('\n', 2)[0], html);
        Assert.Contains("<body class=\"pf-document\">", html);
    }

    [Fact]
    public void Apply_PrependsStyleForBareFragment()
    {
        var html = PrintBaselineInjector.Apply("<p>hello</p>");

        Assert.Equal("<style>\n" + PrintBaselineCss.Content + "\n</style>\n<p>hello</p>", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Apply_RejectsNullOrEmpty(string? html)
    {
        Assert.ThrowsAny<ArgumentException>(() => PrintBaselineInjector.Apply(html!));
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
