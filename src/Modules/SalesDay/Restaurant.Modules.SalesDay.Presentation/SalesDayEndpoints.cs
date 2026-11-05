using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.SalesDay.Presentation;

public static class SalesDayEndpoints
{
    public static RouteGroupBuilder MapSalesDayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/salesday")
            .WithTags("SalesDay")
            .RequireAuthorization();
    }
}
