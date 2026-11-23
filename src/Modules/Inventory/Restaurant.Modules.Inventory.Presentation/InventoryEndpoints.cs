using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Modules.Inventory.Application;

namespace Restaurant.Modules.Inventory.Presentation;

public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization();
        var catalog = group.MapGroup("").RequireAuthorization(p => p.RequireClaim("permission", "inventory.catalog.manage"));
        catalog.MapGet("/catalog", (IInventory stock, CancellationToken ct) => stock.CatalogAsync(ct));
        catalog.MapPost("/units", async (UnitInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.UnitAsync(input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        catalog.MapPost("/ingredients", async (IngredientInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.IngredientAsync(null, input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        catalog.MapPut("/ingredients/{id:long}", async (long id, IngredientInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.IngredientAsync(id, input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        catalog.MapPut("/ingredients/{id:long}/conversions/{unit:long}", async (long id, long unit, ConversionInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.ConversionAsync(id, unit, input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        catalog.MapPost("/suppliers", async (SupplierInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.SupplierAsync(null, input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        catalog.MapPut("/suppliers/{id:long}", async (long id, SupplierInput input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.SupplierAsync(id, input, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        var purchases = group.MapGroup("/purchases").RequireAuthorization(p => p.RequireClaim("permission", "inventory.purchases.manage"));
        purchases.MapGet("", (Guid dayId, IInventory stock, CancellationToken ct) => stock.PurchasesAsync(dayId, ct));
        purchases.MapGet("/{id:long}", async (long id, IInventory stock, CancellationToken ct) =>
            await stock.PurchaseAsync(id, ct) is { } receipt ? Results.Ok(receipt) : Results.NotFound()).Produces<PurchaseView>();
        purchases.MapPost("", async (PurchaseInput input, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IInventory stock, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            if (!Guid.TryParse(requestKey, out var key) || key == Guid.Empty) return Reply(new(null, "inventory.idempotency.required"));
            return Reply(await stock.DraftAsync(input, actor, key, ct));
        }).Produces<StockResult>().ProducesProblem(400).ProducesProblem(409);
        purchases.MapPost("/{id:long}/post", async (long id, PurchaseTransition input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.TransitionAsync(id, input.DayId, true, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        purchases.MapPost("/{id:long}/cancel", async (long id, PurchaseTransition input, IInventory stock, CancellationToken ct) =>
            Reply(await stock.TransitionAsync(id, input.DayId, false, ct))).Produces<StockResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        var counts = group.MapGroup("/days/{day:guid}/counts");
        counts.MapGet("", async (Guid day, HttpContext http, IStockCounts service, CancellationToken ct) =>
        {
            var list = await service.ListAsync(day, ct);
            return http.User.HasClaim("permission", "inventory.counts.approve") ? list :
                list.Select(row => row with { OpeningValue = null, FrozenCost = null }).ToArray();
        }).Produces<IReadOnlyList<StockCountView>>()
            .RequireAuthorization(p => p.RequireClaim("permission", "inventory.counts.enter", "inventory.counts.approve"));
        counts.MapPut("/{ingredient:long}", async (Guid day, long ingredient, DraftCount input, HttpContext http, IStockCounts service, CancellationToken ct) =>
            Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)
                ? Reply(await service.SaveAsync(day, ingredient, input.Quantity, input.Version, false, actor, ct)) : Results.Unauthorized())
            .RequireAuthorization(p => p.RequireClaim("permission", "inventory.counts.enter")).Produces<StockResult>();
        counts.MapPost("/{ingredient:long}/approve", async (Guid day, long ingredient, ApproveCount input, HttpContext http, IStockCounts service, CancellationToken ct) =>
            Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)
                ? Reply(await service.SaveAsync(day, ingredient, null, input.Version, true, actor, ct)) : Results.Unauthorized())
            .RequireAuthorization(p => p.RequireClaim("permission", "inventory.counts.approve")).Produces<StockResult>();
        return group;
    }

    private static IResult Reply(StockResult result) => result.Error is null ? Results.Ok(result) :
        Results.Problem(title: result.Error, statusCode: result.Error switch
        {
            "inventory.reference.not_found" => 404,
            "inventory.idempotency.required" or "inventory.input.invalid" => 400,
            _ => 409
        }, extensions: new Dictionary<string, object?> { ["code"] = result.Error });
}
