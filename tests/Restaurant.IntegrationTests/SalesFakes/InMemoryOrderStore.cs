using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Contracts;

namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>
/// In-memory <see cref="IOrderStore"/>. It hands out ids on save, hides new orders until commit, and can be told to fail the
/// next save with a conflict. Limit: loaded orders are the stored instances, so a change made after a failed save is not
/// rolled back (the real store reloads from SQL); tests therefore check the error and the call order, not rollback of state.
/// </summary>
public sealed class InMemoryOrderStore : IOrderStore
{
    private CallLog? Log { get; }

    public InMemoryOrderStore(CallLog? log = null) => Log = log;

    private readonly Dictionary<long, DonHang> committed = [];
    private long lastOrderId;
    private long lastBatchId;
    private long lastPortionId;
    private int lastCode;
    private StoreConflict? nextSaveFails;

    public IReadOnlyCollection<DonHang> Orders => committed.Values;

    public int Commits { get; private set; }

    public int Rollbacks { get; private set; }

    public void FailNextSave(StoreConflict reason) => nextSaveFails = reason;

    /// <summary>Stores an order as if an earlier request had created it.</summary>
    public DonHang Seed(DonHang order)
    {
        AssignIds([order]);
        committed[order.Id] = order;
        return order;
    }

    public Task<IOrderTransaction> BeginAsync(CancellationToken ct)
    {
        Log?.Add("tx.begin");
        return Task.FromResult<IOrderTransaction>(new Transaction(this));
    }

    private void AssignIds(IEnumerable<DonHang> orders)
    {
        foreach (var order in orders)
        {
            if (order.Id == 0) order.Id = ++lastOrderId;
            foreach (var batch in order.Dots)
            {
                if (batch.Id == 0) batch.Id = ++lastBatchId;
                foreach (var portion in batch.Portions)
                    if (portion.Id == 0) portion.Id = ++lastPortionId;
            }
        }
    }

    private sealed class Transaction(InMemoryOrderStore store) : IOrderTransaction
    {
        private readonly List<DonHang> pending = [];
        private bool done;

        public Task<Guid?> FindBusinessDayAsync(long orderId, CancellationToken ct)
        {
            store.Log?.Add($"order.find_day:{orderId}");
            return Task.FromResult<Guid?>(store.committed.TryGetValue(orderId, out var order) ? order.NgayBanId : null);
        }

        public Task<DonHang?> LockOrderAsync(long orderId, CancellationToken ct)
        {
            store.Log?.Add($"order.lock:{orderId}");
            return Task.FromResult(store.committed.GetValueOrDefault(orderId));
        }

        public Task<long?> FindOpenOrderIdBySessionAsync(long sessionId, CancellationToken ct)
        {
            store.Log?.Add($"order.find_open_by_session:{sessionId}");
            var found = store.committed.Values.FirstOrDefault(x =>
                x.PhienSuDungId == sessionId && x.TrangThai == OrderStatus.Open);
            return Task.FromResult<long?>(found?.Id);
        }

        public Task<string> NextOrderCodeAsync(CancellationToken ct) =>
            Task.FromResult($"DH-{++store.lastCode:D4}");

        public Task<GuestBatchReceipt?> FindGuestBatchAsync(GuestSalesSession session, Guid requestKey, CancellationToken ct) =>
            Task.FromResult(store.committed.Values.Where(x => x.PhienSuDungId == session.SessionId)
                .SelectMany(x => x.Dots)
                .Where(x => x.KhoaYeuCau == requestKey && x.QuyenTruyCapKhachId == session.GrantId)
                .Select(x => GuestBatchReceipt.From(x.Id, x.TrangThai)).SingleOrDefault());

        public Task<long?> FindGuestBatchOrderIdAsync(GuestSalesSession session, long batchId, CancellationToken ct) =>
            Task.FromResult(store.committed.Values.Where(x => x.PhienSuDungId == session.SessionId)
                .Where(x => x.Dots.Any(batch => batch.Id == batchId && batch.QuyenTruyCapKhachId == session.GrantId))
                .Select(x => (long?)x.Id).SingleOrDefault());

        public void Add(DonHang order) => pending.Add(order);

        public Task StageBatchConfirmedAsync(BatchConfirmed message, long aggregateId, int aggregateVersion, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct)
        {
            if (store.nextSaveFails is { } reason)
            {
                store.nextSaveFails = null;
                throw new StoreConflictException(reason);
            }

            store.AssignIds(pending.Concat(store.committed.Values));
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken ct)
        {
            foreach (var order in pending) store.committed[order.Id] = order;
            pending.Clear();
            done = true;
            store.Commits++;
            store.Log?.Add("tx.commit");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!done)
            {
                store.Rollbacks++;
                store.Log?.Add("tx.rollback");
            }

            return ValueTask.CompletedTask;
        }
    }
}
