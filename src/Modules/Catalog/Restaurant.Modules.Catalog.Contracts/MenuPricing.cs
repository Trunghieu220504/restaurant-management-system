namespace Restaurant.Modules.Catalog.Contracts;

/// <summary>
/// Sales asks Catalog to validate and price the portions of one batch. Read-only: it never
/// changes catalog data. Sales calls it again at confirmation time (not only at submission),
/// so the result reflects availability and prices of that moment. Catalog does not decide
/// whether the business day or session may take orders; that is SalesDay/Seating.
/// </summary>
public interface IMenuPricing
{
    Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct);
}

/// <param name="BusinessDayId">Day whose sold-out flags apply.</param>
/// <param name="Lines">One entry per portion (one portion = one order detail row).</param>
public sealed record MenuPricingRequest(Guid BusinessDayId, IReadOnlyList<MenuLineSelection> Lines);

/// <param name="LineNo">Caller-chosen key, unique in the request, echoed in the result.</param>
/// <param name="OptionIds">Chosen options; one per option per portion.</param>
public sealed record MenuLineSelection(int LineNo, Guid MenuItemId, Guid VariantId, IReadOnlyList<Guid> OptionIds);

/// <summary>Valid only when <see cref="Errors"/> is empty; then <see cref="Lines"/> has every request line.</summary>
public sealed record MenuPricingResult(IReadOnlyList<PricedMenuLine> Lines, IReadOnlyList<MenuLineError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Snapshot values Sales stores on the portion. Amounts are integer VND.</summary>
/// <param name="Station">BEP or BAR.</param>
/// <param name="UnitPrice">VariantPrice plus the sum of option surcharges.</param>
public sealed record PricedMenuLine(
    int LineNo,
    Guid MenuItemId,
    string MenuItemName,
    Guid VariantId,
    string VariantName,
    long VariantPrice,
    string Station,
    IReadOnlyList<PricedOption> Options,
    long UnitPrice);

/// <param name="GroupKind">TOPPING or TUY_CHON.</param>
public sealed record PricedOption(Guid OptionId, Guid GroupId, string GroupName, string GroupKind, string OptionName, long Surcharge);

/// <param name="Code">One of <see cref="CatalogErrorCodes"/>.</param>
public sealed record MenuLineError(int LineNo, string Code);

public static class CatalogErrorCodes
{
    public const string ItemNotFound = "catalog.item.not_found";
    public const string ItemInactive = "catalog.item.inactive";
    public const string ItemSoldOut = "catalog.item.sold_out";
    public const string VariantNotOwned = "catalog.variant.not_owned";
    public const string VariantSoldOut = "catalog.variant.sold_out";
    public const string OptionNotAllowed = "catalog.option.not_allowed";
    public const string OptionSoldOut = "catalog.option.sold_out";
    public const string OptionGroupMinNotMet = "catalog.option_group.min_not_met";
    public const string OptionGroupMaxExceeded = "catalog.option_group.max_exceeded";
}
