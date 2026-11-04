using System.Text.Json;
using OxyKit.Render.Core.Inputs;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class TemplateFormatFunctionsTests
{
    [Fact]
    public void Money_DefaultCultureUsdTwoDecimals()
    {
        Assert.Equal("$1,234.50", TemplateFormatFunctions.money(1234.5m, currency: "USD"));
    }

    [Fact]
    public void Money_RespectsDecimalsAndCulture()
    {
        var text = TemplateFormatFunctions.money(
            12.3456m,
            currency: "EUR",
            culture: "de-DE",
            decimals: 3
        );
        Assert.Contains("12,346", text); // AwayFromZero
        Assert.Contains("€", text);
    }

    [Fact]
    public void Number_ExplicitDecimals()
    {
        Assert.Equal("1,234.5679", TemplateFormatFunctions.number(1234.56789m, decimals: 4));
    }

    [Fact]
    public void Percent_ScalesFraction()
    {
        Assert.Equal("15.5%", TemplateFormatFunctions.percent(0.155m, decimals: 1));
    }

    [Fact]
    public void Date_IsoAndShort()
    {
        Assert.Equal("2026-11-04", TemplateFormatFunctions.date("2026-11-04", format: "iso"));
        Assert.Equal(
            "11/4/2026",
            TemplateFormatFunctions.date("2026-11-04", format: "short", culture: "en-US")
        );
    }

    [Fact]
    public void Datetime_Iso()
    {
        Assert.Equal(
            "2026-11-04T15:30:00",
            TemplateFormatFunctions.datetime("2026-11-04T15:30:00", format: "iso")
        );
    }

    [Fact]
    public void Money_InvalidCurrency_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TemplateFormatFunctions.money(1m, currency: "US")
        );
        Assert.Contains("ISO 4217", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Number_InvalidDecimals_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TemplateFormatFunctions.number(1m, decimals: 99)
        );
        Assert.Contains("decimals", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_MoneyAndDateHelpers()
    {
        var html = ScribanTemplateRenderer.Render(
            "<p>{{ money total currency: currency }}</p><p>{{ date issueDate format: \"iso\" }}</p>",
            JsonDocument
                .Parse("""{"total":200.5,"currency":"USD","issueDate":"2026-11-04"}""")
                .RootElement
        );

        Assert.Contains("$200.50", html);
        Assert.Contains("2026-11-04", html);
    }
}
