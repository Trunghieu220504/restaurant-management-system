using System.Data.Common;

namespace Restaurant.Modules.Sales.Application;

public sealed record StaffPortion(string Id, string OrderCode, string Dish, string Variant, string? Note, string Status, string Version);
public sealed record PortionChange(string Version, string? Note, string? Reason);

public interface IStaffPortionDay
{
    Task<Guid?> CurrentOpenDayAsync(CancellationToken ct);
    Task<bool> RequireOpenAsync(DbTransaction transaction, Guid dayId, CancellationToken ct);
}

public interface IStaffPortions
{
    Task<IReadOnlyList<StaffPortion>> ListAsync(CancellationToken ct);
    Task<string?> ChangeAsync(long id, PortionChange change, bool cancel, Guid actor, CancellationToken ct);
}
