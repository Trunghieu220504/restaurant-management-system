using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;

namespace Restaurant.Modules.Sales.Presentation;

public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        var group = endpoints.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization();
        var orders = group.MapGroup("/orders").RequireAuthorization(p => p.RequireClaim("permission", "sales.orders.manage"));
        orders.MapGet("/", async ([FromServices] IOrderQueries queries, CancellationToken ct, int page = 0, int pageSize = 50) =>
            page is < 0 or > 10000 || pageSize is < 1 or > 100
                ? Results.Problem(statusCode: 400, title: SalesErrors.InputInvalid)
                : Results.Ok(await queries.ReadOpenAsync(page, pageSize, ct)))
            .WithName("SalesOpenOrders").Produces<OpenOrdersPage>().ProducesProblem(400);
        orders.MapPost("/", async (CreateOrderCommand request, HttpContext http, [FromServices] IOrderCommands service, CancellationToken ct) =>
        {
            return TryActor(http, out var actor)
                ? Reply(await service.CreateOrderAsync(request with { Actor = actor }, ct), 201, value => $"/api/sales/orders/{value.Id}")
                : Results.Unauthorized();
        }).WithName("SalesCreateOrder").Produces<OrderView>(201).ProducesProblem(400).ProducesProblem(409);
        orders.MapPost("/{orderId:long}/batches", async (long orderId, AddBatchCommand request, HttpContext http,
            [FromHeader(Name = "Idempotency-Key")] string? requestKey, [FromServices] IOrderCommands service, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Unauthorized();
            if (!Guid.TryParse(requestKey, out var key) || key == Guid.Empty)
                return Results.Problem(statusCode: 400, title: "sales.idempotency.required",
                    extensions: new Dictionary<string, object?> { ["code"] = "sales.idempotency.required" });
            if (request.Source == BatchSource.Guest)
                return Reply(ServiceResult<BatchView>.Fail(SalesErrors.InputInvalid), 201);
            var source = request with { OrderId = orderId, StaffId = actor, GuestGrantId = null, RequestKey = key };
            return Reply(await service.AddBatchAsync(source, ct), 201, value => $"/api/sales/orders/{value.OrderId}/batches/{value.Number}");
        }).WithName("SalesAddBatch").WithDescription("Requires a UUID Idempotency-Key header; repeated requests replay the original batch.")
            .Produces<BatchView>(201).Produces<BatchView>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        orders.MapPost("/{orderId:long}/batches/{batchNumber:int}/confirm", async (long orderId, int batchNumber, HttpContext http,
            [FromServices] IOrderCommands service, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Unauthorized();
            return Reply(await service.ConfirmBatchAsync(new ConfirmBatchCommand(orderId, batchNumber, actor), ct), 200);
        }).RequireAuthorization(p => p.RequireClaim("permission", "sales.batches.confirm"))
            .WithName("SalesConfirmBatch").Produces<BatchView>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        orders.MapPost("/{orderId:long}/batches/{batchNumber:int}/reject", async (long orderId, int batchNumber, RejectBatchRequest request,
            HttpContext http, [FromServices] IOrderCommands service, CancellationToken ct) =>
        {
            if (!TryActor(http, out var actor)) return Results.Unauthorized();
            return Reply(await service.RejectBatchAsync(new RejectBatchCommand(orderId, batchNumber, actor, request.Reason), ct), 200);
        }).RequireAuthorization(p => p.RequireClaim("permission", "sales.batches.confirm"))
            .WithName("SalesRejectBatch").Produces<BatchView>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        orders.MapPost("/{orderId:long}/batches/{batchNumber:int}/cancel", async (long orderId, int batchNumber, [FromServices] IOrderCommands service, CancellationToken ct) =>
            Reply(await service.CancelBatchAsync(new CancelBatchCommand(orderId, batchNumber), ct), 200))
            .WithName("SalesCancelBatch").Produces<BatchView>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        var portions = group.MapGroup("/portions").RequireAuthorization(p => p.RequireClaim("permission", "sales.orders.manage"));
        portions.MapGet("/", async (IStaffPortions service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
            .WithName("SalesStaffPortions").Produces<IReadOnlyList<StaffPortion>>();
        portions.MapPut("/{id:long}/note", (long id, PortionChange input, HttpContext http, IStaffPortions service, CancellationToken ct) =>
            Change(id, input, false, http, service, ct)).WithName("SalesEditPortionNote").Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        portions.MapPost("/{id:long}/cancel", (long id, PortionChange input, HttpContext http, IStaffPortions service, CancellationToken ct) =>
            Change(id, input, true, http, service, ct)).WithName("SalesCancelStaffPortion").Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return group;
    }

    private static bool TryActor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirst("sub")?.Value, out actor) && actor != Guid.Empty;

    private sealed record RejectBatchRequest(string? Reason);

    private static IResult Reply<T>(ServiceResult<T> result, int successStatus, Func<T, string>? location = null) where T : class
    {
        if (result.Succeeded)
            return successStatus == 201 && !result.Replayed && location is not null ? Results.Created(location(result.Value!), result.Value) : Results.Ok(result.Value);
        var status = result.Error switch
        {
            SalesErrors.OrderNotFound or SalesErrors.BatchNotFound => 404,
            SalesErrors.OrderNotOpen or SalesErrors.BatchNotPending or SalesErrors.OrderSessionHasOpenOrder or SalesErrors.OrderChanged => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: result.Error,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error, ["lineErrors"] = result.LineErrors });
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
