using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Sales.Presentation;

public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization();
    }
}
