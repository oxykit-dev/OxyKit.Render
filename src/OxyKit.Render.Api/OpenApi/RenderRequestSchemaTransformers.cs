using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OxyKit.Render.Contracts;

namespace OxyKit.Render.Api.OpenApi;

/// <summary>Single schema transformer for all render request DTOs.</summary>
internal sealed class RenderRequestSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(HtmlPdfRenderRequest))
        {
            MarkOptionalPageOptions(schema);
            schema.Description =
                "HTML markup in content → PDF. pageSize/pageOrientation default to A4/Portrait when omitted. "
                + "Content length is capped (see Rendering:MaxHtmlContentChars). Remote HTTP and local file resources are not loaded.";
            Describe(schema, "content", "HTML markup to convert to PDF.");
            OpenApiSchemaHelpers.SetPageOptionDescriptions(schema.Properties!);
        }
        else if (type == typeof(TemplateHtmlRenderRequest))
        {
            schema.Description =
                "Local Scriban HTML template → HTML (browser preview). templateId names a folder under Rendering:TemplatesRoot "
                + "containing template.html and required contract.json. data must satisfy the contract. "
                + "Same render path as template/pdf without PeachPDF conversion.";
            Describe(schema, "templateId", "Template folder name (letters, digits, '-', '_').");
            Describe(
                schema,
                "data",
                "JSON object validated against the template's contract.json before render."
            );
        }
        else if (type == typeof(TemplatePdfRenderRequest))
        {
            MarkOptionalPageOptions(schema);
            schema.Description =
                "Local Scriban HTML template → PDF. templateId names a folder under Rendering:TemplatesRoot "
                + "containing template.html and required contract.json. data must satisfy the contract. "
                + "pageSize/pageOrientation default to A4/Portrait.";
            Describe(schema, "templateId", "Template folder name (letters, digits, '-', '_').");
            Describe(
                schema,
                "data",
                "JSON object validated against the template's contract.json before render."
            );
            OpenApiSchemaHelpers.SetPageOptionDescriptions(schema.Properties!);
        }
        else if (type == typeof(CsvPdfRenderRequest))
        {
            schema.Required?.Remove("hasHeader");
            schema.Required?.Remove("name");
            MarkOptionalPageOptions(schema);
            schema.Description =
                "RFC 4180 CSV in content → PDF. hasHeader defaults to true. "
                + "pageSize/pageOrientation default to A4/Portrait. Content length is capped (Rendering:MaxCsvContentChars).";
            Describe(schema, "content", "CSV text (UTF-8). Optional BOM is stripped.");
            Describe(
                schema,
                "hasHeader",
                "When true (default), the first row is treated as column headers."
            );
            Describe(schema, "name", "Sheet title. Defaults to Sheet1.");
            OpenApiSchemaHelpers.SetPageOptionDescriptions(schema.Properties!);
        }
        else if (type == typeof(CsvXlsxRenderRequest))
        {
            schema.Required?.Remove("hasHeader");
            schema.Required?.Remove("name");
            schema.Description =
                "RFC 4180 CSV in content → Excel. hasHeader defaults to true. "
                + "Empty cells become null; ISO dates, true/false, and canonical numbers are typed "
                + "(leading-zero IDs stay text). Content length is capped (Rendering:MaxCsvContentChars).";
            Describe(schema, "content", "CSV text (UTF-8). Optional BOM is stripped.");
            Describe(
                schema,
                "hasHeader",
                "When true (default), the first row is treated as column headers."
            );
            Describe(schema, "name", "Sheet title. Defaults to Sheet1.");
        }
        else if (type == typeof(TableRenderRequest))
        {
            MarkOptionalPageOptions(schema);
            schema.Description =
                "Tabular workbook for table/html, table/pdf, table/xlsx, and table/csv. "
                + "Cells must be string, number, boolean, or null. "
                + "ISO-8601 date/date-time strings become dates (Excel) or formatted text (HTML/CSV). "
                + "table/csv requires exactly one sheet. "
                + "pageSize/pageOrientation apply to table/pdf only (default A4/Portrait); ignored by other table outputs.";
            Describe(
                schema,
                "sheets",
                "Sheets with name, optional headers, and rows (max 20). table/csv requires exactly one sheet."
            );
            OpenApiSchemaHelpers.SetPageOptionDescriptions(schema.Properties!);
        }

        return Task.CompletedTask;
    }

    private static void MarkOptionalPageOptions(OpenApiSchema schema)
    {
        schema.Required?.Remove("pageSize");
        schema.Required?.Remove("pageOrientation");
    }

    private static void Describe(OpenApiSchema schema, string name, string description)
    {
        if (schema.Properties is null)
        {
            return;
        }

        OpenApiSchemaHelpers.SetDescription(schema.Properties, name, description);
    }
}
