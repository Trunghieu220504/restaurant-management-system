using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Identity.Presentation;

public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/identity")
            .WithTags("Identity")
            .RequireAuthorization();
    }
}
