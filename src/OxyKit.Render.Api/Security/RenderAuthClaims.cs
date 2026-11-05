namespace OxyKit.Render.Api.Security;

internal static class RenderAuthClaims
{
    public const string AuthMethod = "oxykit_auth";
    public const string ApiKeyMethod = "api_key";
    public const string BearerMethod = "bearer";

    public const string RoleAdmin = "Admin";
    public const string RoleUser = "User";
    public const string RoleSender = "Sender";
}
