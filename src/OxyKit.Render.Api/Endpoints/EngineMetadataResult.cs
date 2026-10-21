using OxyKit.Render.Contracts;
using OxyKit.Render.Core;

namespace OxyKit.Render.Api.Endpoints;

/// <summary>Wraps a file (or other) result and stamps engine version / fingerprint headers.</summary>
internal sealed class EngineMetadataResult(IResult inner, EngineIdentity identity) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers[RenderEngineHeaders.Version] = identity.Version;
        httpContext.Response.Headers[RenderEngineHeaders.Fingerprint] = identity.Fingerprint;
        return inner.ExecuteAsync(httpContext);
    }
}
