using System.Data;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;

namespace Restaurant.Modules.Catalog.Infrastructure;

public sealed class MenuPricing(CatalogDbContext db) : IMenuPricing
{
    public async Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct)
    {
        if (request?.Lines is not { Count: > 0 } || request.BusinessDayId == Guid.Empty ||
            request.Lines.Any(x => x is null) || request.Lines.Select(x => x.LineNo).Distinct().Count() != request.Lines.Count)
            return new([], [new(0, "catalog.input.invalid")]);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var day = await DayMenu.LoadAsync(db, request.BusinessDayId, ct);
        var result = new List<PricedMenuLine>();
        var errors = new List<MenuLineError>();
        foreach (var line in request.Lines)
        {
            var error = PriceLine(day, line, result);
            if (error is not null) errors.Add(new(line.LineNo, error));
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return new(errors.Count == 0 ? result : [], errors);
    }

    private static string? PriceLine(DayMenu day, MenuLineSelection line, List<PricedMenuLine> result)
    {
        var item = day.Menu.Items.SingleOrDefault(x => x.Id == line.MenuItemId);
        if (item is null) return CatalogErrorCodes.ItemNotFound;
        if (!item.Active || !day.Menu.Groups.Any(x => x.Id == item.GroupId && x.Active)) return CatalogErrorCodes.ItemInactive;
        if (day.SoldItems.Contains(item.Id)) return CatalogErrorCodes.ItemSoldOut;
        var variant = item.Variants.SingleOrDefault(x => x.Id == line.VariantId);
        if (variant is null) return CatalogErrorCodes.VariantNotOwned;
        if (!variant.Active || day.SoldVariants.Contains(variant.Id)) return CatalogErrorCodes.VariantSoldOut;
        if (line.OptionIds is null || line.OptionIds.Distinct().Count() != line.OptionIds.Count) return CatalogErrorCodes.OptionNotAllowed;
        var selected = new List<PricedOption>();
        foreach (var assignment in item.OptionGroups)
        {
            var group = day.Menu.OptionGroups.Single(x => x.Id == assignment.OptionGroupId);
            var options = group.Options.Where(x => line.OptionIds.Contains(x.Id)).ToArray();
            if (options.Any(x => !x.Active || !group.Active || day.SoldOptions.Contains(x.Id))) return CatalogErrorCodes.OptionSoldOut;
            if (options.Length < assignment.MinSelections) return CatalogErrorCodes.OptionGroupMinNotMet;
            if (options.Length > assignment.MaxSelections) return CatalogErrorCodes.OptionGroupMaxExceeded;
            selected.AddRange(options.Select(x => new PricedOption(x.Id, group.Id, group.Name, group.Type, x.Name, x.ExtraPrice)));
        }
        if (selected.Count != line.OptionIds.Count) return CatalogErrorCodes.OptionNotAllowed;
        var price = variant.Price + selected.Sum(x => (decimal)x.Surcharge);
        if (price > long.MaxValue) return "catalog.input.invalid";
        result.Add(new(line.LineNo, item.Id, item.Name, variant.Id, variant.Name, variant.Price, item.Station, selected, (long)price));
        return null;
    }
}
