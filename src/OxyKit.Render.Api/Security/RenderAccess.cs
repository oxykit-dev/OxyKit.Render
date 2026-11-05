using System.Security.Claims;

namespace OxyKit.Render.Api.Security;

internal static class RenderAccess
{
    public static bool IsApiKey(ClaimsPrincipal user) =>
        user.HasClaim(RenderAuthClaims.AuthMethod, RenderAuthClaims.ApiKeyMethod);

    public static bool IsInRole(ClaimsPrincipal user, params string[] roles) =>
        roles.Any(role => user.IsInRole(role));

    /// <summary>Pack write (PUT/DELETE templates + assets): API key or Admin.</summary>
    public static bool CanWriteTemplates(ClaimsPrincipal user) =>
        IsApiKey(user) || user.IsInRole(RenderAuthClaims.RoleAdmin);

    /// <summary>Pack read (GET templates*): API key, Admin, or User.</summary>
    public static bool CanReadTemplates(ClaimsPrincipal user) =>
        IsApiKey(user)
        || IsInRole(user, RenderAuthClaims.RoleAdmin, RenderAuthClaims.RoleUser);

    /// <summary>Render / formats: API key or Admin|User|Sender.</summary>
    public static bool CanRender(ClaimsPrincipal user) =>
        IsApiKey(user)
        || IsInRole(
            user,
            RenderAuthClaims.RoleAdmin,
            RenderAuthClaims.RoleUser,
            RenderAuthClaims.RoleSender
        );

    public static bool IsTemplateWrite(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        var templates = path.Contains("/templates/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/templates", StringComparison.OrdinalIgnoreCase);
        if (!templates)
            return false;

        return HttpMethods.IsPut(request.Method)
            || HttpMethods.IsDelete(request.Method)
            || HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPatch(request.Method);
    }

    public static bool IsTemplateRead(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        if (
            !path.Contains("/templates", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith("/templates", StringComparison.OrdinalIgnoreCase)
        )
            return false;

        return HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
    }
}
