using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Scriban.Runtime;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Scriban formatting builtins for trusted templates (plain text; HTML-escaped by callers via data import).
/// Culture defaults to <c>en-US</c> so PDF output does not follow the host OS locale.
/// </summary>
internal static partial class TemplateFormatFunctions
{
    public const string DefaultCultureName = "en-US";
    public const int DefaultMoneyDecimals = 2;
    public const int MinDecimals = 0;
    public const int MaxDecimals = 18;
    public const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    private static readonly ConcurrentDictionary<string, string> CurrencySymbols = new(
        StringComparer.OrdinalIgnoreCase
    );

    public static void Register(ScriptObject target) =>
        target.Import(typeof(TemplateFormatFunctions));

#pragma warning disable IDE1006 // Scriban builtin names are lowercase

    /// <summary>
    /// Currency-aware number. Named args: <c>currency</c>, <c>culture</c>, <c>decimals</c> (default 2).
    /// </summary>
    public static string money(
        object? value,
        string? currency = null,
        string? culture = null,
        int? decimals = null
    )
    {
        var amount = ParseDecimal(value, "money");
        var decimalsValue = decimals ?? DefaultMoneyDecimals;
        ValidateDecimals(decimalsValue, "money");
        var cultureInfo = ResolveCulture(culture);
        var rounded = decimal.Round(amount, decimalsValue, Rounding);

        var nfi = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();
        nfi.NumberDecimalDigits = decimalsValue;
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            if (!CurrencyCodePattern().IsMatch(code))
            {
                throw new ArgumentException(
                    "money currency must be a 3-letter ISO 4217 code (e.g. USD)."
                );
            }

            nfi.CurrencySymbol = ResolveCurrencySymbol(code, nfi.CurrencySymbol);
            nfi.CurrencyDecimalDigits = decimalsValue;
            return rounded.ToString("C", nfi);
        }

        return rounded.ToString("N", nfi);
    }

    /// <summary>
    /// Plain number. Named args: <c>culture</c>, <c>decimals</c> (omit for culture default).
    /// </summary>
    public static string number(object? value, string? culture = null, int? decimals = null)
    {
        var amount = ParseDecimal(value, "number");
        var cultureInfo = ResolveCulture(culture);
        if (decimals is null)
            return amount.ToString("N", cultureInfo);

        ValidateDecimals(decimals.Value, "number");
        var rounded = decimal.Round(amount, decimals.Value, Rounding);
        var nfi = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();
        nfi.NumberDecimalDigits = decimals.Value;
        return rounded.ToString("N", nfi);
    }

    /// <summary>
    /// Fraction as percent (0.155 → 15.5%). Named args: <c>culture</c>, <c>decimals</c> (default 0).
    /// </summary>
    public static string percent(object? value, string? culture = null, int? decimals = null)
    {
        var fraction = ParseDecimal(value, "percent");
        var decimalsValue = decimals ?? 0;
        ValidateDecimals(decimalsValue, "percent");
        var cultureInfo = ResolveCulture(culture);
        var scaled = decimal.Round(fraction * 100m, decimalsValue, Rounding);
        var nfi = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();
        nfi.NumberDecimalDigits = decimalsValue;
        // Format the already-scaled value; do not use "P" (it multiplies by 100 again).
        return scaled.ToString("N", nfi) + nfi.PercentSymbol;
    }

    /// <summary>
    /// Date-only. Named args: <c>format</c> (<c>short</c>|<c>long</c>|<c>iso</c>|custom), <c>culture</c>.
    /// </summary>
    public static string date(object? value, string? format = null, string? culture = null)
    {
        var dateOnly = ParseDateOnly(value);
        var cultureInfo = ResolveCulture(culture);
        return FormatDateOnly(dateOnly, format, cultureInfo);
    }

    /// <summary>
    /// Date and time. Named args: <c>format</c> (<c>short</c>|<c>long</c>|<c>iso</c>|custom), <c>culture</c>.
    /// </summary>
    public static string datetime(object? value, string? format = null, string? culture = null)
    {
        var dt = ParseDateTime(value);
        var cultureInfo = ResolveCulture(culture);
        return FormatDateTime(dt, format, cultureInfo);
    }

