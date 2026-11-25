using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Infrastructure;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Api.Modules;

public sealed class BusinessAudit(IServiceProvider services, IHttpContextAccessor http, TimeProvider clock) : IBusinessAudit
{
    public async Task AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken)
    {
        DbContext owner = entry.SourceModule switch
        {
            "Identity" => services.GetRequiredService<IdentityDbContext>(),
            "Seating" => services.GetRequiredService<SeatingDbContext>(),
            "Sales" => services.GetRequiredService<SalesDbContext>(),
            "Fulfillment" => services.GetRequiredService<FulfillmentDbContext>(),
            _ => throw new ArgumentException("Unsupported audit producer.", nameof(entry))
        };
        var transaction = owner.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Business audit requires the producer's active transaction.");
        var actor = entry.ActorId;
        if (entry.ResolveActor && actor is null && Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var caller)) actor = caller;
        var day = entry.BusinessDayId;
        if (day is null && owner is FulfillmentDbContext fulfillment && long.TryParse(entry.EntityId, out var id))
        {
            var ticketId = entry.EntityType == "SuCoMon"
                ? await fulfillment.Incidents.Where(x => x.Id == id).Select(x => x.PhieuCheBienId).SingleAsync(cancellationToken) : id;
            var portion = await fulfillment.Tickets.Where(x => x.Id == ticketId).Select(x => x.ChiTietDonId).SingleAsync(cancellationToken);
            await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(transaction.Connection!).Options);
            await sales.Database.UseTransactionAsync(transaction, cancellationToken);
            day = await (from p in sales.Portions
                         join batch in sales.Batches on p.DotGoiMonId equals batch.Id
                         join order in sales.Orders on batch.DonHangId equals order.Id
                         where p.Id == portion
                         select (Guid?)order.NgayBanId).SingleAsync(cancellationToken);
        }
        await using var audit = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(transaction.Connection!).Options);
        await audit.Database.UseTransactionAsync(transaction, cancellationToken);
        var reason = entry.Reason is null ? null : new string(entry.Reason.Where(c => !char.IsControl(c)).Take(512).ToArray());
        await new AuditLog(audit, clock).AppendAsync(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actor,
            actor is null ? "System" : entry.SourceModule, entry.Action, entry.EntityType, entry.EntityId, day, reason), cancellationToken);
    }
}
