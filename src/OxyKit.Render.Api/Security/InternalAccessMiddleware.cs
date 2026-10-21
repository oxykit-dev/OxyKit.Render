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

        if (
            !context.Request.Headers.TryGetValue(_options.ApiKeyHeader, out var providedApiKey)
            || !ApiKeysMatch(providedApiKey.ToString(), _options.ApiKey)
        )
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Invalid API key."));
            return;
        }

        await next(context);
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
