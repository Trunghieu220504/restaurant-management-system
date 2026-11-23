using Restaurant.Modules.SalesDay.Domain;

namespace Restaurant.Modules.SalesDay.Application;

public sealed record FinalizeDay(decimal CountedCash, string? DifferenceReason, bool LateClose, string? LateReason);
public sealed record CostLine(long IngredientId, long UnitId, decimal Usage, decimal Average, decimal ClosingValue, decimal Cost);
public sealed record SettlementPreview(decimal OpeningCash, decimal ExpectedCash, decimal DrawerPurchases, decimal NetRevenue,
    decimal? Cost, decimal? RoundingDifference, decimal? GrossProfit, IReadOnlyList<CostLine> Ingredients, IReadOnlyList<CloseBlocker> Blockers);
public sealed record SettlementResult(DaySnapshot? Snapshot, string? Error = null, IReadOnlyList<CloseBlocker>? Blockers = null);
public interface IDaySettlement
{
    Task<SettlementPreview?> PreviewAsync(Guid day, CancellationToken ct);
    Task<DaySnapshot?> SnapshotAsync(Guid day, CancellationToken ct);
    Task<IReadOnlyList<DaySnapshot>> HistoryAsync(int offset, int limit, CancellationToken ct);
    Task<SettlementResult> FinalizeAsync(Guid day, FinalizeDay input, Guid actor, Guid key, CancellationToken ct);
}
