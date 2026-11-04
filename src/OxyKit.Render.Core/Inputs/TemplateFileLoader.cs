using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace OxyKit.Render.Core.Inputs;

/// <summary>
/// Scriban <c>include</c> loader scoped to one template directory (partials only).
/// </summary>
internal sealed class TemplateFileLoader : ITemplateLoader
{
    private readonly string _templateDirectory;
    private readonly int _maxPartialChars;

    public TemplateFileLoader(string templateDirectory, int maxPartialChars)
    {
        _templateDirectory = templateDirectory;
        _maxPartialChars = maxPartialChars;
    }

    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        var fullPath = TemplatePathSafety.ResolveUnderTemplateRoot(
            _templateDirectory,
            templateName,
            "include"
        );
        var ext = Path.GetExtension(fullPath);
        if (!string.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("include path must end with .html.");

        if (!File.Exists(fullPath))
            throw new ArgumentException($"include '{templateName}' was not found in the template folder.");

        return fullPath;
    }

    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        var text = File.ReadAllText(templatePath);
        if (text.Length > _maxPartialChars)
        {
            throw new ArgumentException(
                $"include exceeds the maximum length of {_maxPartialChars:N0} characters."
            );
        }

        return text;
    }

    public ValueTask<string?> LoadAsync(
        TemplateContext context,
        SourceSpan callerSpan,
        string templatePath
    ) => new(Load(context, callerSpan, templatePath));
}
