using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Presentation;

public static class FulfillmentEndpoints
{
    public static RouteGroupBuilder MapFulfillmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        var group = endpoints.MapGroup("/api/fulfillment")
            .WithTags("Fulfillment")
            .RequireAuthorization();
        group.MapGet("/snapshot", async (HttpContext http, IOperationsSnapshot snapshots, CancellationToken ct,
            string? station = null, long? orderId = null) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (orderId <= 0 || station is not null && !Stations.All.Contains(station))
                return Problem(FulfillmentErrors.InputInvalid);
            if (orderId.HasValue && !OperationsAccess.CanServe(http.User)) return Results.Forbid();
            var stations = station is null
                ? Stations.All.Where(x => OperationsAccess.CanReadStation(http.User, x)).ToArray()
                : OperationsAccess.CanReadStation(http.User, station) ? [station] : Array.Empty<string>();
            if (stations.Length == 0) return Results.Forbid();
            return Results.Ok(await snapshots.ReadAsync(stations, orderId, ct));
        }).WithName("FulfillmentSnapshot").Produces<OperationsSnapshot>().Produces(403).ProducesProblem(400);
        group.MapGet("/queue/{station}", async (string station, HttpContext http, IFulfillmentService service, CancellationToken ct) =>
        {
            if (!Stations.All.Contains(station)) return Results.Problem(statusCode: 400, title: FulfillmentErrors.InputInvalid);
            if (!CanUseStation(http, station)) return Results.Forbid();
            return
                Results.Ok(await service.QueueAsync(station, ct));
        }).WithName("FulfillmentQueue").Produces<IReadOnlyList<TicketView>>(200).Produces(403).ProducesProblem(400);
        MapCommand(group, "start", (service, id, request, actor, ct) => service.StartAsync(id, request.Station, request.Version, request.RequestKey, actor, ct));
        MapCommand(group, "ready", (service, id, request, actor, ct) => service.ReadyAsync(id, request.Station, request.Version, request.RequestKey, actor, ct));
        MapCommand(group, "deliver", (service, id, request, actor, ct) => service.DeliverAsync(id, request.Station, request.Version, request.RequestKey, actor, ct));
        MapCommand(group, "cancel", (service, id, request, actor, ct) => service.CancelAsync(id, request.Station, request.Version, request.RequestKey, actor, ct));
        group.MapIncidentEndpoints();
        return group;
    }

    private static void MapCommand(RouteGroupBuilder group, string action,
        Func<IFulfillmentService, long, TicketCommand, Guid, CancellationToken, Task<FulfillmentResult<TicketView>>> command)
    {
        group.MapPost($"/tickets/{{id:long}}/{action}", async (long id, TicketCommand body, HttpContext http, IFulfillmentService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            if (body.RequestKey == Guid.Empty) return Problem(FulfillmentErrors.IdempotencyRequired);
            if (!Stations.All.Contains(body.Station)) return Results.Problem(statusCode: 400, title: FulfillmentErrors.InputInvalid);
            if (!CanUseStation(http, body.Station)) return Results.Forbid();
            var result = await command(service, id, body, actor, ct);
            return result.Succeeded ? Results.Ok(result.Value) : Problem(result.Error!);
        }).WithName($"Fulfillment{action}").Produces<TicketView>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }

    private static bool CanUseStation(HttpContext http, string station) =>
        station == Stations.Kitchen && http.User.IsInRole("Kitchen") || station == Stations.Bar && http.User.IsInRole("Bar");

    private static IResult Problem(string error)
    {
        var status = error switch
        {
            FulfillmentErrors.TicketNotFound => 404,
            FulfillmentErrors.StationForbidden => 403,
            FulfillmentErrors.InvalidTransition or FulfillmentErrors.AlreadyProcessed or FulfillmentErrors.ConcurrencyConflict or FulfillmentErrors.IncidentPending => 409,
            "salesday.day.not_open" or "sales.order.not_open" or "sales.portion.cancelled" => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: error,
            extensions: new Dictionary<string, object?> { ["code"] = error });
    }
}
