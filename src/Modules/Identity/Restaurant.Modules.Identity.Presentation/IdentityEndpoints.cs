using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Identity.Application.Authentication;

namespace Restaurant.Modules.Identity.Presentation;

public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity")
            .WithTags("Identity")
            .RequireAuthorization();
        group.MapPost("/login", async (LoginRequest request, IIdentityAuthentication service, CancellationToken ct) =>
        {
            if (!AuthenticationInput.IsValid(request)) return Results.Problem(statusCode: 400, title: "Invalid login input.");
            var result = await service.LoginAsync(request, ct);
            return result is null ? Results.Problem(statusCode: 401, title: "Unable to sign in with the supplied credentials.") : Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("identity-login").WithName("IdentityLogin")
            .Produces<LoginResult>(200).ProducesProblem(400).ProducesProblem(401).Produces(429);

        group.MapGet("/me", (HttpContext context) =>
            context.Items.TryGetValue(typeof(CurrentAccount), out var account) && account is CurrentAccount current
                ? Results.Ok(current) : Results.Unauthorized()).WithName("IdentityCurrentAccount")
            .Produces<CurrentAccount>(200).Produces(401);

        group.MapPost("/logout", async (HttpContext context, IIdentityAuthentication service, CancellationToken ct) =>
        {
            if (!TryGetSession(context, out var userId, out var sessionId)) return Results.Unauthorized();
            return await service.LogoutAsync(userId, sessionId, ct) == AccountMutationResult.Success
                ? Results.NoContent() : Results.Unauthorized();
        }).WithName("IdentityLogout").Produces(204).Produces(401);

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context,
            IIdentityAuthentication service, CancellationToken ct) =>
        {
            if (!TryGetSession(context, out var userId, out var sessionId)) return Results.Unauthorized();
            if (!AuthenticationInput.IsValid(request)) return Results.Problem(statusCode: 400, title: "Invalid password input.");
            return await service.ChangePasswordAsync(userId, sessionId, request, ct) switch
            {
                AccountMutationResult.Success => Results.NoContent(),
                AccountMutationResult.Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(statusCode: 400, title: "Password change rejected. Check the current password and new password policy.")
            };
        }).RequireRateLimiting("identity-password").WithName("IdentityChangePassword")
            .Produces(204).ProducesProblem(400).Produces(401).Produces(429);
        return group;
    }

    private static bool TryGetSession(HttpContext context, out Guid userId, out Guid sessionId)
    {
        sessionId = default;
        return Guid.TryParse(context.User.FindFirst("sub")?.Value, out userId) &&
            Guid.TryParse(context.User.FindFirst("sid")?.Value, out sessionId);
    }
}
