using System.Globalization;

namespace OxyKit.Render.Core;

/// <summary>
/// Parses strict ISO-8601 date / date-time strings into <see cref="DateOnly"/> or <see cref="DateTime"/>.
/// Non-ISO strings are left as text so accidental coercion is avoided.
/// Shared by table/CSV input normalization and HTML/CSV text emit.
/// </summary>
internal static class IsoDateCell
{
    private static readonly string[] DateTimeFormatsNoOffset =
    [
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
    ];

    private static readonly string[] DateTimeFormatsWithOffset =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    public static bool TryParse(string text, out object value)
    {
        if (
            text.Length == 10
            && DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly
            )
        )
        {
            value = dateOnly;
            return true;
        }

        if (!text.Contains('T', StringComparison.Ordinal))
        {
            value = null!;
            return false;
        }

        if (
            DateTime.TryParseExact(
                text,
                DateTimeFormatsNoOffset,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime
            )
        )
        {
            value = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return true;
        }

        if (
            DateTimeOffset.TryParseExact(
                text,
                DateTimeFormatsWithOffset,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTimeOffset
            )
        )
        {
            value = dateTimeOffset.UtcDateTime;
            return true;
        }

        value = null!;
        return false;
    }

    public static string Format(DateOnly value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Format(DateTime value)
    {
        var core = value
            .ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.');

        return value.Kind == DateTimeKind.Utc ? core + "Z" : core;
    }
}
