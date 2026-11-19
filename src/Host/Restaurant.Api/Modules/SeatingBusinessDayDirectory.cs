using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.Seating.Application;

namespace Restaurant.Api.Modules;

/// <summary>
/// Composition-root adapter: lets Seating ask SalesDay about the open day without either module referencing
/// the other. Replace with the SalesDay contract (ISalesDayGate) once it is implemented.
/// </summary>
internal sealed class SeatingBusinessDayDirectory(IBusinessDays days) : IBusinessDayDirectory
{
    private const string OpenStatus = "DANG_BAN";

    public async Task<Guid?> CurrentOpenDayAsync(CancellationToken ct)
    {
        var day = await days.CurrentAsync(ct);
        return day is { Status: OpenStatus } ? day.Id : null;
    }

    public async Task<bool> IsOpenAsync(Guid dayId, CancellationToken ct) =>
        await CurrentOpenDayAsync(ct) == dayId;
}
