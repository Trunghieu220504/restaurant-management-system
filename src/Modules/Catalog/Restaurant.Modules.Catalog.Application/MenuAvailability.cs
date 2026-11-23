using System.Data.Common;

namespace Restaurant.Modules.Catalog.Application;

public interface ICatalogBusinessDays
{
    Task<Guid?> CurrentOpenDayAsync(CancellationToken ct);
    Task<bool> RequireOpenAsync(DbTransaction transaction, Guid dayId, CancellationToken ct);
}

public sealed record AvailabilityChange(Guid BusinessDayId, string Kind, Guid TargetId, bool SoldOut);
public sealed record SoldOutTarget(string Kind, Guid TargetId);
public sealed record AvailabilitySnapshot(Guid? BusinessDayId, IReadOnlyList<SoldOutTarget> SoldOut);

public sealed record PublicVariant(Guid Id, string Name, long Price);
public sealed record PublicChoice(Guid Id, string Name, long ExtraPrice);
public sealed record PublicOptionGroup(Guid Id, string Name, string Type, int Min, int Max, IReadOnlyList<PublicChoice> Options);
public sealed record PublicItem(Guid Id, string Name, string? ImageObjectKey, IReadOnlyList<PublicVariant> Variants, IReadOnlyList<PublicOptionGroup> OptionGroups);
public sealed record PublicGroup(Guid Id, string Name, IReadOnlyList<PublicItem> Items);
public sealed record PublicMenu(Guid? BusinessDayId, IReadOnlyList<PublicGroup> Groups);

public interface IMenuAvailability
{
    Task<PublicMenu> PublicAsync(CancellationToken ct);
    Task<AvailabilitySnapshot> SnapshotAsync(CancellationToken ct);
    Task<string?> SetAsync(AvailabilityChange change, Guid actor, CancellationToken ct);
}
