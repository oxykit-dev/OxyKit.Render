using System.Globalization;

namespace OxyKit.Render.Core.Outputs;

/// <summary>Invariant text for table cells when exporting to CSV/HTML.</summary>
internal static class TableCellText
{
    public static string Format(object? value) =>
        value switch
        {
            null => string.Empty,
            bool flag => flag ? "true" : "false",
            DateOnly dateOnly => IsoDateCell.Format(dateOnly),
            DateTime dateTime => IsoDateCell.Format(dateTime),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)
                ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
}
