using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Restaurant.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Test-only material. Never use this key in a running deployment.
    public const string SigningKey = "test-only-key-restaurant-integration-0123456789";
    public const string ConnectionString =
        "Server=127.0.0.1,1;Database=RestaurantIntegration;User Id=test;Password=test;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Restaurant"] = ConnectionString,
                ["Jwt:Issuer"] = "restaurant-tests",
                ["Jwt:Audience"] = "restaurant-tests-client",
                ["Jwt:SigningKey"] = SigningKey
            }));
    }
}
