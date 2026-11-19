using Microsoft.AspNetCore.Authorization;
using Restaurant.Modules.Identity.Application.Authorization;

namespace Restaurant.Api.Configuration;

public static class PermissionAuthorization
{
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder authorization)
    {
        foreach (var code in PermissionCodes.All)
            authorization.AddPolicy(code, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", code));
        return authorization;
    }

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        if (!PermissionCodes.All.Contains(permission, StringComparer.Ordinal))
            throw new ArgumentException("Unknown permission code.", nameof(permission));
        return builder.RequireAuthorization(permission);
    }
}
