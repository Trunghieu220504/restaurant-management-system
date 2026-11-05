namespace Restaurant.Modules.Identity.Application.Authentication;

public sealed record LoginRequest(string? UserName, string? Password)
{
    public override string ToString() => "LoginRequest (redacted)";
}
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword)
{
    public override string ToString() => "ChangePasswordRequest (redacted)";
}
public sealed record CurrentAccount(Guid Id, string UserName, string? Email, string[] Roles, string[] Permissions);
public sealed record LoginResult(string AccessToken, DateTime ExpiresAt, CurrentAccount Account)
{
    public override string ToString() => "LoginResult (redacted)";
}
public enum AccountMutationResult { Success, Unauthorized, InvalidPassword }

public interface IIdentityAuthentication
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<CurrentAccount?> ValidateSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AccountMutationResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AccountMutationResult> ChangePasswordAsync(Guid userId, Guid sessionId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public interface IAccessTokenIssuer
{
    string Issue(Guid userId, Guid sessionId, string[] roles, DateTime issuedAt, DateTime expiresAt);
}

public static class AuthenticationInput
{
    public static bool IsValid(LoginRequest request) =>
        !string.IsNullOrWhiteSpace(request.UserName) && request.UserName.Length <= 256 &&
        request.UserName == request.UserName.Trim() && IsPasswordShapeValid(request.Password);

    public static bool IsValid(ChangePasswordRequest request) =>
        IsPasswordShapeValid(request.CurrentPassword) && IsPasswordShapeValid(request.NewPassword);

    private static bool IsPasswordShapeValid(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024;
}
