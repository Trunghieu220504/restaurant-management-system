using Restaurant.Modules.Fulfillment.Domain;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class FulfillmentStateTests
{
    private static PhieuCheBien Ticket() =>
        PhieuCheBien.CreateInitial(42, Stations.Kitchen, new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc)).Value!;

    [Fact]
    public void A_ticket_follows_the_preparation_and_delivery_sequence()
    {
        var ticket = Ticket();
        var actor = Guid.NewGuid();
        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        Assert.True(ticket.Start(actor, now).Succeeded);
        Assert.True(ticket.MarkReady(actor, now.AddMinutes(5)).Succeeded);
        Assert.True(ticket.Deliver(actor, now.AddMinutes(6)).Succeeded);
        Assert.Equal(TicketStatus.Served, ticket.TrangThai);
    }

    [Fact]
    public void A_ticket_cannot_be_delivered_twice_or_after_cancellation()
    {
        var actor = Guid.NewGuid();
        var ticket = Ticket();
        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FulfillmentErrors.InvalidTransition, ticket.Deliver(actor, now).Error);
        Assert.True(ticket.Cancel(actor, now).Succeeded);
        Assert.Equal(FulfillmentErrors.AlreadyProcessed, ticket.Cancel(actor, now).Error);
        Assert.Equal(FulfillmentErrors.AlreadyProcessed, ticket.Deliver(actor, now).Error);
    }
}
