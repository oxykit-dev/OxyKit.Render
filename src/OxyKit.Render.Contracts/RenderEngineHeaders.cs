namespace OxyKit.Render.Contracts;

/// <summary>
/// Response headers on successful render (and echoed on <c>/health</c> JSON).
/// Bytes stay in the body; provenance rides on headers so Local stays zero-retention.
/// </summary>
public static class RenderEngineHeaders
{
    public const string Version = "X-OxyKit-Render-Version";
    public const string Fingerprint = "X-OxyKit-Render-Fingerprint";
}
