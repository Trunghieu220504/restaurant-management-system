using System.Text.Json.Serialization;

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
    /// <summary>Raw refresh secret. Never serialized: the endpoint puts it in an HttpOnly cookie.</summary>
    [JsonIgnore] public string? RefreshToken { get; init; }
    [JsonIgnore] public DateTime? RefreshTokenExpiresAt { get; init; }
    public override string ToString() => "LoginResult (redacted)";
}
public enum AccountMutationResult { Success, Unauthorized, InvalidPassword }

public interface IIdentityAuthentication
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    /// <summary>Rotates the refresh token. Null means 401 (unknown, expired, revoked or reused token).</summary>
    Task<LoginResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<CurrentAccount?> ValidateSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AccountMutationResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AccountMutationResult> LogoutByRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken);
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
