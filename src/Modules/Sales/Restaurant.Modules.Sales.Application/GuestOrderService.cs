using System.Globalization;
using System.Security.Cryptography;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.SalesDay.Contracts;

namespace Restaurant.Modules.Sales.Application;

public sealed record GuestSalesSession(Guid GrantId, long SessionId, Guid BusinessDayId, string TableLabel);
public sealed record GuestBatchReceipt(string Id, string Status)
{
    public static GuestBatchReceipt From(long id, string status) => new(id.ToString(CultureInfo.InvariantCulture), status);
}

public interface IGuestOrderSessions
{
    Task<ServiceResult<GuestSalesSession>> FindAsync(string token, CancellationToken ct);
    Task<string?> LockAsync(string token, GuestSalesSession session, CancellationToken ct);
}

public sealed class GuestOrderService(IOrderStore store, ISalesDayGate days, IGuestOrderSessions sessions,
    IMenuPricing menu, TimeProvider clock)
{
    public async Task<ServiceResult<GuestBatchReceipt>> SubmitAsync(string token, Guid key,
        IReadOnlyList<BatchLine>? lines, CancellationToken ct)
    {
        var access = await sessions.FindAsync(token, ct);
        if (access.Value is not { } session) return ServiceResult<GuestBatchReceipt>.Fail(access.Error!);
        if (key == Guid.Empty || lines is not { Count: > 0 and <= 50 } ||
            lines.Any(line => line is null || line.OptionIds is { Count: > 20 }))
            return ServiceResult<GuestBatchReceipt>.Fail(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);
        var day = await days.RequireOpenAsync(session.BusinessDayId, ct);
        if (!day.IsOpen) return ServiceResult<GuestBatchReceipt>.Fail(day.ErrorCode ?? SalesDayErrorCodes.DayNotOpen);

        // Seating close holds the same update lock while checking for open orders and revoking grants.
        var error = await sessions.LockAsync(token, session, ct);
        if (error is not null) return ServiceResult<GuestBatchReceipt>.Fail(error);
        var scopedKey = ScopeKey(session.GrantId, key);
        if (await tx.FindGuestBatchAsync(session, scopedKey, ct) is { } earlier)
            return ServiceResult<GuestBatchReceipt>.Ok(earlier, replayed: true);

        var existing = await tx.FindOpenOrderIdBySessionAsync(session.SessionId, ct);
        DonHang order;
        if (existing is { } id)
        {
            var locked = await tx.LockOrderAsync(id, ct);
            if (locked is null || locked.TrangThai != OrderStatus.Open)
                return ServiceResult<GuestBatchReceipt>.Fail(SalesErrors.OrderNotOpen);
            order = locked;
        }
        else
        {
            var created = DonHang.Create(await tx.NextOrderCodeAsync(ct), OrderTypes.DineIn,
                session.BusinessDayId, session.SessionId, session.TableLabel, null, null, clock.GetUtcNow().UtcDateTime);
            if (!created.Succeeded) return ServiceResult<GuestBatchReceipt>.Fail(created.Error!);
            order = created.Value!;
        }

        var priced = await OrderService.PriceBatchAsync(menu, session.BusinessDayId, lines, ct);
        if (!priced.Succeeded) return ServiceResult<GuestBatchReceipt>.Fail(priced.Error!, priced.LineErrors);
        var added = order.AddBatch(BatchSource.Guest, session.GrantId, null, scopedKey, priced.Value,
            clock.GetUtcNow().UtcDateTime);
        if (added.Value is not { } batch) return ServiceResult<GuestBatchReceipt>.Fail(added.Error!);
        if (existing is null) tx.Add(order);
        return await OrderService.SaveAndCommitAsync(tx, () =>
            ServiceResult<GuestBatchReceipt>.Ok(GuestBatchReceipt.From(batch.Id, batch.TrangThai)), ct);
    }

    private static Guid ScopeKey(Guid grant, Guid requestKey)
    {
        Span<byte> input = stackalloc byte[32];
        grant.TryWriteBytes(input[..16]);
        requestKey.TryWriteBytes(input[16..]);
        return new Guid(SHA256.HashData(input).AsSpan(0, 16));
    }
}
