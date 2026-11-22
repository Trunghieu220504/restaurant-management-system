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

public interface IMenuAvailability
{
    Task<AvailabilitySnapshot> SnapshotAsync(CancellationToken ct);
    Task<string?> SetAsync(AvailabilityChange change, Guid actor, CancellationToken ct);
}
