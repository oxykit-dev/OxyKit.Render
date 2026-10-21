namespace OxyKit.Render.Core;

internal sealed record ValidatedWorkbook(IReadOnlyList<ValidatedSheet> Sheets);

internal sealed record ValidatedSheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<object?>> Rows
);
