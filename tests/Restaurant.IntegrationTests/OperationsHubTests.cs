using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Identity.Application.Authentication;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class OperationsHubTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authenticated_socket_rejoins_after_disconnect_and_snapshot_recovers_current_state(bool queryToken)
    {
        var identity = new IdentityStub();
        var snapshot = new SnapshotStub();
        using var original = new ApiFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIdentityAuthentication>(identity);
            services.AddSingleton<IOperationsSnapshot>(snapshot);
        }));
        using var http = factory.CreateClient();
        var token = factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(identity.UserId, identity.SessionId,
            ["Kitchen"], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5));
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using (var wrongStation = await http.GetAsync("/api/fulfillment/snapshot?station=BAR"))
            Assert.Equal(HttpStatusCode.Forbidden, wrongStation.StatusCode);
        using (var order = await http.GetAsync("/api/fulfillment/snapshot?orderId=10"))
            Assert.Equal(HttpStatusCode.Forbidden, order.StatusCode);

        var client = factory.Server.CreateWebSocketClient();
        if (!queryToken) client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
        var socketUri = new Uri("ws://localhost/hubs/operations" + (queryToken ? "?access_token=" + Uri.EscapeDataString(token) : ""));
        using (var socket = await client.ConnectAsync(socketUri, default))
        {
            await Send(socket, "{\"protocol\":\"json\",\"version\":1}");
            Assert.Equal("{}", await Receive(socket));
            await Send(socket, "{\"type\":1,\"invocationId\":\"1\",\"target\":\"JoinStation\",\"arguments\":[\"BAR\"]}");
            Assert.Contains("operations.access.denied", await Receive(socket));
            await Send(socket, "{\"type\":1,\"invocationId\":\"2\",\"target\":\"JoinStation\",\"arguments\":[\"BEP\"]}");
            Assert.DoesNotContain("error", await Receive(socket));
            socket.Abort();
        }
        snapshot.Status = "SAN_SANG";
        using var reconnect = await client.ConnectAsync(socketUri, default);
        await Send(reconnect, "{\"protocol\":\"json\",\"version\":1}");
        Assert.Equal("{}", await Receive(reconnect));
        await Send(reconnect, "{\"type\":1,\"invocationId\":\"3\",\"target\":\"JoinStation\",\"arguments\":[\"BEP\"]}");
        Assert.DoesNotContain("error", await Receive(reconnect));
        var state = await http.GetFromJsonAsync<OperationsSnapshot>("/api/fulfillment/snapshot?station=BEP");
        Assert.Equal("SAN_SANG", Assert.Single(state!.Tickets).Status);
        identity.Roles = ["Bar"];
        await Send(reconnect, "{\"type\":1,\"invocationId\":\"4\",\"target\":\"JoinStation\",\"arguments\":[\"BEP\"]}");
        Assert.Contains("operations.access.denied", await Receive(reconnect));
        reconnect.Abort();
    }

    [Fact]
    public async Task Anonymous_negotiate_and_snapshot_are_rejected()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var negotiate = await http.PostAsync("/hubs/operations/negotiate?negotiateVersion=1", null);
        using var snapshot = await http.GetAsync("/api/fulfillment/snapshot");
        Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, snapshot.StatusCode);
    }

    internal static Task Send(WebSocket socket, string message) => socket.SendAsync(
        new ArraySegment<byte>(Encoding.UTF8.GetBytes(message + '\u001e')), WebSocketMessageType.Text, true, CancellationToken.None);

    internal static async Task<string> Receive(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        return Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd('\u001e');
    }

    private sealed class SnapshotStub : IOperationsSnapshot
    {
        public string Status { get; set; } = "DANG_LAM";
        public Task<OperationsSnapshot> ReadAsync(IReadOnlyCollection<string> stations, long? orderId, CancellationToken ct) =>
            Task.FromResult(new OperationsSnapshot([new TicketView(1, 42, "BEP", Status, DateTime.UtcNow,
                null, null, null, null, null, null, "AAAAAAAAAAE=")], []));
    }

    internal sealed class IdentityStub : IIdentityAuthentication
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public string[] Roles { get; set; } = ["Kitchen"];
        public Task<CurrentAccount?> ValidateSessionAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
            Task.FromResult(userId == UserId && sessionId == SessionId ? new CurrentAccount(UserId, "test", null, Roles, []) : null);
        public Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<LoginResult?> RefreshAsync(string? token, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> LogoutByRefreshTokenAsync(string? token, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountMutationResult> ChangePasswordAsync(Guid userId, Guid sessionId, ChangePasswordRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
