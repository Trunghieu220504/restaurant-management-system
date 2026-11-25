using Restaurant.Modules.Fulfillment.Domain;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class FulfillmentIncidentTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Remake_requires_approval_and_keeps_the_same_portion_without_reusing_old_attempt()
    {
        var ticket = PhieuCheBien.CreateInitial(42, Stations.Kitchen, Now).Value!;
        ticket.Id = 10;
        var incident = SuCoMon.Report(ticket.Id, "Burnt", Actor, Guid.NewGuid(), Now).Value!;
        incident.Id = 20;
        Assert.False(ticket.ResolveIncident(incident, Actor, Now).Succeeded);
        Assert.True(incident.Decide(IncidentDecision.Remake, "Prepare again", Actor, Now).Succeeded);
        var remake = ticket.ResolveIncident(incident, Actor, Now).Value!;
        Assert.Equal(42, remake.ChiTietDonId);
        Assert.Equal(2, remake.Lan);
        Assert.Equal(ticket.Id, remake.PhieuGocId);
        Assert.Equal(incident.Id, remake.SuCoNguonId);
        Assert.Equal(TicketStatus.New, remake.TrangThai);
        Assert.Equal(TicketStatus.Failed, ticket.TrangThai);
        Assert.False(ticket.Deliver(Actor, Now).Succeeded);
        Assert.False(ticket.ResolveIncident(incident, Actor, Now).Succeeded);
        Assert.False(incident.Decide(IncidentDecision.Remake, "Again", Actor, Now).Succeeded);
    }

    [Theory]
    [InlineData(IncidentDecision.CancelBookkeepingOnly)]
    [InlineData(IncidentDecision.CancelWithObligationAdjustment)]
    public void Cancellation_decision_cancels_original_and_creates_no_new_ticket(string decision)
    {
        var ticket = PhieuCheBien.CreateInitial(42, Stations.Bar, Now).Value!;
        ticket.Id = 10;
        var incident = SuCoMon.Report(ticket.Id, "Wrong drink", Actor, Guid.NewGuid(), Now).Value!;
        incident.Id = 20;
        Assert.True(incident.Decide(decision, "Cancel", Actor, Now).Succeeded);
        var result = ticket.ResolveIncident(incident, Actor, Now);
        Assert.True(result.Succeeded);
        Assert.Same(ticket, result.Value);
        Assert.Equal(TicketStatus.Cancelled, ticket.TrangThai);
    }

    [Fact]
    public void Rejected_or_unrelated_incident_cannot_replace_ticket()
    {
        var ticket = PhieuCheBien.CreateInitial(42, Stations.Bar, Now).Value!;
        ticket.Id = 10;
        var incident = SuCoMon.Report(11, "Wrong drink", Actor, Guid.NewGuid(), Now).Value!;
        incident.Id = 20;
        Assert.False(incident.Decide("UNKNOWN", "reason", Actor, Now).Succeeded);
        Assert.False(incident.Decide(IncidentDecision.Remake, "", Actor, Now).Succeeded);
        Assert.True(incident.Decide(IncidentDecision.Remake, "reason", Actor, Now).Succeeded);
        Assert.False(ticket.ResolveIncident(incident, Actor, Now).Succeeded);
        var rejected = SuCoMon.Report(10, "Report", Actor, Guid.NewGuid(), Now).Value!;
        Assert.True(rejected.Decide(null, "Not a fault", Actor, Now).Succeeded);
        Assert.Equal(IncidentStatus.Rejected, rejected.TrangThai);
        Assert.False(ticket.ResolveIncident(rejected, Actor, Now).Succeeded);
    }
}
