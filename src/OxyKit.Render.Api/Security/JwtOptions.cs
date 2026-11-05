namespace OxyKit.Render.Api.Security;

/// <summary>
/// Optional Mail-issued JWT validation (same Issuer/Audience/Key as OxyKit.Mail Jwt:*).
/// Bound under <c>Security:Jwt</c>; when Key is empty, only API-key auth is accepted.
/// </summary>
internal sealed class JwtOptions
{
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Key)
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience);
}
