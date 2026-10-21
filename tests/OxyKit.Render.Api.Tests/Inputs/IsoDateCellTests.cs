using OxyKit.Render.Core;

namespace OxyKit.Render.Api.Tests.Inputs;

public sealed class IsoDateCellTests
{
    [Fact]
    public void Format_PreservesFractionalSeconds()
    {
        var value = new DateTime(2024, 1, 15, 14, 30, 0, 123, DateTimeKind.Utc).AddTicks(4560);
        var formatted = IsoDateCell.Format(value);

        Assert.StartsWith("2024-01-15T14:30:00.123", formatted, StringComparison.Ordinal);
        Assert.EndsWith("Z", formatted, StringComparison.Ordinal);
    }
}
