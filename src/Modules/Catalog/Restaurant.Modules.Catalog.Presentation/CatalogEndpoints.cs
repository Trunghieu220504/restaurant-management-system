using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Catalog.Presentation;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/catalog")
            .WithTags("Catalog")
            .RequireAuthorization();
    }
}
