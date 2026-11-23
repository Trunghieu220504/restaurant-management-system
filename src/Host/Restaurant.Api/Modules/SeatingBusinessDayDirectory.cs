using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.Seating.Application;

namespace Restaurant.Api.Modules;

/// <summary>
/// Composition-root adapter: lets Seating ask SalesDay about the open day without either module referencing
/// the other. Session writes use the same transaction for the day gate and the table change.
/// </summary>
internal sealed class SeatingBusinessDayDirectory(IBusinessDays days) : IBusinessDayDirectory,
    Restaurant.Modules.Catalog.Application.ICatalogBusinessDays, Restaurant.Modules.Sales.Application.IStaffPortionDay
{
    private const string OpenStatus = "DANG_BAN";

    public async Task<Guid?> CurrentOpenDayAsync(CancellationToken ct)
    {
        var day = await days.CurrentAsync(ct);
        return day is { Status: OpenStatus } ? day.Id : null;
    }

    public async Task<bool> RequireOpenAsync(System.Data.Common.DbTransaction transaction, Guid dayId, CancellationToken ct) =>
        (await new SqlSalesDayGate(() => transaction).RequireOpenAsync(dayId, ct)).IsOpen;
}
