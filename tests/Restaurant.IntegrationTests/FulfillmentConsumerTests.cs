using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Shared.Contracts;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class FulfillmentConsumerTests
{
    [Fact]
    public void A_confirmed_portion_creates_one_new_ticket_for_its_station()
    {
        var eventData = new BatchConfirmed(10, 20, 1,
            [new ConfirmedPortion(42, Stations.Kitchen), new ConfirmedPortion(43, Stations.Bar)]);

        var tickets = eventData.Portions
            .Select(portion => PhieuCheBien.CreateInitial(portion.PortionId, portion.Station,
                new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc)))
            .ToArray();

        Assert.All(tickets, ticket => Assert.True(ticket.Succeeded));
        Assert.Equal([Stations.Kitchen, Stations.Bar], tickets.Select(x => x.Value!.TramChot));
        Assert.All(tickets, ticket => Assert.Equal(TicketStatus.New, ticket.Value!.TrangThai));
    }

    [Fact]
    public void The_event_contract_keeps_one_portion_identity_per_ticket()
    {
        var eventData = new BatchConfirmed(10, 20, 1,
            [new ConfirmedPortion(42, Stations.Kitchen), new ConfirmedPortion(43, Stations.Kitchen)]);

        Assert.Equal([42L, 43L], eventData.Portions.Select(x => x.PortionId));
        Assert.NotEqual(eventData.Portions[0].PortionId, eventData.Portions[1].PortionId);
    }
}
