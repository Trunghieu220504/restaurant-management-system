using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Inventory.Presentation;

public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization();
    }
}
