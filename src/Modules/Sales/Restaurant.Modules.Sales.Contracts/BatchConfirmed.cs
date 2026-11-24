namespace Restaurant.Modules.Sales.Contracts;

public sealed record BatchConfirmed(long OrderId, long BatchId, int BatchNumber,
    IReadOnlyList<ConfirmedPortion> Portions);

public sealed record ConfirmedPortion(long PortionId, string Station);
