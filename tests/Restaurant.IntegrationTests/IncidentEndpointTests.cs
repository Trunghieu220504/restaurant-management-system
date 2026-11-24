using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Identity.Application.Authentication;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class IncidentEndpointTests
{
    [Theory]
    [InlineData("Kitchen", "BEP", true, false)]
    [InlineData("Kitchen", "BAR", false, false)]
    [InlineData("Bar", "BAR", true, false)]
    [InlineData("Bar", "BEP", false, false)]
    [InlineData("Staff", "BEP", true, false)]
    [InlineData("Manager", "BAR", true, true)]
    [InlineData("Admin", "BEP", false, false)]
    public async Task Incident_report_is_station_scoped_and_decisions_require_manager(string role, string station, bool canReport, bool canDecide)
    {
        using var original = new ApiFactory();
        var identity = new IdentityStub(role);
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIdentityAuthentication>(identity);
            services.AddSingleton<IFulfillmentIncidents>(new IncidentsStub());
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Services.GetRequiredService<IAccessTokenIssuer>()
            .Issue(identity.UserId, identity.SessionId, [role], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5)));
        using var report = await client.PostAsJsonAsync("/api/fulfillment/tickets/1/incidents",
            new IncidentReport("AAAAAAAAAAE=", station, "Wrong dish", Guid.NewGuid()));
        Assert.Equal(canReport ? HttpStatusCode.OK : HttpStatusCode.Forbidden, report.StatusCode);
        using var decide = await client.PostAsJsonAsync("/api/fulfillment/incidents/1/decision",
            new IncidentResolution("AAAAAAAAAAE=", IncidentDecision.Remake, "Remake", Guid.NewGuid()));
        Assert.Equal(canDecide ? HttpStatusCode.OK : HttpStatusCode.Forbidden, decide.StatusCode);
    }

    private sealed class IncidentsStub : IFulfillmentIncidents
    {
        private static IncidentView View => new(1, 1, "Wrong dish", IncidentStatus.Pending, null, Guid.NewGuid(), DateTime.UtcNow,
            null, null, null, "AAAAAAAAAAE=", null);
        public Task<IReadOnlyList<IncidentView>> PendingAsync(IReadOnlyCollection<string> stations, CancellationToken ct) => Task.FromResult<IReadOnlyList<IncidentView>>([View]);
        public Task<FulfillmentResult<IncidentView>> ReportAsync(long id, IncidentReport request, Guid actor, CancellationToken ct) => Task.FromResult(FulfillmentResult<IncidentView>.Ok(View));
        public Task<FulfillmentResult<IncidentView>> ResolveAsync(long id, IncidentResolution request, Guid actor, CancellationToken ct) => Task.FromResult(FulfillmentResult<IncidentView>.Ok(View));
    }

    private sealed class IdentityStub(string role) : IIdentityAuthentication
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public Task<CurrentAccount?> ValidateSessionAsync(Guid user, Guid session, CancellationToken ct) => Task.FromResult<CurrentAccount?>(
            new(UserId, "test", null, [role], role == "Admin" ? [] : role == "Manager"
                ? ["fulfillment.incidents.report", "fulfillment.incidents.approve"] : ["fulfillment.incidents.report"]));
        public Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<LoginResult?> RefreshAsync(string? token, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> LogoutAsync(Guid user, Guid session, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> LogoutByRefreshTokenAsync(string? token, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> ChangePasswordAsync(Guid user, Guid session, ChangePasswordRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
