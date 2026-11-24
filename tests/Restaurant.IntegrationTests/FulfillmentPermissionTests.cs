using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Presentation;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class FulfillmentPermissionTests
{
    [Theory]
    [InlineData("Staff", "fulfillment.portions.serve", "BEP", "deliver", true)]
    [InlineData("Manager", "fulfillment.portions.serve", "BAR", "deliver", true)]
    [InlineData("Kitchen", "fulfillment.kitchen.process", "BEP", "deliver", false)]
    [InlineData("Kitchen", "fulfillment.kitchen.process", "BEP", "start", true)]
    [InlineData("Kitchen", "fulfillment.kitchen.process", "BAR", "start", false)]
    [InlineData("Bar", "fulfillment.bar.process", "BAR", "ready", true)]
    [InlineData("Staff", "fulfillment.portions.serve", "BEP", "start", false)]
    [InlineData("Admin", "", "BEP", "deliver", false)]
    [InlineData("Kitchen", "fulfillment.kitchen.process", "BEP", "cancel", false)]
    [InlineData("Manager", "fulfillment.incidents.approve", "BEP", "cancel", true)]
    public async Task Commands_require_the_action_permission_and_station_scope(string role, string permission, string station, string action, bool allowed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IFulfillmentService, Stub>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapFulfillmentEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/fulfillment/tickets/1/{action}");
        request.Headers.Add("Test-Role", role);
        request.Headers.Add("Test-Permission", permission);
        request.Content = JsonContent.Create(new TicketCommand("AAAAAAAAAAE=", Guid.NewGuid(), station));
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", Request.Headers["Test-Role"].ToString()),
                new Claim("permission", Request.Headers["Test-Permission"].ToString()) };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name, "sub", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class Stub : IFulfillmentService
    {
        private static Task<FulfillmentResult<TicketView>> Success() => Task.FromResult(FulfillmentResult<TicketView>.Ok(
            new(1, 42, Stations.Kitchen, TicketStatus.Ready, DateTime.UtcNow, null, null, null, null, null, null, "AAAAAAAAAAE=")));
        public Task<IReadOnlyList<TicketView>> QueueAsync(string station, CancellationToken ct) => Task.FromResult<IReadOnlyList<TicketView>>([]);
        public Task<FulfillmentResult<TicketView>> StartAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) => Success();
        public Task<FulfillmentResult<TicketView>> ReadyAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) => Success();
        public Task<FulfillmentResult<TicketView>> DeliverAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) => Success();
        public Task<FulfillmentResult<TicketView>> CancelAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) => Success();
    }
}
