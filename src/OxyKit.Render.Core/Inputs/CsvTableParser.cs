namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Parses RFC 4180 CSV text into a single-sheet <see cref="ValidatedWorkbook"/>.
/// </summary>
internal static class CsvTableParser
{
    public static ValidatedWorkbook Parse(string content, string? sheetName, bool hasHeader)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        var grid = ParseGrid(content);
        if (grid.Count == 0)
        {
            throw new ArgumentException("CSV content must contain at least one row.");
        }

        IReadOnlyList<string> headers;
        IReadOnlyList<IReadOnlyList<string>> dataRows;

        if (hasHeader)
        {
            headers = grid[0];
            dataRows = grid.Count > 1 ? grid.Skip(1).ToArray() : [];
            if (headers.Count == 0)
            {
                throw new ArgumentException("CSV header row must contain at least one column.");
            }
        }
        else
        {
            headers = [];
            dataRows = grid;
        }

        return TableWorkbookConstraints.ValidateStringGrid(sheetName, headers, dataRows);
    }

    private static List<IReadOnlyList<string>> ParseGrid(string content)
    {
        var rows = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var i = 0;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            rows.Add(fields.ToArray());
            fields.Clear();
        }

        while (i < content.Length)
        {
            var ch = content[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    if (i < content.Length && content[i] is not (',' or '\r' or '\n'))
                    {
                        throw new ArgumentException(
                            "CSV content has unexpected characters after a closing quote."
                        );
                    }

                    continue;
                }

                field.Append(ch);
                i++;
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    EndField();
                    i++;
                    break;
                case '\r':
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRow();
                    i++;
                    break;
                case '\n':
                    EndRow();
                    i++;
                    break;
                default:
                    field.Append(ch);
                    i++;
                    break;
            }
        }

        if (inQuotes)
        {
            throw new ArgumentException("CSV content has an unclosed quoted field.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            EndRow();
        }

        while (rows.Count > 1 && IsBlankRow(rows[^1]))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static bool IsBlankRow(IReadOnlyList<string> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            if (row[i].Length > 0)
            {
                return false;
            }
        }

        return true;
    }
}
