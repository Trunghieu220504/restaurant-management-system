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
        group.MapPost("/login", async (LoginRequest request, HttpContext context, IIdentityAuthentication service, CancellationToken ct) =>
        {
            if (!AuthenticationInput.IsValid(request)) return Results.Problem(statusCode: 400, title: "Invalid login input.");
            var result = await service.LoginAsync(request, ct);
            if (result is null) return Results.Problem(statusCode: 401, title: "Unable to sign in with the supplied credentials.");
            SetRefreshCookie(context, result);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("identity-login").WithName("IdentityLogin")
            .Produces<LoginResult>(200).ProducesProblem(400).ProducesProblem(401).Produces(429);

        group.MapPost("/refresh", async (HttpContext context, IIdentityAuthentication service, CancellationToken ct) =>
        {
            var result = await service.RefreshAsync(context.Request.Cookies[RefreshCookieName], ct);
            // Do not clear the cookie on failure: a second tab racing a rotation would otherwise delete the new cookie.
            if (result is null) return Results.Problem(statusCode: 401, title: "Unable to refresh the session.");
            SetRefreshCookie(context, result);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("identity-refresh").WithName("IdentityRefresh")
            .Produces<LoginResult>(200).ProducesProblem(401).Produces(429);

        group.MapGet("/me", (HttpContext context) =>
            context.Items.TryGetValue(typeof(CurrentAccount), out var account) && account is CurrentAccount current
                ? Results.Ok(current) : Results.Unauthorized()).WithName("IdentityCurrentAccount")
            .Produces<CurrentAccount>(200).Produces(401);

        group.MapPost("/logout", async (HttpContext context, IIdentityAuthentication service, CancellationToken ct) =>
        {
            var result = context.User.Identity?.IsAuthenticated == true && TryGetSession(context, out var userId, out var sessionId)
                ? await service.LogoutAsync(userId, sessionId, ct)
                : await service.LogoutByRefreshTokenAsync(context.Request.Cookies[RefreshCookieName], ct);
            var success = result == AccountMutationResult.Success;
            if (success) ClearRefreshCookie(context);
            return success ? Results.NoContent() : Results.Unauthorized();
        }).AllowAnonymous().RequireRateLimiting("identity-refresh").WithName("IdentityLogout").Produces(204).Produces(401).Produces(429);

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context,
            IIdentityAuthentication service, CancellationToken ct) =>
        {
            if (!TryGetSession(context, out var userId, out var sessionId)) return Results.Unauthorized();
            if (!AuthenticationInput.IsValid(request)) return Results.Problem(statusCode: 400, title: "Invalid password input.");
            return await service.ChangePasswordAsync(userId, sessionId, request, ct) switch
            {
                AccountMutationResult.Success => ClearedNoContent(context),
                AccountMutationResult.Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(statusCode: 400, title: "Password change rejected. Check the current password and new password policy.")
            };
        }).RequireRateLimiting("identity-password").WithName("IdentityChangePassword")
            .Produces(204).ProducesProblem(400).Produces(401).Produces(429);
        return group;
    }

    public const string RefreshCookieName = "rt";
    private const string RefreshCookiePath = "/api/identity";

    private static CookieOptions RefreshCookieOptions(DateTime? expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = true, // explicit: TLS is terminated by the proxy, so do not rely on the request scheme
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expiresUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc)),
        IsEssential = true
    };

    private static void SetRefreshCookie(HttpContext context, LoginResult result)
    {
        if (result.RefreshToken is null) return;
        context.Response.Cookies.Append(RefreshCookieName, result.RefreshToken, RefreshCookieOptions(result.RefreshTokenExpiresAt));
    }

    private static void ClearRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(null));

    private static IResult ClearedNoContent(HttpContext context)
    {
        ClearRefreshCookie(context);
        return Results.NoContent();
    }

    private static bool TryGetSession(HttpContext context, out Guid userId, out Guid sessionId)
    {
        sessionId = default;
        return Guid.TryParse(context.User.FindFirst("sub")?.Value, out userId) &&
            Guid.TryParse(context.User.FindFirst("sid")?.Value, out sessionId);
    }
}
