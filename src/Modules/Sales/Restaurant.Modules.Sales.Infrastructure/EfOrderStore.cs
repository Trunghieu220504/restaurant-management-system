using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Sales.Infrastructure;

internal sealed class EfOrderStore(SalesDbContext db, IBusinessAudit audit) : IOrderStore
{
    public async Task<IOrderTransaction> BeginAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        return new Transaction(db, transaction, audit);
    }

    private sealed class Transaction(SalesDbContext db, IDbContextTransaction transaction, IBusinessAudit audit) : IOrderTransaction
    {
        private bool committed;

        public Task<Guid?> FindBusinessDayAsync(long orderId, CancellationToken ct) =>
            db.Orders.AsNoTracking().Where(x => x.Id == orderId).Select(x => (Guid?)x.NgayBanId).SingleOrDefaultAsync(ct);

        public Task<DonHang?> LockOrderAsync(long orderId, CancellationToken ct) =>
            db.Orders.FromSqlInterpolated($"SELECT * FROM [sales].[DonHang] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId}")
                .Include(x => x.Dots).ThenInclude(x => x.Portions).ThenInclude(x => x.Options)
                .SingleOrDefaultAsync(ct);

        public Task<long?> FindOpenOrderIdBySessionAsync(long sessionId, CancellationToken ct) =>
            db.Orders.AsNoTracking()
                .Where(x => x.PhienSuDungId == sessionId && x.TrangThai == OrderStatus.Open)
                .Select(x => (long?)x.Id)
                .SingleOrDefaultAsync(ct);

        public async Task<GuestBatchReceipt?> FindGuestBatchAsync(GuestSalesSession session, Guid requestKey, CancellationToken ct)
        {
            var previous = await (from batch in db.Batches.AsNoTracking()
                                  join order in db.Orders on batch.DonHangId equals order.Id
                                  where batch.KhoaYeuCau == requestKey && batch.QuyenTruyCapKhachId == session.GrantId &&
                                      order.PhienSuDungId == session.SessionId
                                  select new { batch.Id, batch.TrangThai }).SingleOrDefaultAsync(ct);
            return previous is null ? null : GuestBatchReceipt.From(previous.Id, previous.TrangThai);
        }

        public Task<long?> FindGuestBatchOrderIdAsync(GuestSalesSession session, long batchId, CancellationToken ct) =>
            (from batch in db.Batches.AsNoTracking()
             join order in db.Orders.AsNoTracking() on batch.DonHangId equals order.Id
             where batch.Id == batchId && batch.QuyenTruyCapKhachId == session.GrantId &&
                 order.PhienSuDungId == session.SessionId
             select (long?)order.Id).SingleOrDefaultAsync(ct);

        public async Task<string> NextOrderCodeAsync(CancellationToken ct)
        {
            var suffix = await db.Orders.AsNoTracking()
                .Where(x => x.MaDon.StartsWith("DH-"))
                .Select(x => x.MaDon)
                .ToListAsync(ct);
            var next = suffix.Select(x => int.TryParse(x[3..], out var value) ? value : 0).DefaultIfEmpty().Max() + 1;
            return $"DH-{next:D4}";
        }

        public void Add(DonHang order) => db.Orders.Add(order);

        public Task StageBatchConfirmedAsync(BatchConfirmed message, long aggregateId, int aggregateVersion, CancellationToken ct)
        {
            var envelope = new IntegrationEvent<BatchConfirmed>(Guid.NewGuid(), DateTimeOffset.UtcNow,
                "sales.batch_confirmed", 1, message);
            db.Set<Messaging.OutboxEntry>().Add(new Messaging.OutboxEntry
            {
                Id = envelope.EventId,
                AggregateId = aggregateId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AggregateVersion = aggregateVersion,
                Type = envelope.Type,
                SchemaVersion = envelope.Version,
                Payload = IntegrationEventJson.SerializePayload(envelope.Payload),
                OccurredAt = envelope.OccurredAt.UtcDateTime,
                NextAttemptAt = envelope.OccurredAt.UtcDateTime
            });
            return Task.CompletedTask;
        }

        public async Task SaveAsync(CancellationToken ct)
        {
            var orders = db.ChangeTracker.Entries<DonHang>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
                .Select(x => (Order: x.Entity, Created: x.State == EntityState.Added)).ToArray();
            var batches = db.ChangeTracker.Entries<DotGoiMon>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
                .Select(x => (Batch: x.Entity, Created: x.State == EntityState.Added)).ToArray();
            try
            {
                await db.SaveChangesAsync(ct);
                foreach (var (order, created) in orders)
                    await audit.AppendAsync(new("Sales", created ? "sales.order.created" : "sales.order.updated",
                        "DonHang", order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        created ? order.NguoiTaoId : null, order.NgayBanId), ct);
                foreach (var (batch, created) in batches)
                {
                    var day = await db.Orders.AsNoTracking()
                        .Where(x => x.Id == batch.DonHangId)
                        .Select(x => (Guid?)x.NgayBanId)
                        .SingleOrDefaultAsync(ct)
                        ?? throw new InvalidOperationException($"Order {batch.DonHangId} was not found for batch audit.");
                    var action = created ? "sales.batch.added" : batch.TrangThai switch
                    {
                        BatchStatus.Confirmed => "sales.batch.confirmed",
                        BatchStatus.Rejected => "sales.batch.rejected",
                        _ => "sales.batch.cancelled"
                    };
                    await audit.AppendAsync(new("Sales", action, "DotGoiMon", batch.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        created ? batch.NguoiTaoId : batch.NguoiXacNhanId, day, batch.LyDoTuChoi), ct);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new StoreConflictException(StoreConflict.ConcurrentChange, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_DonHang_PhienMo", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new StoreConflictException(StoreConflict.OpenOrderForSession, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_DonHang_MaDon", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new StoreConflictException(StoreConflict.ConcurrentChange, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Outbox_SuKien", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new StoreConflictException(StoreConflict.ConcurrentChange, ex);
            }
        }

        public async Task CommitAsync(CancellationToken ct)
        {
            await transaction.CommitAsync(ct);
            committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!committed) await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }
    }
}
