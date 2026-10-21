using OxyKit.Render.Contracts;
using OxyKit.Render.Core;

namespace OxyKit.Render.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var isV1 = endpoints is RouteGroupBuilder;

        endpoints
            .MapGet(
                "/health",
                (EngineIdentity identity) =>
                    Results.Ok(new HealthResponse("ok", identity.Version, identity.Fingerprint))
            )
            .WithName(isV1 ? "GetHealthV1" : "GetHealth")
            .WithSummary("Health check")
            .WithDescription(
                "Unauthenticated liveness probe. Returns status plus engine version and "
                    + "output-affecting dependency fingerprint (same values as render response headers)."
            )
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return endpoints;
    }
}
