using System.Security.Claims;
using Microsoft.Extensions.Options;
using OxyKit.Render.Contracts;

namespace OxyKit.Render.Api.Security;

internal sealed class InternalAccessMiddleware(
    RequestDelegate next,
    IOptions<SecurityOptions> options
)
{
    private readonly SecurityOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (AnonymousPaths.Matches(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (_options.RequireLocalRequests && !IsLocalRequest(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("Only local requests are allowed.")
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("Server API key is not configured.")
            );
            return;
        }

        ClaimsPrincipal? user = null;
        var hasApiKey = context.Request.Headers.TryGetValue(
            _options.ApiKeyHeader,
            out var providedApiKey
        );
        var providedKey = hasApiKey ? providedApiKey.ToString() : string.Empty;

        if (hasApiKey && !string.IsNullOrWhiteSpace(providedKey))
        {
            // A key header is an explicit machine override. A mismatch must not fall through to Bearer.
            if (!ApiKeysMatch(providedKey, _options.ApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid API key or Bearer token.")
                );
                return;
            }

            user = CreateApiKeyPrincipal();
        }
        else if (
            TryGetBearerToken(context.Request, out var bearer)
            && _options.Jwt.IsConfigured
            && JwtTokenValidator.TryValidate(bearer, _options.Jwt, out var jwtUser, out _)
            && jwtUser is not null
        )
        {
            user = AugmentBearerPrincipal(jwtUser);
        }

        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("Invalid API key or Bearer token.")
            );
            return;
        }

        context.User = user;

        if (!Authorize(context.Request, user))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("Insufficient permissions for this resource.")
            );
            return;
        }

        await next(context);
    }

    private static bool Authorize(HttpRequest request, ClaimsPrincipal user)
    {
        if (RenderAccess.IsTemplateWrite(request))
            return RenderAccess.CanWriteTemplates(user);

        if (RenderAccess.IsTemplateRead(request))
            return RenderAccess.CanReadTemplates(user);

        return RenderAccess.CanRender(user);
    }

    private static ClaimsPrincipal CreateApiKeyPrincipal()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "render-api-key"),
                new Claim(RenderAuthClaims.AuthMethod, RenderAuthClaims.ApiKeyMethod),
                new Claim(ClaimTypes.Role, RenderAuthClaims.RoleAdmin),
            ],
            authenticationType: RenderAuthClaims.ApiKeyMethod
        );
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal AugmentBearerPrincipal(ClaimsPrincipal jwtUser)
    {
        var identity = (ClaimsIdentity)jwtUser.Identity!;
        if (!identity.HasClaim(c => c.Type == RenderAuthClaims.AuthMethod))
            identity.AddClaim(
                new Claim(RenderAuthClaims.AuthMethod, RenderAuthClaims.BearerMethod)
            );
        return jwtUser;
    }

    private static bool TryGetBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;
        if (!request.Headers.TryGetValue("Authorization", out var header))
            return false;

        var value = header.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        token = value[prefix.Length..].Trim();
        return token.Length > 0;
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        var localIp = context.Connection.LocalIpAddress;

        if (remoteIp is null)
        {
            return false;
        }

        return System.Net.IPAddress.IsLoopback(remoteIp)
            || (localIp is not null && remoteIp.Equals(localIp));
    }

    private static bool ApiKeysMatch(string provided, string expected)
    {
        // Hash first so FixedTimeEquals always compares equal-length digests (no length oracle).
        var providedHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(provided)
        );
        var expectedHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(expected)
        );
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            providedHash,
            expectedHash
        );
    }
}
