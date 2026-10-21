namespace OxyKit.Render.Core;

internal sealed class RenderingOptions
{
    public const string SectionName = "Rendering";

    /// <summary>Maximum HTML character length for /render/html/pdf.</summary>
    public int MaxHtmlContentChars { get; init; } = 1_000_000;

    /// <summary>Maximum CSV character length for /render/csv/*.</summary>
    public int MaxCsvContentChars { get; init; } = 1_000_000;

    /// <summary>Maximum JSON request body size in bytes.</summary>
    public long MaxRequestBodyBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>Max concurrent render operations (PDF/XLSX).</summary>
    public int MaxConcurrentRenders { get; init; } = 4;

    /// <summary>
    /// Root directory for local HTML templates (relative to content root or absolute).
    /// Each template is a subdirectory containing template.html and contract.json.
    /// </summary>
    public string TemplatesRoot { get; init; } = "templates";

    /// <summary>Maximum size in characters for a single template.html file.</summary>
    public int MaxTemplateChars { get; init; } = 500_000;

    /// <summary>Maximum size in characters for a single contract.json file.</summary>
    public int MaxContractChars { get; init; } = 100_000;
}
