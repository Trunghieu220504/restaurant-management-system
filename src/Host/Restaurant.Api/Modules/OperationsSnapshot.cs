using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

public sealed class OperationsSnapshotReader(FulfillmentDbContext fulfillment, SalesDbContext sales) : IOperationsSnapshot
{
    public async Task<OperationsSnapshot> ReadAsync(IReadOnlyCollection<string> stations, long? orderId, CancellationToken ct)
    {
        var query = fulfillment.Tickets.AsNoTracking().Where(x => stations.Contains(x.TramChot));
        if (orderId.HasValue)
        {
            var portions = await (from portion in sales.Portions
                                  join batch in sales.Batches on portion.DotGoiMonId equals batch.Id
                                  where batch.DonHangId == orderId.Value
                                  select portion.Id).ToArrayAsync(ct);
            query = query.Where(x => portions.Contains(x.ChiTietDonId));
        }
        var tickets = await query.OrderBy(x => x.TaoLuc).ThenBy(x => x.Id).ToArrayAsync(ct);
        var terminal = tickets.Where(x => x.TrangThai is TicketStatus.Served or TicketStatus.Cancelled or TicketStatus.Failed).ToArray();
        var visible = orderId.HasValue ? tickets : tickets.Except(terminal);
        return new OperationsSnapshot(await TicketDescriptions.EnrichAsync(visible.Select(TicketEvents.View), sales, ct),
            terminal.Select(x => new TerminalTicket(x.Id, Convert.ToBase64String(x.Version))).ToArray());
    }
}
