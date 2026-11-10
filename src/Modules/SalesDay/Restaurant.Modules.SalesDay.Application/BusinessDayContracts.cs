namespace Restaurant.Modules.SalesDay.Application;

public sealed record OpenDayRequest(DateOnly BusinessDate, long OpeningCash);
public sealed record BusinessDayResponse(Guid Id, DateOnly BusinessDate, long OpeningCash, string Status, DateTimeOffset OpenedAt);
public sealed record OpenDayResult(BusinessDayResponse? Day, string? Error, bool Replayed = false);
public interface IBusinessDays
{
    Task<BusinessDayResponse?> CurrentAsync(CancellationToken ct);
    Task<OpenDayResult> OpenAsync(OpenDayRequest request, Guid actor, Guid key, CancellationToken ct);
}
