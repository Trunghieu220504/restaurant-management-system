using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Application;

public sealed record TicketView(long Id, long PortionId, string Station, string Status, DateTime CreatedAt,
    Guid? StartedBy, DateTime? StartedAt, Guid? FinishedBy, DateTime? FinishedAt, Guid? DeliveredBy, DateTime? DeliveredAt,
    string Version)
{
    public string? DishName { get; init; }
    public string? VariantName { get; init; }
    public IReadOnlyList<string>? OptionNames { get; init; }
    public string? Note { get; init; }
    public string? OrderCode { get; init; }
}

public sealed record TicketCommand(string Version, Guid RequestKey, string Station);

public interface IFulfillmentService
{
    Task<IReadOnlyList<TicketView>> QueueAsync(string station, CancellationToken ct);
    Task<FulfillmentResult<TicketView>> StartAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct);
    Task<FulfillmentResult<TicketView>> ReadyAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct);
    Task<FulfillmentResult<TicketView>> DeliverAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct);
    Task<FulfillmentResult<TicketView>> CancelAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct);
}
