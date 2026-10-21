using System.Text.Json;

namespace OxyKit.Render.Contracts;

/// <summary>
/// CSV → PDF. <see cref="Content"/> is required; page size/orientation default to A4/Portrait.
/// </summary>
public sealed class CsvPdfRenderRequest
{
    public string? Content { get; init; }
    public bool? HasHeader { get; init; }
    public string? Name { get; init; }
    public PdfPageSize? PageSize { get; init; }
    public PdfPageOrientation? PageOrientation { get; init; }
}

/// <summary>
/// CSV → XLSX. <see cref="Content"/> is required.
/// </summary>
public sealed class CsvXlsxRenderRequest
{
    public string? Content { get; init; }
    public bool? HasHeader { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// HTML → PDF. <see cref="Content"/> is required; page size/orientation default to A4/Portrait.
/// </summary>
public sealed class HtmlPdfRenderRequest
{
    public string? Content { get; init; }
    public PdfPageSize? PageSize { get; init; }
    public PdfPageOrientation? PageOrientation { get; init; }
}

/// <summary>
/// Local Scriban HTML template → HTML (browser preview). <see cref="TemplateId"/> and <see cref="Data"/> are required.
/// </summary>
public sealed class TemplateHtmlRenderRequest
{
    public string? TemplateId { get; init; }
    public JsonElement? Data { get; init; }
}

/// <summary>
/// Local Scriban HTML template → PDF. <see cref="TemplateId"/> and <see cref="Data"/> are required.
/// </summary>
public sealed class TemplatePdfRenderRequest
{
    public string? TemplateId { get; init; }
    public JsonElement? Data { get; init; }
    public PdfPageSize? PageSize { get; init; }
    public PdfPageOrientation? PageOrientation { get; init; }
}

/// <summary>
/// Table → HTML / PDF / XLSX / CSV. <see cref="Sheets"/> is required.
/// PDF uses optional <see cref="PageSize"/> / <see cref="PageOrientation"/> (default A4/Portrait).
/// CSV requires exactly one sheet; page options are ignored for non-PDF outputs.
/// </summary>
public sealed class TableRenderRequest
{
    public IReadOnlyList<TableSheet>? Sheets { get; init; }
    public PdfPageSize? PageSize { get; init; }
    public PdfPageOrientation? PageOrientation { get; init; }
}

public sealed class TableSheet
{
    public string? Name { get; init; }
    public IReadOnlyList<string>? Headers { get; init; }
    public IReadOnlyList<IReadOnlyList<JsonElement>>? Rows { get; init; }
}
