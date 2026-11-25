using System.Data.Common;
using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Application;

public sealed record IncidentReport(string Version, string Station, string Reason, Guid RequestKey);
public sealed record IncidentResolution(string Version, string? Decision, string Reason, Guid RequestKey);
public sealed record IncidentView(long Id, long TicketId, string Reason, string Status, string? Decision,
    Guid ReportedBy, DateTime ReportedAt, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionReason,
    string Version, long? RemakeTicketId);

public interface IFulfillmentIncidents
{
    Task<IReadOnlyList<IncidentView>> PendingAsync(IReadOnlyCollection<string> stations, CancellationToken ct);
    Task<FulfillmentResult<IncidentView>> ReportAsync(long ticketId, IncidentReport request, Guid actor, CancellationToken ct);
    Task<FulfillmentResult<IncidentView>> ResolveAsync(long incidentId, IncidentResolution request, Guid actor, CancellationToken ct);
}

// Host implements cross-module operations on the caller's transaction. Lock order: day, order, ticket, invoice.
public interface IFulfillmentIncidentContext
{
    Task<string?> RequireOpenAsync(DbTransaction transaction, long portionId, CancellationToken ct);
    Task<string?> ReduceObligationAsync(DbTransaction transaction, long portionId, long incidentId,
        Guid actor, Guid requestKey, string reason, CancellationToken ct);
}
