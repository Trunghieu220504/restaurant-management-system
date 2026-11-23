using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Sales.Application;

namespace Restaurant.Modules.Sales.Presentation;

public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        var group = endpoints.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization();
        var portions = group.MapGroup("/portions").RequireAuthorization(p => p.RequireClaim("permission", "sales.orders.manage"));
        portions.MapGet("/", async (IStaffPortions service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
            .WithName("SalesStaffPortions").Produces<IReadOnlyList<StaffPortion>>();
        portions.MapPut("/{id:long}/note", (long id, PortionChange input, HttpContext http, IStaffPortions service, CancellationToken ct) =>
            Change(id, input, false, http, service, ct)).WithName("SalesEditPortionNote").Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        portions.MapPost("/{id:long}/cancel", (long id, PortionChange input, HttpContext http, IStaffPortions service, CancellationToken ct) =>
            Change(id, input, true, http, service, ct)).WithName("SalesCancelStaffPortion").Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return group;
    }

    private static async Task<IResult> Change(long id, PortionChange input, bool cancel, HttpContext http, IStaffPortions service, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
        var error = await service.ChangeAsync(id, input, cancel, actor, ct);
        return error is null ? Results.NoContent() : Results.Problem(title: error,
            statusCode: error == "sales.input.invalid" ? 400 : error == "sales.portion.not_found" ? 404 : 409,
            extensions: new Dictionary<string, object?> { ["code"] = error });
    }
}
