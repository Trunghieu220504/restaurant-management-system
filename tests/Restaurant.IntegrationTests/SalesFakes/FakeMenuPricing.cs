using Restaurant.Modules.Catalog.Contracts;

namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>
/// In-memory <see cref="IMenuPricing"/> with a tiny menu built by the test. It applies the contract error codes for the
/// cases Sales needs (unknown, inactive or sold-out dish, wrong variant, option not offered or sold out) and records requests.
/// </summary>
public sealed class FakeMenuPricing(CallLog? log = null) : IMenuPricing
{
    private sealed record Dish(string Name, string Station, bool Active, HashSet<Guid> SoldOutOptions, bool SoldOut);
    private sealed record Variant(Guid DishId, string Name, long Price);
    private sealed record Option(Guid DishId, Guid GroupId, string GroupName, string Kind, string Name, long Surcharge);

    private readonly Dictionary<Guid, Dish> dishes = [];
    private readonly Dictionary<Guid, Variant> variants = [];
    private readonly Dictionary<Guid, Option> options = [];

    public List<MenuPricingRequest> Requests { get; } = [];

    public FakeMenuPricing AddDish(Guid id, string name, string station, bool active = true, bool soldOut = false)
    {
        dishes[id] = new Dish(name, station, active, [], soldOut);
        return this;
    }

    public FakeMenuPricing AddVariant(Guid dishId, Guid id, string name, long price)
    {
        variants[id] = new Variant(dishId, name, price);
        return this;
    }

    public FakeMenuPricing AddOption(Guid dishId, Guid id, Guid groupId, string groupName, string kind, string name, long surcharge)
    {
        options[id] = new Option(dishId, groupId, groupName, kind, name, surcharge);
        return this;
    }

    public void SellOutOption(Guid dishId, Guid optionId) => dishes[dishId].SoldOutOptions.Add(optionId);

    public void SetPrice(Guid variantId, long price) => variants[variantId] = variants[variantId] with { Price = price };

    public Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct)
    {
        log?.Add($"menu.price:{request.BusinessDayId}");
        Requests.Add(request);
        var lines = new List<PricedMenuLine>();
        var errors = new List<MenuLineError>();
        foreach (var line in request.Lines)
        {
            var error = Check(line, out var priced);
            if (error is not null) errors.Add(new MenuLineError(line.LineNo, error));
            else lines.Add(priced!);
        }

        return Task.FromResult(new MenuPricingResult(errors.Count == 0 ? lines : [], errors));
    }

    private string? Check(MenuLineSelection line, out PricedMenuLine? priced)
    {
        priced = null;
        if (!dishes.TryGetValue(line.MenuItemId, out var dish)) return CatalogErrorCodes.ItemNotFound;
        if (!dish.Active) return CatalogErrorCodes.ItemInactive;
        if (dish.SoldOut) return CatalogErrorCodes.ItemSoldOut;
        if (!variants.TryGetValue(line.VariantId, out var variant) || variant.DishId != line.MenuItemId)
            return CatalogErrorCodes.VariantNotOwned;
        var chosen = new List<PricedOption>();
        foreach (var optionId in line.OptionIds)
        {
            if (!options.TryGetValue(optionId, out var option) || option.DishId != line.MenuItemId)
                return CatalogErrorCodes.OptionNotAllowed;
            if (dish.SoldOutOptions.Contains(optionId)) return CatalogErrorCodes.OptionSoldOut;
            chosen.Add(new PricedOption(optionId, option.GroupId, option.GroupName, option.Kind, option.Name, option.Surcharge));
        }

        priced = new PricedMenuLine(line.LineNo, line.MenuItemId, dish.Name, line.VariantId, variant.Name, variant.Price,
            dish.Station, chosen, variant.Price + chosen.Sum(x => x.Surcharge));
        return null;
    }
}
