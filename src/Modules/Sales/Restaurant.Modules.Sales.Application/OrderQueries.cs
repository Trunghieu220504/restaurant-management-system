namespace Restaurant.Modules.Sales.Application;

public sealed record OpenOrderBatch(long Id, int Number, string Status, long Total, IReadOnlyList<PortionView> Portions);
public sealed record OpenOrder(long Id, string Code, string Type, Guid BusinessDayId, long? SessionId,
    string? TableLabel, string? CustomerName, IReadOnlyList<OpenOrderBatch> Batches);
public sealed record OpenOrdersPage(IReadOnlyList<OpenOrder> Items, int Total);
public interface IOrderQueries
{
    Task<OpenOrdersPage> ReadOpenAsync(int page, int pageSize, CancellationToken ct);
}
