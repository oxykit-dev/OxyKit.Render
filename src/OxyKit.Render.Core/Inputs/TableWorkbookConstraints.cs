using System.Globalization;
using System.Text.Json;
using OxyKit.Render.Contracts;

namespace OxyKit.Render.Core.Inputs;

internal static class TableWorkbookConstraints
{
    public const int MaxSheets = 20;
    public const int MaxColumns = 100;
    public const int MaxRows = 10_000;
    public const int MaxCellChars = 32_767;
    public const int MaxSheetNameLength = 31;

    private static readonly char[] InvalidSheetNameChars = ['\\', '/', '*', '?', ':', '[', ']'];

    public static ValidatedWorkbook Validate(TableRenderRequest? request)
    {
        if (request?.Sheets is null || request.Sheets.Count == 0)
        {
            throw new ArgumentException("Field 'sheets' must contain at least one sheet.");
        }

        if (request.Sheets.Count > MaxSheets)
        {
            throw new ArgumentException($"At most {MaxSheets} sheets are allowed.");
        }

        var sheets = new List<ValidatedSheet>(request.Sheets.Count);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var sheetIndex = 0; sheetIndex < request.Sheets.Count; sheetIndex++)
        {
            sheets.Add(ValidateSheet(request.Sheets[sheetIndex], sheetIndex, usedNames));
        }

