using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scriban;

namespace OxyKit.Render.Core.Inputs;

internal sealed record TemplateDefinition(
    string Id,
    string Html,
    string ContractJson,
    string Directory,
    Template Parsed
);

internal sealed record TemplateInfo(string Id, bool HasContract);

/// <summary>
/// Loads templates from a configured root. Each template is a subdirectory with
/// <c>template.html</c> and required <c>contract.json</c>.
/// Caches HTML + contract + parsed Scriban; invalidated on pack write/delete and when
/// on-disk mtime/size stamps change (out-of-band edits).
/// </summary>
internal sealed partial class TemplateStore
{
    private readonly string _rootPath;
    private readonly RenderingOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(
        StringComparer.OrdinalIgnoreCase
    );

    public TemplateStore(IOptions<RenderingOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _rootPath = ResolveRoot(_options.TemplatesRoot, environment.ContentRootPath);
    }

    public IReadOnlyList<TemplateInfo> List()
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        var results = new List<TemplateInfo>();
        foreach (var dir in Directory.EnumerateDirectories(_rootPath))
        {
            var id = Path.GetFileName(dir);
            if (!SafeTemplateId().IsMatch(id))
            {
                continue;
            }

            var hasTemplate = File.Exists(Path.Combine(dir, "template.html"));
            var hasContract = File.Exists(Path.Combine(dir, "contract.json"));
            if (hasTemplate)
            {
                results.Add(new TemplateInfo(id, hasContract));
            }
        }

        results.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return results;
    }

    public TemplateDefinition Load(string? templateId)
    {
        var id = NormalizeId(templateId);
        var dir = ResolveTemplateDirectory(id);

        var templatePath = Path.Combine(dir, "template.html");
        var contractPath = Path.Combine(dir, "contract.json");

        if (!File.Exists(templatePath))
        {
            throw new ArgumentException($"Template '{id}' was not found.");
        }

        if (!File.Exists(contractPath))
        {
            throw new ArgumentException($"Template '{id}' is missing required contract.json.");
        }

        var templateStamp = ReadStamp(templatePath);
        var contractStamp = ReadStamp(contractPath);

        if (
            _cache.TryGetValue(id, out var cached)
            && cached.TemplateStamp == templateStamp
            && cached.ContractStamp == contractStamp
        )
        {
            return cached.Definition;
        }

        var html = File.ReadAllText(templatePath);
        if (html.Length > _options.MaxTemplateChars)
        {
            throw new ArgumentException(
                $"Template '{id}' exceeds the maximum length of {_options.MaxTemplateChars:N0} characters."
            );
        }

        var contractJson = File.ReadAllText(contractPath);
        if (contractJson.Length > _options.MaxContractChars)
        {
            throw new ArgumentException(
                $"Template '{id}' contract exceeds the maximum length of {_options.MaxContractChars:N0} characters."
            );
        }

        var parsed = ScribanTemplateRenderer.Parse(html);
        var definition = new TemplateDefinition(id, html, contractJson, dir, parsed);
        _cache[id] = new CacheEntry(definition, templateStamp, contractStamp);
        return definition;
    }

    internal void Invalidate(string templateId) => _cache.TryRemove(templateId, out _);

    private static FileStamp ReadStamp(string path)
    {
        var info = new FileInfo(path);
        return new FileStamp(info.LastWriteTimeUtc.Ticks, info.Length);
    }

    private static string ResolveRoot(string configured, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "templates";
        }

        var root = Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(contentRoot, configured));

        return root;
    }

    private static string NormalizeId(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Field 'templateId' is required.");
        }

        var id = templateId.Trim();
        if (!SafeTemplateId().IsMatch(id))
        {
            throw new ArgumentException(
                "Field 'templateId' must be a simple name (letters, digits, '-', '_')."
            );
        }

        return id;
    }

    private string ResolveTemplateDirectory(string id)
    {
        // Combine then full-path; ensure result stays under root (blocks .. and absolute tricks).
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, id));
        var rootWithSep = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (
            !candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, _rootPath, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException($"Template '{id}' was not found.");
        }

        if (!Directory.Exists(candidate))
        {
            throw new ArgumentException($"Template '{id}' was not found.");
        }

        return candidate;
    }

    private readonly record struct FileStamp(long Ticks, long Length);

    private sealed record CacheEntry(
        TemplateDefinition Definition,
        FileStamp TemplateStamp,
        FileStamp ContractStamp
    );

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTemplateId();
}
