using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Presentation;

public static class IncidentEndpoints
{
    public static void MapIncidentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/incidents", async (HttpContext http, IFulfillmentIncidents service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var stations = Stations.All.Where(x => OperationsAccess.CanReadStation(http.User, x)).ToArray();
            return stations.Length == 0 ? Results.Forbid() : Results.Ok(await service.PendingAsync(stations, ct));
        }).RequireAuthorization(p => p.RequireClaim("permission", "fulfillment.incidents.report"))
            .WithName("FulfillmentIncidents").Produces<IReadOnlyList<IncidentView>>().Produces(403);
        group.MapPost("/tickets/{id:long}/incidents", async (long id, IncidentReport request, HttpContext http,
            IFulfillmentIncidents service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            if (!OperationsAccess.CanReadStation(http.User, request.Station)) return Results.Forbid();
            return Respond(await service.ReportAsync(id, request, actor, ct));
        }).RequireAuthorization(p => p.RequireClaim("permission", "fulfillment.incidents.report"))
            .WithName("FulfillmentReportIncident").Produces<IncidentView>().ProducesProblem(400).ProducesProblem(409).Produces(403);
        group.MapPost("/incidents/{id:long}/decision", async (long id, IncidentResolution request, HttpContext http,
            IFulfillmentIncidents service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            return Respond(await service.ResolveAsync(id, request, actor, ct));
        }).RequireAuthorization(p => p.RequireRole("Manager").RequireClaim("permission", "fulfillment.incidents.approve"))
            .WithName("FulfillmentDecideIncident").Produces<IncidentView>().ProducesProblem(400).ProducesProblem(409).Produces(403);
    }

    private static IResult Respond(FulfillmentResult<IncidentView> result)
    {
        if (result.Succeeded) return Results.Ok(result.Value);
        var status = result.Error switch
        {
            FulfillmentErrors.InputInvalid or FulfillmentErrors.IdempotencyRequired => 400,
            FulfillmentErrors.StationForbidden => 403,
            FulfillmentErrors.IncidentNotFound or FulfillmentErrors.TicketNotFound => 404,
            _ => 409
        };
        return Results.Problem(statusCode: status, title: result.Error,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error });
    }
}
