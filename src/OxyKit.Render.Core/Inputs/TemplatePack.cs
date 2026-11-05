using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OxyKit.Render.Core.Inputs;

internal sealed record TemplateDetail(string Id, bool HasContract, IReadOnlyList<string> Files);

/// <summary>
/// Zip import/export for on-disk template packs under <see cref="RenderingOptions.TemplatesRoot"/>.
/// </summary>
internal sealed partial class TemplateStore
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public TemplateDetail Describe(string? templateId)
    {
        var id = NormalizeId(templateId);
        var dir = ResolveTemplateDirectory(id);
        var hasTemplate = File.Exists(Path.Combine(dir, "template.html"));
        if (!hasTemplate)
        {
            throw new ArgumentException($"Template '{id}' was not found.");
        }

        var hasContract = File.Exists(Path.Combine(dir, "contract.json"));
        var files = EnumeratePackFiles(dir).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        return new TemplateDetail(id, hasContract, files);
    }

    public async Task<TemplateInfo> SaveFromZipAsync(
        string? templateId,
        Stream zipStream,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        var id = NormalizeId(templateId);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_rootPath);

            var staging = Path.Combine(
                _rootPath,
                $".incoming-{id}-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(staging);

            try
            {
                await ExtractAndValidateZipAsync(zipStream, staging, cancellationToken)
                    .ConfigureAwait(false);

                var target = Path.Combine(_rootPath, id);
                var backup = Path.Combine(
                    _rootPath,
                    $".backup-{id}-{Guid.NewGuid():N}"
                );

                if (Directory.Exists(target))
                {
                    Directory.Move(target, backup);
                }

                try
                {
                    Directory.Move(staging, target);
                }
                catch
                {
                    if (Directory.Exists(backup) && !Directory.Exists(target))
                    {
                        Directory.Move(backup, target);
                    }

                    throw;
                }

                if (Directory.Exists(backup))
                {
                    TryDeleteDirectory(backup);
                }

                var hasContract = File.Exists(Path.Combine(target, "contract.json"));
                return new TemplateInfo(id, hasContract);
            }
            catch
            {
                TryDeleteDirectory(staging);
                throw;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task DeleteAsync(string? templateId, CancellationToken cancellationToken = default)
    {
        var id = NormalizeId(templateId);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = Path.GetFullPath(Path.Combine(_rootPath, id));
            EnsureUnderRoot(dir, id);

            if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "template.html")))
            {
                throw new ArgumentException($"Template '{id}' was not found.");
            }

            TryDeleteDirectory(dir);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Writes a single image under <c>assets/</c>. <paramref name="assetLabel"/> is a file name
    /// or relative path (e.g. <c>logo.svg</c> or <c>brand/logo.png</c>); optional <c>assets/</c> prefix is stripped.
    /// Returns the pack-relative path for <c>asset</c> helper use (always <c>assets/…</c>).
    /// </summary>
    public async Task<string> SaveAssetAsync(
        string? templateId,
        string? assetLabel,
        Stream content,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        var id = NormalizeId(templateId);
        var relative = NormalizeAssetRelativePath(assetLabel);
        ValidatePackRelativePath(relative);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = ResolveTemplateDirectory(id);
            if (!File.Exists(Path.Combine(dir, "template.html")))
            {
                throw new ArgumentException($"Template '{id}' was not found.");
            }

            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
            {
                throw new ArgumentException("Asset body is empty.");
            }

            if (buffer.Length > _options.MaxTemplateAssetBytes)
            {
                throw new ArgumentException(
                    $"Asset exceeds the maximum size of {_options.MaxTemplateAssetBytes:N0} bytes."
                );
            }

            var dest = Path.GetFullPath(
                Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar))
            );
            EnsureUnderDirectory(dir, dest, relative);
            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var temp = dest + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                buffer.Position = 0;
                await using (var file = File.Create(temp))
                {
                    await buffer.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temp, dest, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                        // best-effort
                    }
                }
            }

            return relative;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task DeleteAssetAsync(
        string? templateId,
        string? assetLabel,
        CancellationToken cancellationToken = default
    )
    {
        var id = NormalizeId(templateId);
        var relative = NormalizeAssetRelativePath(assetLabel);
        ValidatePackRelativePath(relative);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = ResolveTemplateDirectory(id);
            if (!File.Exists(Path.Combine(dir, "template.html")))
            {
                throw new ArgumentException($"Template '{id}' was not found.");
            }

            var dest = Path.GetFullPath(
                Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar))
            );
            EnsureUnderDirectory(dir, dest, relative);
            if (!File.Exists(dest))
            {
                throw new ArgumentException($"Asset '{relative}' was not found.");
            }

            File.Delete(dest);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task WriteZipAsync(
        string? templateId,
        Stream destination,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        var id = NormalizeId(templateId);
        var dir = ResolveTemplateDirectory(id);
        if (!File.Exists(Path.Combine(dir, "template.html")))
        {
            throw new ArgumentException($"Template '{id}' was not found.");
        }

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var relative in EnumeratePackFiles(dir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
            archive.CreateEntryFromFile(fullPath, relative.Replace('\\', '/'), CompressionLevel.Optimal);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task ExtractAndValidateZipAsync(
        Stream zipStream,
        string staging,
        CancellationToken cancellationToken
    )
    {
        // Buffer so we can enforce pack size before extracting.
        await using var buffer = new MemoryStream();
        await zipStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (buffer.Length == 0)
        {
            throw new ArgumentException("Template pack zip is empty.");
        }

        if (buffer.Length > _options.MaxTemplatePackBytes)
        {
            throw new ArgumentException(
                $"Template pack exceeds the maximum size of {_options.MaxTemplatePackBytes:N0} bytes."
            );
        }

        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);

        var entries = archive
            .Entries.Where(e => !string.IsNullOrEmpty(e.FullName) && !e.FullName.EndsWith('/'))
            .ToList();
        if (entries.Count == 0)
        {
            throw new ArgumentException("Template pack zip has no files.");
        }

        var stripPrefix = DetectSingleRootPrefix(entries.Select(e => e.FullName));

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = NormalizeZipEntryPath(entry.FullName, stripPrefix);
            ValidatePackRelativePath(relative);

            if (entry.Length > GetMaxBytesForRelativePath(relative))
            {
                throw new ArgumentException(
                    $"Pack entry '{relative}' exceeds the allowed size for that file type."
                );
            }

            var dest = Path.GetFullPath(
                Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar))
            );
            EnsureUnderDirectory(staging, dest, relative);

            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await using var entryStream = entry.Open();
            await using var file = File.Create(dest);
            await entryStream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var templatePath = Path.Combine(staging, "template.html");
        var contractPath = Path.Combine(staging, "contract.json");
        if (!File.Exists(templatePath))
        {
            throw new ArgumentException("Template pack must include template.html.");
        }

        if (!File.Exists(contractPath))
        {
            throw new ArgumentException("Template pack must include contract.json.");
        }

        var html = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);
        if (html.Length > _options.MaxTemplateChars)
        {
            throw new ArgumentException(
                $"template.html exceeds the maximum length of {_options.MaxTemplateChars:N0} characters."
            );
        }

        var contractJson = await File.ReadAllTextAsync(contractPath, cancellationToken)
            .ConfigureAwait(false);
        if (contractJson.Length > _options.MaxContractChars)
        {
            throw new ArgumentException(
                $"contract.json exceeds the maximum length of {_options.MaxContractChars:N0} characters."
            );
        }

        try
        {
            using var _ = JsonDocument.Parse(contractJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("contract.json must be valid JSON.", ex);
        }

        foreach (var relative in EnumeratePackFiles(staging))
        {
            if (relative.StartsWith("partials/", StringComparison.OrdinalIgnoreCase))
            {
                var text = await File.ReadAllTextAsync(
                        Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (text.Length > _options.MaxTemplatePartialChars)
                {
                    throw new ArgumentException(
                        $"Partial '{relative}' exceeds the maximum length of {_options.MaxTemplatePartialChars:N0} characters."
                    );
                }
            }
        }
    }

    private long GetMaxBytesForRelativePath(string relative)
    {
        if (relative.Equals("template.html", StringComparison.OrdinalIgnoreCase))
            return _options.MaxTemplateChars * 4L; // UTF-8 upper bound
        if (relative.Equals("contract.json", StringComparison.OrdinalIgnoreCase))
            return _options.MaxContractChars * 4L;
        if (relative.Equals("sample-data.json", StringComparison.OrdinalIgnoreCase))
            return _options.MaxContractChars * 4L;
        if (relative.StartsWith("partials/", StringComparison.OrdinalIgnoreCase))
            return _options.MaxTemplatePartialChars * 4L;
        if (relative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            return _options.MaxTemplateAssetBytes;
        return _options.MaxTemplateAssetBytes;
    }

    /// <summary>
    /// Maps a label or relative path to pack-relative <c>assets/…</c> (for <c>{{ asset "assets/…" }}</c>).
    /// </summary>
    private static string NormalizeAssetRelativePath(string? assetLabel)
    {
        if (string.IsNullOrWhiteSpace(assetLabel))
        {
            throw new ArgumentException("Asset path is required.");
        }

        var name = assetLabel.Trim().Replace('\\', '/').Trim('/');
        if (name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            name = name["assets/".Length..];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Asset path must be a relative name under assets/ (no '..').");
        }

        return "assets/" + name;
    }

    private static void ValidatePackRelativePath(string relative)
    {
        if (
            relative.Equals("template.html", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("contract.json", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("sample-data.json", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        if (relative.StartsWith("partials/", StringComparison.OrdinalIgnoreCase))
        {
            if (!relative.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Pack entry '{relative}' must be a .html partial.");
            if (!SafePackPath().IsMatch(relative))
                throw new ArgumentException($"Pack entry '{relative}' has an unsafe path.");
            return;
        }

        if (relative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(relative);
            if (
                !ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".svg", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new ArgumentException(
                    $"Pack entry '{relative}' must be an allowlisted image (png/jpg/gif/webp/svg)."
                );
            }

            if (!SafePackPath().IsMatch(relative))
                throw new ArgumentException($"Pack entry '{relative}' has an unsafe path.");
            return;
        }

        throw new ArgumentException(
            $"Pack entry '{relative}' is not allowed. Use template.html, contract.json, optional sample-data.json, partials/*.html, assets/*."
        );
    }

    private static string NormalizeZipEntryPath(string fullName, string? stripPrefix)
    {
        var name = fullName.Replace('\\', '/').Trim('/');
        if (name.Contains("..", StringComparison.Ordinal) || name.StartsWith('/'))
        {
            throw new ArgumentException($"Pack entry '{fullName}' has an unsafe path.");
        }

        if (!string.IsNullOrEmpty(stripPrefix))
        {
            if (!name.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Pack entry '{fullName}' is outside the pack root folder.");
            }

            name = name[stripPrefix.Length..];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Pack entry '{fullName}' has an unsafe path.");
        }

        if (!SafePackPath().IsMatch(name))
        {
            throw new ArgumentException($"Pack entry '{name}' has an unsafe path.");
        }

        return name;
    }

    /// <summary>
    /// If every entry lives under a single top-level folder and template.html is not at zip root,
    /// strip that folder (typical when zipping a directory).
    /// </summary>
    private static string? DetectSingleRootPrefix(IEnumerable<string> fullNames)
    {
        var normalized = fullNames.Select(n => n.Replace('\\', '/').Trim('/')).ToArray();
        if (normalized.Any(n => n.Equals("template.html", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string? prefix = null;
        foreach (var name in normalized)
        {
            var slash = name.IndexOf('/');
            if (slash <= 0)
            {
                return null;
            }

            var root = name[..(slash + 1)];
            if (prefix is null)
            {
                prefix = root;
            }
            else if (!string.Equals(prefix, root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (prefix is null)
        {
            return null;
        }

        var hasTemplate = normalized.Any(n =>
            n.Equals(prefix + "template.html", StringComparison.OrdinalIgnoreCase)
        );
        return hasTemplate ? prefix : null;
    }

    private static IEnumerable<string> EnumeratePackFiles(string dir)
    {
        var root = Path.GetFullPath(dir);
        foreach (
            var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        )
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            // Only advertise known pack shapes (ignore stray files).
            try
            {
                ValidatePackRelativePath(relative);
            }
            catch (ArgumentException)
            {
                continue;
            }

            yield return relative;
        }
    }

    private void EnsureUnderRoot(string candidate, string id)
    {
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
    }

    private static void EnsureUnderDirectory(string root, string candidate, string label)
    {
        var fullRoot = Path.GetFullPath(root);
        var rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidate);
        if (
            !fullCandidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException($"Pack entry '{label}' escapes the template folder.");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of staging/backup dirs.
        }
    }

    [GeneratedRegex(
        @"^[A-Za-z0-9][A-Za-z0-9._/-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    )]
    private static partial Regex SafePackPath();
}
