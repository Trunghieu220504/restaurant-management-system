using System.Net.Mail;

namespace Restaurant.Modules.Identity.Infrastructure.Initialization;

public sealed class AdminBootstrapInput
{
    public string UserName { get; }
    public string Email { get; }
    public string Password { get; }

    public AdminBootstrapInput(string? userName, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 256 || userName != userName.Trim() ||
            string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            string.IsNullOrWhiteSpace(password) || password.Length > 1024)
        {
            throw new ArgumentException("Bootstrap requires a valid username, email and password from environment secrets.");
        }
        UserName = userName;
        Email = email;
        Password = password;
    }

    public override string ToString() => "Admin bootstrap input (redacted)";
}
