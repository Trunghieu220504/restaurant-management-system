using Restaurant.Modules.SalesDay.Contracts;

namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>In-memory <see cref="ISalesDayGate"/>. Unknown days are "not found"; a day is open until the test changes it.</summary>
public sealed class FakeSalesDayGate(CallLog? log = null) : ISalesDayGate
{
    private readonly Dictionary<Guid, string> days = [];

    public FakeSalesDayGate AddDay(Guid id, string status = SalesDayStatuses.Open)
    {
        days[id] = status;
        return this;
    }

    public void SetStatus(Guid id, string status) => days[id] = status;

    public Task<SalesDayGateResult> RequireOpenAsync(Guid businessDayId, CancellationToken ct)
    {
        log?.Add($"day.gate:{businessDayId}");
        if (!days.TryGetValue(businessDayId, out var status))
            return Task.FromResult(new SalesDayGateResult(null, SalesDayErrorCodes.DayNotFound));
        return Task.FromResult(status == SalesDayStatuses.Open
            ? new SalesDayGateResult(new OpenBusinessDay(businessDayId, new DateOnly(2026, 11, 20)), null)
            : new SalesDayGateResult(null, SalesDayErrorCodes.DayNotOpen));
    }
}