#pragma warning restore IDE1006

    private static void ValidateDecimals(int decimals, string helper)
    {
        if (decimals < MinDecimals || decimals > MaxDecimals)
        {
            throw new ArgumentException(
                $"{helper} decimals must be between {MinDecimals} and {MaxDecimals}."
            );
        }
    }

    private static CultureInfo ResolveCulture(string? culture)
    {
        var name = string.IsNullOrWhiteSpace(culture) ? DefaultCultureName : culture.Trim();
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            throw new ArgumentException(
                $"Unknown culture '{name}'. Use a .NET culture name such as en-US or de-DE."
            );
        }
    }

    private static decimal ParseDecimal(object? value, string helper)
    {
        switch (value)
        {
            case null:
                throw new ArgumentException($"{helper} value is required.");
            case decimal d:
                return d;
            case byte or sbyte or short or ushort or int or uint or long:
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            case ulong u:
                return u;
            case float or double:
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            case string s:
            {
                var text = DecodeBasicEntities(s).Trim();
                if (
                    decimal.TryParse(
                        text,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    )
                )
                    return parsed;
                throw new ArgumentException($"{helper} value '{text}' is not a number.");
            }
            default:
                if (
                    decimal.TryParse(
                        Convert.ToString(value, CultureInfo.InvariantCulture),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var fromObject
                    )
                )
                    return fromObject;
                throw new ArgumentException($"{helper} value is not a number.");
        }
    }

    private static DateOnly ParseDateOnly(object? value)
    {
        switch (value)
        {
            case null:
                throw new ArgumentException("date value is required.");
            case DateOnly d:
                return d;
            case DateTime dt:
                return DateOnly.FromDateTime(dt);
            case DateTimeOffset dto:
                return DateOnly.FromDateTime(dto.DateTime);
            case string s:
            {
                var text = DecodeBasicEntities(s).Trim();
                if (
                    DateOnly.TryParseExact(
                        text,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var iso
                    )
                )
                    return iso;
                if (
                    DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var parsed
                    )
                )
                    return DateOnly.FromDateTime(parsed);
                throw new ArgumentException($"date value '{text}' is not a recognized date.");
            }
            default:
                throw new ArgumentException("date value must be a date or ISO date string.");
        }
    }

    private static DateTime ParseDateTime(object? value)
    {
        switch (value)
        {
            case null:
                throw new ArgumentException("datetime value is required.");
            case DateTime dt:
                return dt;
            case DateTimeOffset dto:
                return dto.UtcDateTime;
            case DateOnly d:
                return d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            case string s:
            {
                var text = DecodeBasicEntities(s).Trim();
                if (
                    DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var parsed
                    )
                )
                    return parsed;
                throw new ArgumentException(
                    $"datetime value '{text}' is not a recognized date-time."
                );
            }
            default:
                throw new ArgumentException(
                    "datetime value must be a date-time or ISO date-time string."
                );
        }
    }

    private static string FormatDateOnly(DateOnly value, string? format, CultureInfo culture)
    {
        var kind = string.IsNullOrWhiteSpace(format) ? "short" : format.Trim();
        return kind.ToLowerInvariant() switch
        {
            "short" => value.ToString("d", culture),
            "long" => value.ToString("D", culture),
            "iso" => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value.ToString(kind, culture),
        };
    }

    private static string FormatDateTime(DateTime value, string? format, CultureInfo culture)
    {
        var kind = string.IsNullOrWhiteSpace(format) ? "short" : format.Trim();
        return kind.ToLowerInvariant() switch
        {
            "short" => value.ToString("g", culture),
            "long" => value.ToString("F", culture),
            "iso" => value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            _ => value.ToString(kind, culture),
        };
    }

    private static string ResolveCurrencySymbol(string iso4217, string fallback)
    {
        var symbol = CurrencySymbols.GetOrAdd(
            iso4217,
            static code =>
            {
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                {
                    try
                    {
                        var region = new RegionInfo(culture.Name);
                        if (
                            string.Equals(
                                region.ISOCurrencySymbol,
                                code,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            return region.CurrencySymbol;
                    }
                    catch (ArgumentException)
                    {
                        // Some cultures do not map to a RegionInfo.
                    }
                }

                return code;
            }
        );
        return string.IsNullOrEmpty(symbol) ? fallback : symbol;
    }

    /// <summary>
    /// Data strings are HTML-encoded on import; undo common entities so ISO dates/numbers still parse.
    /// </summary>
    private static string DecodeBasicEntities(string value) =>
        value
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCodePattern();
}
