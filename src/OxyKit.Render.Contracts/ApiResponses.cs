namespace OxyKit.Render.Contracts;

/// <param name="Status">Liveness marker (<c>ok</c> when the process is up).</param>
/// <param name="Version">OxyKit.Render.Core informational version (e.g. <c>0.1.0</c>).</param>
/// <param name="Fingerprint">SHA-256 hex of output-affecting dependency pins + print baseline.</param>
public sealed record HealthResponse(string Status, string Version, string Fingerprint);

public sealed record FormatsResponse(IReadOnlyCollection<string> Formats);

public sealed record TemplateListItem(string Id, bool HasContract);

public sealed record TemplatesResponse(IReadOnlyList<TemplateListItem> Templates);
