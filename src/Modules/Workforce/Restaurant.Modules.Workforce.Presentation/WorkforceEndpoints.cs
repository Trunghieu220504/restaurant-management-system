using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Workforce.Presentation;

public static class WorkforceEndpoints
{
    public static RouteGroupBuilder MapWorkforceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/workforce")
            .WithTags("Workforce")
            .RequireAuthorization();
    }
}
