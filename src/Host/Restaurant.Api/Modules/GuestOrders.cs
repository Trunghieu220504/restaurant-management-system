using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Application;

namespace Restaurant.Api.Modules;

internal sealed class SessionOrderGuard(SalesDbContext sales) : ISessionClosureGuard
{
    public Task<bool> HasOpenOrdersAsync(long sessionId, CancellationToken ct) => sales.Orders.AsNoTracking()
        .AnyAsync(order => order.PhienSuDungId == sessionId && order.TrangThai == OrderStatus.Open, ct);
}

internal sealed class GuestOrderSessions(IGuestGrants grants, SalesDbContext sales) : IGuestOrderSessions
{
    public async Task<ServiceResult<GuestSalesSession>> FindAsync(string token, CancellationToken ct)
    {
        var access = await grants.ResolveAsync(token, ct);
        return access.Value is { } grant
            ? ServiceResult<GuestSalesSession>.Ok(new(grant.Id, grant.SessionId, grant.BusinessDayId, grant.TableName))
            : ServiceResult<GuestSalesSession>.Fail(access.Error!);
    }

    public Task<string?> LockAsync(string token, GuestSalesSession session, CancellationToken ct) =>
        grants.LockAsync(token, session.GrantId, session.SessionId,
            sales.Database.CurrentTransaction!.GetDbTransaction(), ct);
}

internal static class GuestOrders
{
    private const string Batches = "/api/public/guest-access/batches";
    private sealed record GuestBatchRequest(IReadOnlyList<BatchLine>? Lines);

    public static void MapGuestOrders(this IEndpointRouteBuilder endpoints)
    {
        var batches = endpoints.MapGroup(Batches).AllowAnonymous().WithTags("Guest orders");
        batches.MapPost("", async (GuestBatchRequest body, HttpContext http, [FromServices] GuestOrderService service,
            [FromHeader(Name = "Idempotency-Key")] string? requestKey, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (Token(http) is not { } token) return Problem("qr.token.missing", 401);
            Guid.TryParse(requestKey, out var key);
            var result = await service.SubmitAsync(token, key, body.Lines, ct);
            if (result.Value is { } receipt)
                return result.Replayed ? Results.Ok(receipt) : Results.Created($"{Batches}/{receipt.Id}", receipt);
            var status = result.Error switch
            {
                "qr.token.invalid" or "qr.token.expired" or "qr.token.revoked" => 401,
                SalesErrors.OrderNotOpen or SalesErrors.OrderChanged or SalesErrors.OrderSessionHasOpenOrder => 409,
                _ => 400
            };
            return Problem(result.Error!, status, result.LineErrors);
        }).RequireRateLimiting("qr-submit").WithName("GuestSubmitBatch")
            .Produces<GuestBatchReceipt>(201).Produces<GuestBatchReceipt>().ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(409).ProducesProblem(429);

        batches.MapGet("/{id:long}", async (long id, HttpContext http, [FromServices] IGuestOrderSessions sessions,
            [FromServices] SalesDbContext sales, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (Token(http) is not { } token) return Problem("qr.token.missing", 401);
            var access = await sessions.FindAsync(token, ct);
            if (access.Value is not { } session) return Problem(access.Error!, 401);
            var batch = await (from b in sales.Batches.AsNoTracking()
                               join order in sales.Orders on b.DonHangId equals order.Id
                               where b.Id == id && b.QuyenTruyCapKhachId == session.GrantId &&
                                   order.PhienSuDungId == session.SessionId
                               select new { b.Id, b.TrangThai }).SingleOrDefaultAsync(ct);
            return batch is null ? Problem(SalesErrors.BatchNotFound, 404) : Results.Ok(GuestBatchReceipt.From(batch.Id, batch.TrangThai));
        }).RequireRateLimiting("qr-read").WithName("GuestTrackBatch").Produces<GuestBatchReceipt>()
            .ProducesProblem(401).ProducesProblem(404).ProducesProblem(429);
    }

    private static string? Token(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Guest ", StringComparison.OrdinalIgnoreCase) ? header[6..] : null;
    }

    private static IResult Problem(string code, int status, IReadOnlyList<LineError>? lineErrors = null) =>
        Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["lineErrors"] = lineErrors ?? []
        });
}
