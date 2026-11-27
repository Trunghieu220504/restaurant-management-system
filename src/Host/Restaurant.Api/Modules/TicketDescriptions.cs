using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Sales.Infrastructure.Persistence;

namespace Restaurant.Api.Modules;

internal static class TicketDescriptions
{
    internal static async Task<TicketView[]> EnrichAsync(IEnumerable<TicketView> source, SalesDbContext sales, CancellationToken ct)
    {
        var tickets = source.ToArray();
        if (tickets.Length == 0) return tickets;
        var ids = tickets.Select(t => t.PortionId).Distinct().ToArray();
        var portions = await sales.Portions.AsNoTracking().Where(p => ids.Contains(p.Id)).Include(p => p.Options).ToDictionaryAsync(p => p.Id, ct);
        var codes = await (from portion in sales.Portions.AsNoTracking()
                           join batch in sales.Batches on portion.DotGoiMonId equals batch.Id
                           join order in sales.Orders on batch.DonHangId equals order.Id
                           where ids.Contains(portion.Id)
                           select new { portion.Id, order.MaDon }).ToDictionaryAsync(p => p.Id, p => p.MaDon, ct);
        return tickets.Select(ticket => portions.TryGetValue(ticket.PortionId, out var portion) ? ticket with
        {
            DishName = portion.TenMonChot,
            VariantName = portion.TenBienTheChot,
            OptionNames = portion.Options.Select(option => option.TenTuyChonChot).ToArray(),
            Note = portion.GhiChu,
            OrderCode = codes.GetValueOrDefault(ticket.PortionId)
        } : ticket).ToArray();
    }
}
