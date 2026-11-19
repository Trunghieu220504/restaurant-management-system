#:project ../tests/Restaurant.IntegrationTests/Restaurant.IntegrationTests.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.IntegrationTests;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;

// Local acceptance host only. Never accepts an existing database or a remote SQL server.
var sessionPath = Environment.GetEnvironmentVariable("WEEK1_SESSION_FILE")
    ?? throw new InvalidOperationException("Set WEEK1_SESSION_FILE to an ignored local artifact path.");
var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")
    ?? "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5";
var source = new SqlConnectionStringBuilder(supplied);
if (!Regex.IsMatch(source.DataSource, @"^(localhost|127\.0\.0\.1|\.)(\\[A-Za-z0-9_-]+)?(,[0-9]{1,5})?$", RegexOptions.IgnoreCase) ||
    (source.InitialCatalog.Length > 0 && source.InitialCatalog != "master") ||
    source.AttachDBFilename.Length > 0 || source.FailoverPartner.Length > 0 || source.UserInstance)
    throw new InvalidOperationException("Only a loopback SQL instance without an application database is allowed.");
var database = "RestaurantWeekOneBrowser_" + Guid.NewGuid().ToString("N");
await using (var probe = new SqlConnection(new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
{
    await probe.OpenAsync();
    await using var version = new SqlCommand("SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)", probe);
    if (Convert.ToInt32(await version.ExecuteScalarAsync()) < 16)
        throw new InvalidOperationException("Browser acceptance requires SQL Server 2022 or newer.");
}
source.InitialCatalog = database;
var connection = source.ConnectionString;
var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
// File-based builds run from a temporary directory outside the solution tree.
var contentRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "Host", "Restaurant.Api");
if (!File.Exists(Path.Combine(contentRoot, "Restaurant.Api.csproj")))
    throw new InvalidOperationException("Run from the repository root.");
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_RESTAURANT_API", contentRoot);
await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
{
    builder.UseEnvironment("Testing");
    builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Restaurant"] = connection,
        ["Jwt:SigningKey"] = key,
        ["Logging:LogLevel:Default"] = "Warning"
    }));
});
factory.UseKestrel(0);
using var client = factory.CreateClient();
try
{
    await using var scope = factory.Services.CreateAsyncScope();
    var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await identity.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SalesDayDbContext>().Database.MigrateAsync();
    var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
    async Task<object> CreateUser(string suffix, string role)
    {
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(), UserName = "w1-e2e-" + suffix,
            Email = "w1-e2e-" + suffix + "@example.invalid", LockoutEnabled = true
        };
        if (!(await manager.CreateAsync(user, password)).Succeeded || !(await manager.AddToRoleAsync(user, role)).Succeeded)
            throw new InvalidOperationException("Test account creation failed.");
        return new { userName = user.UserName, password };
    }
    var accounts = new
    {
        manager = await CreateUser("manager", "Manager"),
        staff = await CreateUser("staff", "Staff"),
        lockout = await CreateUser("lockout", "Staff")
    };
    var session = new { environment = "isolated-local", apiUrl = client.BaseAddress!.ToString(), accounts };
    await File.WriteAllTextAsync(sessionPath, JsonSerializer.Serialize(session));
    Console.WriteLine("WEEK1_HOST_READY (loopback API; private session file; generated test database only)");
    var stopFile = sessionPath + ".stop";
    var expires = DateTime.UtcNow.AddMinutes(45);
    while (!File.Exists(stopFile) && DateTime.UtcNow < expires) await Task.Delay(1000);
    if (File.Exists(stopFile)) File.Delete(stopFile);
}
finally
{
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
    if (target != database || !target.StartsWith("RestaurantWeekOneBrowser_", StringComparison.Ordinal))
        throw new InvalidOperationException("Unsafe browser-test cleanup target.");
    await db.Database.EnsureDeletedAsync();
    File.Delete(sessionPath);
    Console.WriteLine("WEEK1_HOST_CLEANED (only its generated database)");
}
