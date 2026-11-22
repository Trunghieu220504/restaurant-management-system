namespace Restaurant.Modules.Seating.Application;

public sealed record GuestExchange(string TableCode, string JoinCode);
public sealed record GuestInvite(string TableCode, string JoinCode, DateTimeOffset ExpiresAt);
public sealed record GuestToken(string Token, DateTimeOffset ExpiresAt);
public sealed record GuestSession(string TableName, DateTimeOffset ExpiresAt);
public interface IGuestAccess
{
    Task<SeatingResult<GuestInvite>> InviteAsync(long session, Guid actor, CancellationToken ct);
    Task<SeatingResult<GuestToken>> ExchangeAsync(GuestExchange request, CancellationToken ct);
    Task<SeatingResult<GuestSession>> CheckAsync(string token, CancellationToken ct);
}
