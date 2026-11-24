using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Infrastructure;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;

namespace Restaurant.Api.Modules;

internal sealed class InventoryAudit(TimeProvider clock) : IInventoryAudit
{
    public async Task AppendAsync(DbTransaction transaction, Guid actor, Guid day, string action,
        string entity, string id, string reason, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(transaction.Connection!).Options;
        await using var db = new AuditDbContext(options);
        await db.Database.UseTransactionAsync(transaction, ct);
        await new AuditLog(db, clock).AppendAsync(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actor, "Inventory", action, entity, id, day, reason), ct);
    }
}
