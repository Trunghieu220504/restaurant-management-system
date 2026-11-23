using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.Seating.Application;

namespace Restaurant.Api.Modules;

/// <summary>
/// Routes day-scoped operations to SalesDay using each caller's write transaction.
/// </summary>
internal sealed class BusinessDayDirectory(IBusinessDays days) : IBusinessDayDirectory,
    Restaurant.Modules.Inventory.Application.IInventoryDayGate, Restaurant.Modules.Billing.Application.IBillingDayGate, Restaurant.Modules.Catalog.Application.ICatalogBusinessDays,
    Restaurant.Modules.Sales.Application.IStaffPortionDay
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
