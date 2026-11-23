namespace Restaurant.Modules.Identity.Application.Accounts;

/// <summary>The five fixed roles. Only the Admin can hand out Admin or Manager; a Manager works with the subordinate roles.</summary>
public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";
    public const string Kitchen = "Kitchen";
    public const string Bar = "Bar";

    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Staff, Kitchen, Bar];

    /// <summary>The roles a Manager may give out and the only roles an account may hold for a Manager to manage it.</summary>
    public static IReadOnlyList<string> Subordinate { get; } = [Staff, Kitchen, Bar];
}

public sealed record AccountDto(Guid Id, string UserName, string? Email, string[] Roles, bool Active, bool LockedOut);

public sealed record AccountPage(IReadOnlyList<AccountDto> Items, int Total);

/// <param name="Search">Part of the user name or e-mail.</param>
/// <param name="Role">Only accounts holding this role.</param>
/// <param name="Active">Only active (true) or only disabled (false) accounts.</param>
public sealed record AccountListQuery(string? Search, string? Role, bool? Active, int Offset = 0, int Limit = 50)
{
    public const int MaxLimit = 200;
}

public sealed record CreateAccountRequest(string? UserName, string? Email, string? Password, string[]? Roles)
{
    public override string ToString() => "CreateAccountRequest (redacted)";
}

public sealed record SetRolesRequest(string[]? Roles);

public sealed record ResetPasswordRequest(string? NewPassword)
{
    public override string ToString() => "ResetPasswordRequest (redacted)";
}

/// <summary>Stable error codes, <c>identity.account.&lt;reason&gt;</c>.</summary>
public static class AccountErrors
{
    public const string InputInvalid = "identity.account.input_invalid";
    public const string PasswordInvalid = "identity.account.password_invalid";
    public const string NotFound = "identity.account.not_found";
    /// <summary>The caller has no account-management permission (or lost it since the token was issued).</summary>
    public const string Forbidden = "identity.account.forbidden";
    /// <summary>A Manager tried to touch an account that holds Admin, Manager or no subordinate role at all.</summary>
    public const string OutOfScope = "identity.account.out_of_scope";
    /// <summary>A Manager tried to hand out Admin or Manager.</summary>
    public const string RoleNotAllowed = "identity.account.role_not_allowed";
    /// <summary>Roles, activation and password reset cannot be applied to one's own account.</summary>
    public const string SelfChange = "identity.account.self_change";
    public const string UserNameTaken = "identity.account.user_name_taken";
    public const string EmailTaken = "identity.account.email_taken";
}

public sealed class AccountResult<T> where T : class
{
    private AccountResult(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public string? Error { get; }
    public bool Succeeded => Error is null;

    public static AccountResult<T> Ok(T value) => new(value, null);
    public static AccountResult<T> Fail(string error) => new(null, error);
}

/// <summary>
/// Account management for Admins (every account) and Managers (subordinates only). The caller is always an account id taken
/// from the validated token; what the caller may do is read from the database inside each call, not from the token.
/// </summary>
public interface IAccountManagement
{
    Task<AccountResult<AccountPage>> ListAsync(Guid actorId, AccountListQuery query, CancellationToken cancellationToken);
    Task<AccountResult<AccountDto>> GetAsync(Guid actorId, Guid accountId, CancellationToken cancellationToken);
    Task<AccountResult<AccountDto>> CreateAsync(Guid actorId, CreateAccountRequest request, CancellationToken cancellationToken);
    Task<AccountResult<AccountDto>> SetRolesAsync(Guid actorId, Guid accountId, SetRolesRequest request, CancellationToken cancellationToken);
    Task<AccountResult<AccountDto>> SetActiveAsync(Guid actorId, Guid accountId, bool active, CancellationToken cancellationToken);
    /// <summary>Sets a new password, ends every session of the account and clears its lockout.</summary>
    Task<AccountResult<AccountDto>> ResetPasswordAsync(Guid actorId, Guid accountId, ResetPasswordRequest request, CancellationToken cancellationToken);
}
