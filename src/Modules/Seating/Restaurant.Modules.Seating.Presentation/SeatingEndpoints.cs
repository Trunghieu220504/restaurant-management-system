using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Seating.Presentation;

public static class SeatingEndpoints
{
    public static RouteGroupBuilder MapSeatingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/seating")
            .WithTags("Seating")
            .RequireAuthorization();
    }
}
