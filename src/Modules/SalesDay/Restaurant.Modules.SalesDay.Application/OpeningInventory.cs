using System.Data.Common;

namespace Restaurant.Modules.SalesDay.Application;

public interface IOpeningInventory
{
    Task CarryAsync(DbTransaction transaction, Guid newDay, CancellationToken ct);
}
