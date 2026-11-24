using Restaurant.Modules.Sales.Domain;

namespace Restaurant.Modules.Sales.Application;

/// <summary>Opens one unit of work for a Sales write. Infrastructure implements it with the module's DbContext.</summary>

public interface IOrderCommands
{
    Task<ServiceResult<OrderView>> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct);
    Task<ServiceResult<BatchView>> AddBatchAsync(AddBatchCommand command, CancellationToken ct);
    Task<ServiceResult<BatchView>> CancelBatchAsync(CancelBatchCommand command, CancellationToken ct);
    Task<ServiceResult<BatchView>> ConfirmBatchAsync(ConfirmBatchCommand command, CancellationToken ct);
    Task<ServiceResult<BatchView>> RejectBatchAsync(RejectBatchCommand command, CancellationToken ct);
}
public interface IOrderStore
{
    Task<IOrderTransaction> BeginAsync(CancellationToken ct);
}

/// <summary>
/// One SQL transaction. Disposing it without <see cref="CommitAsync"/> rolls everything back, including the lock the
/// business day gate took inside it. Lock order (ADR-01): the service gates the business day first, then asks for the
/// order lock here, so a write never takes the order lock before the day lock.
/// </summary>
public interface IOrderTransaction : IAsyncDisposable
{
    /// <summary>Business day of an order, read without a lock (it never changes). Null when the order does not exist.</summary>
    Task<Guid?> FindBusinessDayAsync(long orderId, CancellationToken ct);

    /// <summary>Loads the order with its batches, portions and options and takes the update lock. Null when missing.</summary>
    Task<DonHang?> LockOrderAsync(long orderId, CancellationToken ct);

    /// <summary>Id of the open order of a table session, or null when the session has none.</summary>
    Task<long?> FindOpenOrderIdBySessionAsync(long sessionId, CancellationToken ct);

    /// <summary>The next human-readable order code (<c>MaDon</c>), unique over all orders.</summary>
    Task<string> NextOrderCodeAsync(CancellationToken ct);

    void Add(DonHang order);

    /// <summary>Writes the changes so database-generated ids exist; the transaction stays open.</summary>
    /// <exception cref="StoreConflictException">A unique index or a row version rejected the write.</exception>
    Task SaveAsync(CancellationToken ct);

    Task CommitAsync(CancellationToken ct);
}

public enum StoreConflict
{
    /// <summary>Another request opened an order for the same session first (one open order per session).</summary>
    OpenOrderForSession,

    /// <summary>The order changed under this request (row version or change counter), or the request key already exists.</summary>
    ConcurrentChange
}

public sealed class StoreConflictException(StoreConflict reason, Exception? inner = null) : Exception($"Sales store conflict: {reason}.", inner)
{
    public StoreConflict Reason { get; } = reason;
}
