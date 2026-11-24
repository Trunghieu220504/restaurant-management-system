using System.Data.Common;

namespace Restaurant.Modules.Inventory.Application;

public sealed record StockAdjustment(Guid DayId, long IngredientId, decimal Quantity, decimal Value, string Reason, Guid? OriginalDayId = null);
public sealed record PurchaseCorrection(PurchaseInput Replacement, string Reason);
public interface IInventoryCorrections
{
    Task<StockResult> AdjustAsync(StockAdjustment input, Guid actor, Guid key, CancellationToken ct);
    Task<StockResult> CorrectAsync(long id, PurchaseCorrection input, Guid actor, Guid key, CancellationToken ct);
}

public interface IInventoryAudit
{
    Task AppendAsync(DbTransaction transaction, Guid actor, Guid day, string action, string entity, string id, string reason, CancellationToken ct);
}
