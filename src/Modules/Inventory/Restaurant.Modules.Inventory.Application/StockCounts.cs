using Restaurant.Modules.Inventory.Domain;

namespace Restaurant.Modules.Inventory.Application;

public sealed record StockCountView(long IngredientId, string Name, long UnitId, decimal OpeningQuantity, decimal? OpeningValue,
    decimal? Draft, decimal? Approved, byte[]? Version)
{
    public StockCost? FrozenCost { get; init; }
}
public sealed record DraftCount(decimal Quantity, byte[]? Version);
public sealed record ApproveCount(byte[] Version);
public interface IStockCounts
{
    Task<IReadOnlyList<StockCountView>> ListAsync(Guid day, CancellationToken ct);
    Task<StockResult> SaveAsync(Guid day, long ingredient, decimal? draft, byte[]? version, bool approve, Guid actor, CancellationToken ct);
}
