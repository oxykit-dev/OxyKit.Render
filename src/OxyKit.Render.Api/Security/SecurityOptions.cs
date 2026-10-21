namespace OxyKit.Render.Api.Security;

internal sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public bool RequireLocalRequests { get; init; } = true;
    public string ApiKeyHeader { get; init; } = "X-OxyKit-Render-Key";
    public string ApiKey { get; init; } = string.Empty;
}
