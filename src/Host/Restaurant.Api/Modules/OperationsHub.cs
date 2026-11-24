using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Identity.Application.Authentication;

namespace Restaurant.Api.Modules;

public sealed class OperationsConnections
{
    public sealed record Connection(Guid UserId, Guid SessionId, DateTimeOffset ExpiresAt)
    {
        public ConcurrentDictionary<string, byte> Groups { get; } = new(StringComparer.Ordinal);
    }

    public ConcurrentDictionary<string, Connection> Active { get; } = new(StringComparer.Ordinal);
    public static string Station(string station) => $"station:{station}";
    public static string Order(long id) => $"order:{id}";
    public const string Serving = "serving";
}

[Authorize]
public sealed class OperationsHub(OperationsConnections connections, IIdentityAuthentication identity, TimeProvider clock) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        if (!Guid.TryParse(user.FindFirst("sub")?.Value, out var userId) ||
            !Guid.TryParse(user.FindFirst("sid")?.Value, out var sessionId) ||
            !long.TryParse(user.FindFirst("exp")?.Value, out var expiration))
            throw new HubException("operations.session.invalid");
        var connection = new OperationsConnections.Connection(userId, sessionId, DateTimeOffset.FromUnixTimeSeconds(expiration));
        connections.Active[Context.ConnectionId] = connection;
        try
        {
            var current = await CurrentUser();
            var permitted = Stations.All.Where(s => OperationsAccess.CanReadStation(current, s)).ToArray();
            if (permitted.Length == 0) throw new HubException("operations.access.denied");
            foreach (var station in permitted) await Join(OperationsConnections.Station(station));
            if (OperationsAccess.CanServe(current)) await Join(OperationsConnections.Serving);
            await base.OnConnectedAsync();
        }
        catch
        {
            connections.Active.TryRemove(Context.ConnectionId, out _);
            throw;
        }
    }

    public async Task JoinStation(string station)
    {
        if (!OperationsAccess.CanReadStation(await CurrentUser(), station)) throw new HubException("operations.access.denied");
        await Join(OperationsConnections.Station(station));
    }

    public async Task JoinServing()
    {
        if (!OperationsAccess.CanServe(await CurrentUser())) throw new HubException("operations.access.denied");
        await Join(OperationsConnections.Serving);
    }

    public async Task JoinOrder(long orderId)
    {
        if (orderId <= 0) throw new HubException("operations.order.invalid");
        if (!OperationsAccess.CanServe(await CurrentUser())) throw new HubException("operations.access.denied");
        await Join(OperationsConnections.Order(orderId));
    }

    public async Task LeaveOrder(long orderId)
    {
        if (connections.Active.TryGetValue(Context.ConnectionId, out var connection))
            connection.Groups.TryRemove(OperationsConnections.Order(orderId), out _);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, OperationsConnections.Order(orderId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Active.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task Join(string group)
    {
        var connection = connections.Active[Context.ConnectionId];
        if (connection.Groups.Count >= 100 && !connection.Groups.ContainsKey(group))
            throw new HubException("operations.subscriptions.limit");
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        connection.Groups.TryAdd(group, 0);
    }

    private async Task<ClaimsPrincipal> CurrentUser()
    {
        if (!connections.Active.TryGetValue(Context.ConnectionId, out var connection) || connection.ExpiresAt <= clock.GetUtcNow())
            throw new HubException("operations.session.invalid");
        var account = await identity.ValidateSessionAsync(connection.UserId, connection.SessionId, Context.ConnectionAborted);
        if (account is null) throw new HubException("operations.session.invalid");
        return Principal(account);
    }

    internal static ClaimsPrincipal Principal(CurrentAccount account) =>
        new(new ClaimsIdentity(account.Roles.Select(role => new Claim("role", role)), "operations", "sub", "role"));
}
