using System.Data.Common;

namespace Restaurant.Modules.Seating.Application;

public sealed record GuestExchange(string TableCode, string JoinCode);
public sealed record GuestInvite(string TableCode, string JoinCode, DateTimeOffset ExpiresAt);
public sealed record GuestToken(string Token, DateTimeOffset ExpiresAt);
public sealed record GuestSession(string TableName, DateTimeOffset ExpiresAt, bool CanSubmitOrders = false);
public sealed record GuestGrant(Guid Id, long SessionId, Guid BusinessDayId, string TableName, DateTimeOffset ExpiresAt);

public interface IGuestGrants
{
    Task<SeatingResult<GuestGrant>> ResolveAsync(string token, CancellationToken ct);
    Task<string?> LockAsync(string token, Guid grantId, long sessionId, DbTransaction transaction, CancellationToken ct);
}
public interface IGuestAccess
{
    Task<SeatingResult<GuestInvite>> InviteAsync(long session, Guid actor, CancellationToken ct);
    Task<SeatingResult<GuestToken>> ExchangeAsync(GuestExchange request, CancellationToken ct);
    Task<SeatingResult<GuestSession>> CheckAsync(string token, CancellationToken ct);
}