        return new ValidatedWorkbook(sheets);
    }

    /// <summary>
    /// Validates a string grid (e.g. from CSV) into a single-sheet workbook.
    /// Empty cells become null; ISO dates, booleans, and canonical invariant numbers are coerced.
    /// </summary>
    public static ValidatedWorkbook ValidateStringGrid(
        string? sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> dataRows
    )
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var name = ResolveSheetName(sheetName, defaultIndex: 0, usedNames);
        var columnCount = ResolveColumnCount(name, headers, dataRows);
        ValidateHeaders(name, headers);

        if (dataRows.Count > MaxRows)
        {
            throw new ArgumentException(
                $"Sheet '{name}' has {dataRows.Count} rows; at most {MaxRows} are allowed."
            );
        }

        var validatedRows = new List<IReadOnlyList<object?>>(dataRows.Count);
        for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
        {
            var row = dataRows[rowIndex];
            EnsureRowWidth(name, rowIndex, row.Count, columnCount);

            var cells = new object?[columnCount];
            for (var col = 0; col < columnCount; col++)
            {
                cells[col] = NormalizeTextCell(row[col], name, rowIndex, col);
            }

            validatedRows.Add(cells);
        }

        return new ValidatedWorkbook([new ValidatedSheet(name, headers, validatedRows)]);
    }

    private static ValidatedSheet ValidateSheet(
        TableSheet sheet,
        int sheetIndex,
        HashSet<string> usedNames
    )
    {
        var name = ResolveSheetName(sheet.Name, sheetIndex, usedNames);
        var headers = sheet.Headers ?? [];
        var rows = sheet.Rows ?? [];
        var columnCount = ResolveColumnCount(name, headers, rows);
        ValidateHeaders(name, headers);

        if (rows.Count > MaxRows)
        {
            throw new ArgumentException(
                $"Sheet '{name}' has {rows.Count} rows; at most {MaxRows} are allowed."
            );
        }

        var validatedRows = new List<IReadOnlyList<object?>>(rows.Count);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            EnsureRowWidth(name, rowIndex, row.Count, columnCount);

            var cells = new object?[columnCount];
            for (var col = 0; col < columnCount; col++)
            {
                cells[col] = NormalizeCell(row[col], name, rowIndex, col);
            }

            validatedRows.Add(cells);
        }

        return new ValidatedSheet(name, headers, validatedRows);
    }

    private static string ResolveSheetName(
        string? sheetName,
        int defaultIndex,
        HashSet<string> usedNames
    )
    {
        var name = string.IsNullOrWhiteSpace(sheetName)
            ? $"Sheet{defaultIndex + 1}"
            : sheetName.Trim();

        if (name.Length > MaxSheetNameLength)
        {
            throw new ArgumentException(
                $"Sheet name '{name}' exceeds Excel's {MaxSheetNameLength}-character limit."
            );
        }

        if (name.IndexOfAny(InvalidSheetNameChars) >= 0)
        {
            throw new ArgumentException(
                $"Sheet name '{name}' contains invalid characters (\\ / * ? : [ ])."
            );
        }

        if (!usedNames.Add(name))
        {
            throw new ArgumentException($"Duplicate sheet name '{name}'.");
        }

        return name;
    }

    private static void ValidateHeaders(string sheetName, IReadOnlyList<string> headers)
    {
        if (headers.Count > MaxColumns)
        {
            throw new ArgumentException(
                $"Sheet '{sheetName}' has {headers.Count} headers; at most {MaxColumns} columns are allowed."
            );
        }

        for (var i = 0; i < headers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(headers[i]))
            {
                throw new ArgumentException(
                    $"Sheet '{sheetName}' header at index {i} must be a non-empty string."
                );
            }

            if (headers[i].Length > MaxCellChars)
            {
                throw new ArgumentException(
                    $"Sheet '{sheetName}' header at index {i} exceeds {MaxCellChars} characters."
                );
            }
        }

        if (headers.Count != headers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new ArgumentException(
                $"Sheet '{sheetName}' headers must be unique (case-insensitive)."
            );
        }
    }

    private static int ResolveColumnCount<T>(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<T>> rows
    )
    {
        var columnCount = headers.Count;
        if (columnCount == 0 && rows.Count > 0)
        {
            columnCount = rows[0].Count;
            if (columnCount == 0)
            {
                throw new ArgumentException(
                    $"Sheet '{sheetName}' row 0 must contain at least one cell when headers are omitted."
                );
            }

            if (columnCount > MaxColumns)
            {
                throw new ArgumentException(
                    $"Sheet '{sheetName}' has {columnCount} columns; at most {MaxColumns} are allowed."
                );
            }
        }

        if (columnCount == 0)
        {
            throw new ArgumentException(
                $"Sheet '{sheetName}' must include headers and/or at least one data row."
            );
        }

        return columnCount;
    }

    private static void EnsureRowWidth(string sheetName, int rowIndex, int actual, int expected)
    {
        if (actual != expected)
        {
            throw new ArgumentException(
                $"Sheet '{sheetName}' row {rowIndex} has {actual} cells; expected {expected}."
            );
        }
    }

    private static object? NormalizeCell(
        JsonElement cell,
        string sheetName,
        int rowIndex,
        int colIndex
    )
    {
        switch (cell.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                var text = cell.GetString();
                if (text is null)
                {
                    return null;
                }

                if (text.Length > MaxCellChars)
                {
                    throw new ArgumentException(
                        $"Sheet '{sheetName}' cell[{rowIndex},{colIndex}] exceeds {MaxCellChars} characters."
                    );
                }

                if (IsoDateCell.TryParse(text, out var dateValue))
                {
                    return dateValue;
                }

                return text;
            case JsonValueKind.Number:
                if (cell.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                return cell.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                throw new ArgumentException(
                    $"Sheet '{sheetName}' cell[{rowIndex},{colIndex}] must be a string, number, boolean, or null."
                );
        }
    }

    /// <summary>
    /// CSV/text cell typing: empty → null; ISO dates; true/false; canonical integers;
    /// finite decimals/exponents. Leading-zero IDs and non-finite tokens stay text.
    /// </summary>
    internal static object? NormalizeTextCell(
        string? text,
        string sheetName,
        int rowIndex,
        int colIndex
    )
    {
        if (text is null || text.Length == 0)
        {
            return null;
        }

        if (text.Length > MaxCellChars)
        {
            throw new ArgumentException(
                $"Sheet '{sheetName}' cell[{rowIndex},{colIndex}] exceeds {MaxCellChars} characters."
            );
        }

        if (IsoDateCell.TryParse(text, out var dateValue))
        {
            return dateValue;
        }

        if (bool.TryParse(text, out var flag))
        {
            return flag;
        }

        if (TryParseCsvNumber(text, out var number))
        {
            return number;
        }

        return text;
    }

    private static bool TryParseCsvNumber(string text, out object value)
    {
        if (
            IsCanonicalInteger(text)
            && long.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var longValue
            )
        )
        {
            value = longValue;
            return true;
        }

        // Require a decimal point or exponent so leading-zero integers stay text.
        if (
            (text.Contains('.') || text.Contains('e', StringComparison.OrdinalIgnoreCase))
            && double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var doubleValue
            )
            && double.IsFinite(doubleValue)
        )
        {
            value = doubleValue;
            return true;
        }

        value = null!;
        return false;
    }

    private static bool IsCanonicalInteger(string text)
    {
        var index = 0;
        if (text[0] is '+' or '-')
        {
            index = 1;
        }

        if (index >= text.Length)
        {
            return false;
        }

        if (text[index] == '0')
        {
            return text.Length == index + 1;
        }

        for (; index < text.Length; index++)
        {
            if (!char.IsAsciiDigit(text[index]))
            {
                return false;
            }
        }

        return true;
    }
}
