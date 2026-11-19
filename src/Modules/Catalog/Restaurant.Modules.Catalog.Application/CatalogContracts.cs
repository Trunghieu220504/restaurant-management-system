namespace Restaurant.Modules.Catalog.Application;

public static class CatalogErrors
{
    public const string InputInvalid = "catalog.input.invalid";
    public const string GroupNotFound = "catalog.group.not_found";
    public const string GroupInactive = "catalog.group.inactive";
    public const string ItemNotFound = "catalog.item.not_found";
    public const string ItemInactive = "catalog.item.inactive";
    public const string ItemCodeConflict = "catalog.item.code_conflict";
    public const string VariantNotFound = "catalog.variant.not_found";
    public const string VariantCodeConflict = "catalog.variant.code_conflict";
    public const string OptionGroupNotFound = "catalog.option_group.not_found";
    public const string OptionGroupInactive = "catalog.option_group.inactive";
    public const string OptionNotFound = "catalog.option.not_found";
    public const string SelectionRangeInvalid = "catalog.selection.invalid";
    public const string OptionRequired = "catalog.option.required";
    public const string ConcurrencyConflict = "catalog.concurrency.conflict";
}

public sealed record CatalogResult<T>(T? Value, string? Error)
{
    public static CatalogResult<T> Ok(T value) => new(value, null);
    public static CatalogResult<T> Fail(string error) => new(default, error);
}

public sealed record MenuDto(IReadOnlyList<MenuGroupDto> Groups, IReadOnlyList<MenuItemDto> Items,
    IReadOnlyList<OptionGroupDto> OptionGroups);
public sealed record MenuGroupDto(Guid Id, string Name, int SortOrder, bool Active, string Version);
public sealed record MenuItemDto(Guid Id, Guid GroupId, string Code, string Name, string Station,
    string? ImageObjectKey, bool Active, string Version, IReadOnlyList<VariantDto> Variants,
    IReadOnlyList<ItemOptionGroupDto> OptionGroups);
public sealed record VariantDto(Guid Id, Guid ItemId, string Code, string Name, long Price, bool Active, string Version);
public sealed record OptionGroupDto(Guid Id, string Name, string Type, bool Active, string Version,
    IReadOnlyList<OptionDto> Options);
public sealed record OptionDto(Guid Id, Guid OptionGroupId, string Name, long ExtraPrice, bool Active, string Version);
public sealed record ItemOptionGroupDto(Guid OptionGroupId, int MinSelections, int MaxSelections);

public sealed record CreateMenuGroupRequest(string Name, int SortOrder);
public sealed record UpdateMenuGroupRequest(string Name, int SortOrder, bool Active, string Version);
public sealed record CreateMenuItemRequest(Guid GroupId, string Code, string Name, string Station);
public sealed record UpdateMenuItemRequest(Guid GroupId, string Code, string Name, string Station, bool Active, string Version);
public sealed record CreateVariantRequest(string Code, string Name, long Price);
public sealed record UpdateVariantRequest(string Code, string Name, long Price, bool Active, string Version);
public sealed record CreateOptionGroupRequest(string Name, string Type);
public sealed record UpdateOptionGroupRequest(string Name, string Type, bool Active, string Version);
public sealed record CreateOptionRequest(string Name, long ExtraPrice);
public sealed record UpdateOptionRequest(string Name, long ExtraPrice, bool Active, string Version);
public sealed record AssignOptionGroupRequest(int MinSelections, int MaxSelections);

public interface ICatalogMenu
{
    Task<MenuDto> GetMenuAsync(CancellationToken ct);
    Task<CatalogResult<MenuGroupDto>> CreateGroupAsync(CreateMenuGroupRequest request, CancellationToken ct);
    Task<CatalogResult<MenuGroupDto>> UpdateGroupAsync(Guid id, UpdateMenuGroupRequest request, CancellationToken ct);
    Task<CatalogResult<MenuItemDto>> CreateItemAsync(CreateMenuItemRequest request, CancellationToken ct);
    Task<CatalogResult<MenuItemDto>> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken ct);
    Task<CatalogResult<VariantDto>> CreateVariantAsync(Guid itemId, CreateVariantRequest request, CancellationToken ct);
    Task<CatalogResult<VariantDto>> UpdateVariantAsync(Guid itemId, Guid id, UpdateVariantRequest request, CancellationToken ct);
    Task<CatalogResult<OptionGroupDto>> CreateOptionGroupAsync(CreateOptionGroupRequest request, CancellationToken ct);
    Task<CatalogResult<OptionGroupDto>> UpdateOptionGroupAsync(Guid id, UpdateOptionGroupRequest request, CancellationToken ct);
    Task<CatalogResult<OptionDto>> CreateOptionAsync(Guid groupId, CreateOptionRequest request, CancellationToken ct);
    Task<CatalogResult<OptionDto>> UpdateOptionAsync(Guid groupId, Guid id, UpdateOptionRequest request, CancellationToken ct);
    Task<CatalogResult<ItemOptionGroupDto>> AssignOptionGroupAsync(Guid itemId, Guid groupId, AssignOptionGroupRequest request, CancellationToken ct);
    Task<CatalogResult<bool>> RemoveOptionGroupAsync(Guid itemId, Guid groupId, CancellationToken ct);
}

public static class CatalogRules
{
    // Amounts are integer VND and must round-trip through JavaScript without losing precision.
    public const long MaximumPrice = 9_007_199_254_740_991;
    public static bool ValidName(string? value) => value?.Trim().Length is >= 1 and <= 200;
    public static bool ValidCode(string? value) => value?.Trim().Length is >= 1 and <= 50 &&
        value.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static bool ValidPrice(long value) => value is >= 0 and <= MaximumPrice;
    public static bool ValidStation(string? value) => value is "BEP" or "BAR";
    public static bool ValidOptionType(string? value) => value is "TOPPING" or "TUY_CHON";
    public static bool ValidSelections(int min, int max, int activeOptions) => min >= 0 && max >= min && max > 0 && max <= activeOptions;
    public static bool TryVersion(string? value, out byte[] version)
    {
        version = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { version = Convert.FromBase64String(value); }
        catch (FormatException) { return false; }
        return version.Length == 8;
    }
}
