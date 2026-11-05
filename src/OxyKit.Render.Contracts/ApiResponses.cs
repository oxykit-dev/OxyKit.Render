namespace OxyKit.Render.Contracts;

/// <param name="Status">Liveness marker (<c>ok</c> when the process is up).</param>
/// <param name="Version">OxyKit.Render.Core informational version (e.g. <c>0.1.0</c>).</param>
/// <param name="Fingerprint">SHA-256 hex of output-affecting dependency pins + print baseline.</param>
public sealed record HealthResponse(string Status, string Version, string Fingerprint);

public sealed record FormatsResponse(IReadOnlyCollection<string> Formats);

public sealed record TemplateListItem(string Id, bool HasContract);

public sealed record TemplatesResponse(IReadOnlyList<TemplateListItem> Templates);

/// <param name="Id">Template folder id under TemplatesRoot.</param>
/// <param name="HasContract">Whether <c>contract.json</c> is present (required for render).</param>
/// <param name="Files">Relative pack paths (template.html, contract.json, optional assets/partials/sample-data).</param>
public sealed record TemplateDetailResponse(string Id, bool HasContract, IReadOnlyList<string> Files);

/// <param name="TemplateId">Pack id.</param>
/// <param name="Path">Pack-relative asset path for <c>asset</c> helper (e.g. <c>assets/logo.svg</c>).</param>
public sealed record TemplateAssetResponse(string TemplateId, string Path);
