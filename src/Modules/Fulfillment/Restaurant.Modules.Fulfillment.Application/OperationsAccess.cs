using System.Security.Claims;
using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Application;

public static class OperationsAccess
{
    public static bool CanServe(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Staff") || user.IsInRole("Manager"));

    public static bool CanReadStation(ClaimsPrincipal user, string station) =>
        Stations.All.Contains(station) && user.Identity?.IsAuthenticated == true &&
        (CanServe(user) || user.IsInRole(station == Stations.Kitchen ? "Kitchen" : "Bar"));
}
