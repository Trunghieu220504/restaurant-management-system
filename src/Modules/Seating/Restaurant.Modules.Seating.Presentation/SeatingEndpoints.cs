using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Seating.Application;

namespace Restaurant.Modules.Seating.Presentation;

public static class SeatingEndpoints
{
    private const string CatalogPermission = "seating.catalog.manage";
    private const string SessionsPermission = "seating.sessions.manage";

    public static RouteGroupBuilder MapSeatingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/seating").WithTags("Seating").RequireAuthorization();

        var areas = group.MapGroup("/areas")
            .RequireAuthorization(policy => policy.RequireClaim("permission", CatalogPermission));
        areas.MapGet("/", async (ISeatingCatalog catalog, CancellationToken ct) =>
                Results.Ok(await catalog.ListAreasAsync(ct)))
            .WithName("SeatingListAreas").Produces<IReadOnlyList<AreaDto>>(200);
        areas.MapPost("/", async (CreateAreaRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.CreateAreaAsync(request, ct), created: value => $"/api/seating/areas/{value.Id}"))
            .WithName("SeatingCreateArea").Produces<AreaDto>(201).ProducesProblem(400).ProducesProblem(409);
        areas.MapPut("/{id:long}", async (long id, UpdateAreaRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.UpdateAreaAsync(id, request, ct)))
            .WithName("SeatingUpdateArea").Produces<AreaDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        areas.MapPost("/{id:long}/archive", async (long id, ArchiveRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.ArchiveAreaAsync(id, request, ct)))
            .WithName("SeatingArchiveArea").Produces<AreaDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        var tables = group.MapGroup("/tables")
            .RequireAuthorization(policy => policy.RequireClaim("permission", CatalogPermission));
        tables.MapGet("/", async (ISeatingCatalog catalog, CancellationToken ct) =>
                Results.Ok(await catalog.ListTablesAsync(ct)))
            .WithName("SeatingListTables").Produces<IReadOnlyList<TableDto>>(200);
        tables.MapPost("/", async (CreateTableRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.CreateTableAsync(request, ct), created: value => $"/api/seating/tables/{value.Id}"))
            .WithName("SeatingCreateTable").Produces<TableDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        tables.MapPut("/{id:long}", async (long id, UpdateTableRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.UpdateTableAsync(id, request, ct)))
            .WithName("SeatingUpdateTable").Produces<TableDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        tables.MapPost("/{id:long}/archive", async (long id, ArchiveRequest request, ISeatingCatalog catalog, CancellationToken ct) =>
                Respond(await catalog.ArchiveTableAsync(id, request, ct)))
            .WithName("SeatingArchiveTable").Produces<TableDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        var sessionsPolicy = new Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder>(
            policy => policy.RequireClaim("permission", SessionsPermission));
        group.MapGet("/board", async (ISeatingCatalog catalog, CancellationToken ct) =>
                Results.Ok(await catalog.BoardAsync(ct)))
            .RequireAuthorization(sessionsPolicy)
            .WithName("SeatingBoard").Produces<IReadOnlyList<BoardAreaDto>>(200)
            .WithDescription("Free and in-use tables of active areas. Requires seating.sessions.manage.");

        group.MapPost("/tables/{tableId:long}/sessions", async (
                long tableId, HttpContext http, ISeatingSessions sessions, CancellationToken ct) =>
            {
                if (!TryActor(http, out var actor)) return Results.Unauthorized();
                if (!TryKey(http, out var key)) return Problem(SeatingErrors.IdempotencyRequired);
                return Respond(await sessions.OpenAsync(tableId, actor, key, ct),
                    created: value => $"/api/seating/sessions/{value.Id}");
            })
            .RequireAuthorization(sessionsPolicy)
            .WithName("SeatingOpenSession").Produces<SessionDto>(201).Produces<SessionDto>(200)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).Produces(401).Produces(403)
            .WithDescription("Requires seating.sessions.manage and an Idempotency-Key UUID header. The same key and payload replays the first result; a different payload returns 409.");

        group.MapPost("/sessions/{sessionId:long}/transfer", async (
                long sessionId, TransferRequest request, HttpContext http, ISeatingSessions sessions, CancellationToken ct) =>
            {
                if (!TryActor(http, out var actor)) return Results.Unauthorized();
                if (!TryKey(http, out var key)) return Problem(SeatingErrors.IdempotencyRequired);
                return Respond(await sessions.TransferAsync(sessionId, request, actor, key, ct));
            })
            .RequireAuthorization(sessionsPolicy)
            .WithName("SeatingTransferSession").Produces<SessionDto>(200)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).Produces(401).Produces(403)
            .WithDescription("Moves the session to a free table, keeping guest access. Requires seating.sessions.manage and an Idempotency-Key UUID header.");

        group.MapPost("/sessions/{sessionId:long}/close", async (
                long sessionId, HttpContext http, ISeatingSessions sessions, CancellationToken ct) =>
            {
                if (!TryActor(http, out var actor)) return Results.Unauthorized();
                if (!TryKey(http, out var key)) return Problem(SeatingErrors.IdempotencyRequired);
                return Respond(await sessions.CloseAsync(sessionId, actor, key, ct));
            })
            .RequireAuthorization(sessionsPolicy)
            .WithName("SeatingCloseSession").Produces<SessionDto>(200)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).Produces(401).Produces(403)
            .WithDescription("Closes the session and revokes every guest access. Requires seating.sessions.manage and an Idempotency-Key UUID header.");

        return group;
    }

    private static bool TryActor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirst("sub")?.Value, out actor) && actor != Guid.Empty;

    private static bool TryKey(HttpContext http, out Guid key) =>
        Guid.TryParse(http.Request.Headers["Idempotency-Key"], out key) && key != Guid.Empty;

    private static IResult Respond<T>(SeatingResult<T> result, Func<T, string>? created = null)
    {
        if (result.Error is not null) return Problem(result.Error);
        if (created is not null && !result.Replayed) return Results.Created(created(result.Value!), result.Value);
        return Results.Ok(result.Value);
    }

    private static int StatusOf(string code) => code switch
    {
        SeatingErrors.InputInvalid or SeatingErrors.IdempotencyRequired or SeatingErrors.TransferSameTable => 400,
        _ when code.EndsWith(".not_found", StringComparison.Ordinal) => 404,
        _ => 409
    };

    private static IResult Problem(string code) => Results.Problem(statusCode: StatusOf(code), title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
