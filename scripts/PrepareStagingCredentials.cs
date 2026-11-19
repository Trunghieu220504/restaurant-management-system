#:project ../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Restaurant.Modules.Identity.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

var directory = Environment.GetEnvironmentVariable("STAGING_CREDENTIAL_DIRECTORY")
    ?? throw new InvalidOperationException("Set a private, ignored output directory.");
Directory.CreateDirectory(directory);
var credentialsPath = Path.Combine(directory, "credentials.json");
var provisioningPath = Path.Combine(directory, "provisioning.json");
if (File.Exists(credentialsPath) || File.Exists(provisioningPath))
    throw new InvalidOperationException("Refusing to overwrite credentials.");
var hasher = new PasswordHasher<IdentityUser<Guid>>();
var credentials = new List<object>();
var accounts = new List<object>();
foreach (var (name, role) in new[] { ("staging.manager", "Manager"), ("staging.staff", "Staff") })
{
    var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = name };
    var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
    var hash = hasher.HashPassword(user, password);
    if (hasher.VerifyHashedPassword(user, hash, password) == PasswordVerificationResult.Failed)
        throw new InvalidOperationException("Password hash self-check failed.");
    credentials.Add(new { userName = name, role, password });
    accounts.Add(new { id = user.Id, userName = name, role, passwordHash = hash });
}
await File.WriteAllTextAsync(credentialsPath, JsonSerializer.Serialize(new
{
    environment = "staging", origin = "https://staging.quanlynh.id.vn", status = "NOT_PROVISIONED", accounts = credentials
}, new JsonSerializerOptions { WriteIndented = true }));
await File.WriteAllTextAsync(provisioningPath, JsonSerializer.Serialize(new { environment = "staging", accounts }));
Console.WriteLine("Two staging credentials generated privately; hashes verified. No database changed.");
