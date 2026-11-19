#:project ../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Restaurant.Modules.Identity.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

var output = Environment.GetEnvironmentVariable("WEEK1_PROVISION_FILE")
    ?? throw new InvalidOperationException("Set an ignored, private WEEK1_PROVISION_FILE path.");
if (File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite existing test credentials.");
var run = DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
var hasher = new PasswordHasher<IdentityUser<Guid>>();
object Account(string suffix, string role)
{
    var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "w1-e2e-" + run + "-" + suffix };
    var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
    var passwordHash = hasher.HashPassword(user, password);
    if (hasher.VerifyHashedPassword(user, passwordHash, password) == PasswordVerificationResult.Failed)
        throw new InvalidOperationException("Password hash self-check failed.");
    return new { id = user.Id, userName = user.UserName, role, password, passwordHash };
}
var session = new
{
    environment = "staging",
    accounts = new { manager = Account("manager", "Manager"), staff = Account("staff", "Staff"), lockout = Account("lockout", "Staff") }
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(session));
Console.WriteLine("Dedicated test credentials generated privately; hashes self-checked. No staging database changed.");
