namespace Restaurant.Modules.Reporting.Application;

public interface IBusinessReports
{
    Task<IReadOnlyList<SoldItem>> ItemsAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<ClosingStock>> StockAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<BusinessReport> ReadAsync(DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record CashMovement(string Method, decimal Receipts, decimal Refunds, decimal Net);
public sealed record ReportDay(Guid DayId, DateOnly Date, decimal Charges, decimal Adjustments, decimal Waivers, decimal NetRevenue,
    decimal Receipts, decimal Refunds, decimal DrawerPurchases, decimal Cost, decimal GrossProfit, IReadOnlyList<CashMovement> Methods);
public sealed record SoldItem(string Name, int Portions, decimal Charges);
public sealed record ClosingStock(Guid DayId, long IngredientId, long? UnitId, decimal OpeningQuantity,
    decimal? ClosingQuantity, decimal? ClosingValue, decimal? Usage, decimal? Cost);
public sealed record BusinessReport(DateOnly From, DateOnly To, IReadOnlyList<ReportDay> Days, decimal NetRevenue, decimal Cost,
    decimal GrossProfit, decimal DrawerPurchases, IReadOnlyList<CashMovement> Methods, IReadOnlyList<SoldItem> Items, IReadOnlyList<ClosingStock> Stock);
