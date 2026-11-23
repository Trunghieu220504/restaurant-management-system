using System.Data.Common;
using Restaurant.Modules.Inventory.Domain;

namespace Restaurant.Modules.Inventory.Application;

public interface IInventoryDayGate
{
    Task<bool> RequireOpenAsync(DbTransaction transaction, Guid dayId, CancellationToken ct);
}

public sealed record UnitInput(string Code, string Name, string Dimension);
public sealed record IngredientInput(Guid DayId, string Code, string Name, long BaseUnitId, string Mode, bool Archived);
public sealed record ConversionInput(Guid DayId, decimal Factor);
public sealed record SupplierInput(string Name, string? Phone, string? Address, bool Active);
public sealed record PurchaseLineInput(long IngredientId, long UnitId, decimal Quantity, decimal UnitPrice);
public sealed record PurchaseInput(Guid DayId, long? SupplierId, bool CashDrawer, string? Note, IReadOnlyList<PurchaseLineInput> Lines);
public sealed record PurchaseTransition(Guid DayId);
public sealed record StockResult(long? Id, string? Error = null);
public sealed record StockCatalog(IReadOnlyList<DonVi> Units, IReadOnlyList<NguyenLieu> Ingredients,
    IReadOnlyList<QuyDoi> Conversions, IReadOnlyList<NhaCungCap> Suppliers);
public sealed record PurchaseHeader(long Id, Guid DayId, string Status, bool CashDrawer, long? SupplierId,
    string? Note, DateTimeOffset CreatedAt, Guid Actor);
public sealed record PurchaseView(PurchaseHeader Header, IReadOnlyList<ChiTietPhieuNhap> Lines, decimal Total);

public interface IInventory
{
    Task<StockCatalog> CatalogAsync(CancellationToken ct);
    Task<PurchaseView?> PurchaseAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<PurchaseHeader>> PurchasesAsync(Guid dayId, CancellationToken ct);
    Task<StockResult> UnitAsync(UnitInput input, CancellationToken ct);
    Task<StockResult> IngredientAsync(long? id, IngredientInput input, CancellationToken ct);
    Task<StockResult> ConversionAsync(long ingredient, long unit, ConversionInput input, CancellationToken ct);
    Task<StockResult> SupplierAsync(long? id, SupplierInput input, CancellationToken ct);
    Task<StockResult> DraftAsync(PurchaseInput input, Guid actor, Guid key, CancellationToken ct);
    Task<StockResult> TransitionAsync(long id, Guid day, bool post, CancellationToken ct);
}
