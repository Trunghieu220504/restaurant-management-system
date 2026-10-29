using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Fulfillment.Presentation;

public static class FulfillmentEndpoints
{
    public static RouteGroupBuilder MapFulfillmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/fulfillment")
            .WithTags("Fulfillment")
            .RequireAuthorization();
    }
}
