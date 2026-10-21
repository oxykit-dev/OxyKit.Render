namespace OxyKit.Render.Contracts;

/// <summary>How a document entered OxyKit.Render Cloud.</summary>
public enum DocumentSource
{
    Rendered,
    Registered,
}

/// <summary>
/// Integrity metadata always returned for preserved/registered documents.
/// Byte snapshots are separate and opt-in.
/// </summary>
public sealed record DocumentRecord(
    string DocumentId,
    string ContentType,
    long Size,
    string Sha256,
    DateTimeOffset CreatedAt,
    DocumentSource Source,
    string? RendererVersion = null,
    string? TemplateSha256 = null,
    string? PayloadSha256 = null,
    bool ArtifactRetained = false,
    bool TemplateRetained = false,
    bool PayloadRetained = false
);

/// <summary>Opt-in byte retention flags. Metadata (hashes, size) is always recorded.</summary>
public sealed class RetentionOptions
{
    public bool RetainArtifact { get; init; }
    public bool RetainTemplate { get; init; }
    public bool RetainPayload { get; init; }
}

/// <summary>
/// Register an externally produced document. Prefer uploading bytes so OxyKit.Render
/// computes the hash; hash-asserted mode accepts client-supplied integrity fields.
/// </summary>
public sealed class RegisterDocumentRequest
{
    public string? ContentType { get; init; }
    public string? Sha256 { get; init; }
    public long? Size { get; init; }
    public RetentionOptions? Retention { get; init; }
}

public sealed record VerifyDocumentResponse(
    string DocumentId,
    string Sha256,
    bool Matches,
    string? Message = null
);
