using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;

namespace Restaurant.Modules.Catalog.Infrastructure;

internal sealed record DayMenu(MenuDto Menu, HashSet<Guid> SoldItems, HashSet<Guid> SoldVariants, HashSet<Guid> SoldOptions)
{
    internal static async Task<DayMenu> LoadAsync(CatalogDbContext db, Guid dayId, CancellationToken ct)
    {
        var menu = await new CatalogMenu(db).GetMenuAsync(ct);
        var items = await db.Set<HetMon>().Where(x => x.NgayBanId == dayId && x.Het).Select(x => x.MonId).ToHashSetAsync(ct);
        var variants = await db.Set<HetBienThe>().Where(x => x.NgayBanId == dayId && x.Het).Select(x => x.BienTheId).ToHashSetAsync(ct);
        var options = await db.Set<HetTuyChon>().Where(x => x.NgayBanId == dayId && x.Het).Select(x => x.TuyChonId).ToHashSetAsync(ct);
        return new(menu, items, variants, options);
    }

    internal PublicItem? PublicItem(MenuItemDto item)
    {
        if (!item.Active || SoldItems.Contains(item.Id)) return null;
        var variants = item.Variants.Where(x => x.Active && !SoldVariants.Contains(x.Id))
            .Select(x => new PublicVariant(x.Id, x.Name, x.Price)).ToArray();
        if (variants.Length == 0) return null;
        var groups = new List<PublicOptionGroup>();
        foreach (var assignment in item.OptionGroups)
        {
            var group = Menu.OptionGroups.Single(x => x.Id == assignment.OptionGroupId);
            var options = group.Options.Where(x => group.Active && x.Active && !SoldOptions.Contains(x.Id))
                .Select(x => new PublicChoice(x.Id, x.Name, x.ExtraPrice)).ToArray();
            if (options.Length < assignment.MinSelections) return null;
            if (options.Length > 0) groups.Add(new(group.Id, group.Name, group.Type, assignment.MinSelections,
                Math.Min(assignment.MaxSelections, options.Length), options));
        }
        return new(item.Id, item.Name, variants, groups);
    }
}
