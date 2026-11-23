using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Identity.Application.Accounts;
using Restaurant.Modules.Identity.Application.Authorization;

namespace Restaurant.Modules.Identity.Presentation;

/// <summary>HIE-5.2. Which accounts the caller may touch is decided by the service from the database, not here.</summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder identity)
    {
        var group = identity.MapGroup("/accounts").RequireAuthorization(policy => policy.RequireAssertion(context =>
            context.User.HasClaim("permission", PermissionCodes.IdentityAccountsManage) ||
            context.User.HasClaim("permission", PermissionCodes.IdentitySubordinatesManage)));

        group.MapGet("/", async (HttpContext http, IAccountManagement accounts, string? search, string? role, bool? active,
                int? offset, int? limit, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized()
                : Respond(await accounts.ListAsync(actor, new AccountListQuery(search, role, active, offset ?? 0, limit ?? 50), ct)))
            .WithName("IdentityListAccounts").Produces<AccountPage>(200).ProducesProblem(400).Produces(401).Produces(403);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IAccountManagement accounts, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized() : Respond(await accounts.GetAsync(actor, id, ct)))
            .WithName("IdentityGetAccount").Produces<AccountDto>(200).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/", async (CreateAccountRequest request, HttpContext http, IAccountManagement accounts, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized()
                : Respond(await accounts.CreateAsync(actor, request, ct), created => $"/api/identity/accounts/{created.Id}"))
            .WithName("IdentityCreateAccount").Produces<AccountDto>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409);

        group.MapPut("/{id:guid}/roles", async (Guid id, SetRolesRequest request, HttpContext http, IAccountManagement accounts,
                CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized() : Respond(await accounts.SetRolesAsync(actor, id, request, ct)))
            .WithName("IdentitySetAccountRoles").Produces<AccountDto>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/{id:guid}/activate", async (Guid id, HttpContext http, IAccountManagement accounts, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized() : Respond(await accounts.SetActiveAsync(actor, id, true, ct)))
            .WithName("IdentityActivateAccount").Produces<AccountDto>(200).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, HttpContext http, IAccountManagement accounts, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized() : Respond(await accounts.SetActiveAsync(actor, id, false, ct)))
            .WithName("IdentityDeactivateAccount").Produces<AccountDto>(200).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, HttpContext http,
                IAccountManagement accounts, CancellationToken ct) =>
            !TryActor(http, out var actor) ? Results.Unauthorized()
                : await accounts.ResetPasswordAsync(actor, id, request, ct) is { Error: { } error } ? Problem(error) : Results.NoContent())
            .WithName("IdentityResetAccountPassword").Produces(204).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);

        return identity;
    }

    private static bool TryActor(HttpContext http, out Guid actor) =>
        Guid.TryParse(http.User.FindFirst("sub")?.Value, out actor) && actor != Guid.Empty;

    private static IResult Respond<T>(AccountResult<T> result, Func<T, string>? created = null) where T : class
    {
        if (result.Error is not null) return Problem(result.Error);
        return created is null ? Results.Ok(result.Value) : Results.Created(created(result.Value!), result.Value);
    }

    private static int StatusOf(string code) => code switch
    {
        AccountErrors.InputInvalid or AccountErrors.PasswordInvalid => 400,
        AccountErrors.NotFound => 404,
        AccountErrors.UserNameTaken or AccountErrors.EmailTaken => 409,
        _ => 403 // forbidden, out_of_scope, role_not_allowed, self_change
    };

    private static IResult Problem(string code) => Results.Problem(statusCode: StatusOf(code), title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
