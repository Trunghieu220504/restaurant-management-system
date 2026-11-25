using System.Security.Claims;
using Restaurant.Modules.Fulfillment.Application;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class OperationsAccessTests
{
    [Theory]
    [InlineData("Kitchen", "BEP", true)]
    [InlineData("Kitchen", "BAR", false)]
    [InlineData("Bar", "BAR", true)]
    [InlineData("Bar", "BEP", false)]
    [InlineData("Staff", "BEP", true)]
    [InlineData("Manager", "BAR", true)]
    [InlineData("Admin", "BAR", false)]
    [InlineData("Kitchen", "unknown", false)]
    public void Station_subscriptions_are_limited_to_operational_roles(string role, string station, bool allowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", role)], "test", "sub", "role"));
        Assert.Equal(allowed, OperationsAccess.CanReadStation(user, station));
    }

    [Theory]
    [InlineData("Staff", true)]
    [InlineData("Manager", true)]
    [InlineData("Kitchen", false)]
    [InlineData("Bar", false)]
    [InlineData("Admin", false)]
    public void Only_serving_staff_and_managers_can_watch_whole_orders(string role, bool allowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", role)], "test", "sub", "role"));
        Assert.Equal(allowed, OperationsAccess.CanServe(user));
    }
}
