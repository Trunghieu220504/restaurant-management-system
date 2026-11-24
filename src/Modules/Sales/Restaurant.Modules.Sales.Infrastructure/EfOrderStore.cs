using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Sales.Infrastructure;

internal sealed class EfOrderStore(SalesDbContext db) : IOrderStore
{
    public async Task<IOrderTransaction> BeginAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        return new Transaction(db, transaction);
    }

    private sealed class Transaction(SalesDbContext db, IDbContextTransaction transaction) : IOrderTransaction
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
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new StoreConflictException(StoreConflict.ConcurrentChange, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_DonHang_PhienMo", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new StoreConflictException(StoreConflict.OpenOrderForSession, ex);
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
