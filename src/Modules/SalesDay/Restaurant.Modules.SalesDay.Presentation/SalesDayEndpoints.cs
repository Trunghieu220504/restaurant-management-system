using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Modules.SalesDay.Application;

namespace Restaurant.Modules.SalesDay.Presentation;

public static class SalesDayEndpoints
{
    public static RouteGroupBuilder MapSalesDayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/salesday").WithTags("SalesDay").RequireAuthorization();
        group.MapGet("/current", async (IBusinessDays days, CancellationToken ct) =>
            Results.Ok(await days.CurrentAsync(ct))).WithName("SalesDayCurrent").Produces<BusinessDayResponse>(200)
            .WithDescription("Returns the active business day or null. Requires authentication.");
        group.MapPost("/open", async (OpenDayRequest request, HttpContext http, IBusinessDays days, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
                return Problem("salesday.idempotency.required", 400);
            var result = await days.OpenAsync(request, actor, key, ct);
            if (result.Error is not null) return Problem(result.Error, result.Error == "salesday.input.invalid" ? 400 : 409);
            return result.Replayed ? Results.Ok(result.Day) : Results.Created($"/api/salesday/current", result.Day);
        }).RequireAuthorization(policy => policy.RequireClaim("permission", "salesday.manage"))
            .WithName("SalesDayOpen").Produces<BusinessDayResponse>(201).Produces<BusinessDayResponse>(200)
            .ProducesProblem(400).ProducesProblem(409).Produces(401).Produces(403)
            .WithDescription("Requires permission salesday.manage and an Idempotency-Key UUID header. Same key and payload replays the response; conflicting payload returns 409.");
        var closing = group.MapGroup("/{dayId:guid}").RequireAuthorization(p => p.RequireClaim("permission", "salesday.manage"));
        closing.MapGet("/close-checklist", async (Guid dayId, IDayClosing service, CancellationToken ct) =>
            Reply(await service.ChecklistAsync(dayId, ct))).Produces<CloseChecklist>();
        closing.MapPost("/begin-close", (Guid dayId, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IDayClosing service, CancellationToken ct) =>
            Change(dayId, false, http, requestKey, service, ct)).Produces<CloseChecklist>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        closing.MapPost("/cancel-close", (Guid dayId, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IDayClosing service, CancellationToken ct) =>
            Change(dayId, true, http, requestKey, service, ct)).Produces<CloseChecklist>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/history", async (int? offset, int? limit, IDaySettlement service, CancellationToken ct) =>
            offset is < 0 || limit is < 1 or > 100 ? Problem("salesday.input.invalid", 400) :
                Results.Ok(await service.HistoryAsync(offset ?? 0, limit ?? 30, ct)))
            .RequireAuthorization(p => p.RequireClaim("permission", "salesday.manage"))
            .Produces<IReadOnlyList<Restaurant.Modules.SalesDay.Domain.DaySnapshot>>();
        closing.MapGet("/settlement-preview", async (Guid dayId, IDaySettlement service, CancellationToken ct) =>
            await service.PreviewAsync(dayId, ct) is { } view ? Results.Ok(view) : Problem("salesday.active_day.not_found", 404))
            .Produces<SettlementPreview>();
        closing.MapGet("/snapshot", async (Guid dayId, IDaySettlement service, CancellationToken ct) =>
            await service.SnapshotAsync(dayId, ct) is { } view ? Results.Ok(view) : Problem("salesday.snapshot.not_found", 404))
            .Produces<Restaurant.Modules.SalesDay.Domain.DaySnapshot>();
        closing.MapPost("/finalize", async (Guid dayId, FinalizeDay input, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey,
            IDaySettlement service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
            if (!Guid.TryParse(requestKey, out var key) || key == Guid.Empty) return Problem("salesday.idempotency.required", 400);
            var result = await service.FinalizeAsync(dayId, input, actor, key, ct);
            if (result.Error == null) return Results.Ok(result.Snapshot);
            var status = result.Error switch { "salesday.day.not_found" => 404, "salesday.input.invalid" => 400, _ => 409 };
            return Results.Problem(title: result.Error, statusCode: status,
                extensions: new Dictionary<string, object?> { ["code"] = result.Error, ["blockers"] = result.Blockers });
        }).Produces<Restaurant.Modules.SalesDay.Domain.DaySnapshot>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return group;
    }
    private static async Task<IResult> Change(Guid dayId, bool cancel, HttpContext http, string? requestKey, IDayClosing service, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
        if (!Guid.TryParse(requestKey, out var key) || key == Guid.Empty)
            return Problem("salesday.idempotency.required", 400);
        return Reply(await service.ChangeAsync(dayId, cancel, actor, key, ct));
    }

    private static IResult Reply(ClosingResult result) => result.Error is null ? Results.Ok(result.Checklist) :
        Results.Problem(title: result.Error, statusCode: result.Error == "salesday.day.not_found" ? 404 : 409,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error, ["checklist"] = result.Checklist });

    private static IResult Problem(string code, int status) => Results.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
