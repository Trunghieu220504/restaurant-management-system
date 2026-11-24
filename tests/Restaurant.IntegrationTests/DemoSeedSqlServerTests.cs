using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Restaurant.Api.Configuration;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class DemoSeedSqlServerTests
{
    [Theory]
    [InlineData("RestaurantManagement", "Testing")]
    [InlineData("master", "Development")]
    [InlineData("RestaurantDemo_manual", "Staging")]
    [InlineData("RestaurantDemo_0123456789abcdef0123456789abcdef", "Production")]
    public void Demo_commands_refuse_application_databases_and_production(string database, string environment) =>
        Assert.Throws<InvalidOperationException>(() => DemoSeedCommand.ValidateTarget($"Server=localhost;Database={database};Integrated Security=True", environment));

    [SqlServerFact]
    public async Task Seed_twice_preserves_rows_and_reset_only_deletes_its_marked_database_under_five_minutes()
    {
        var database = "RestaurantDemo_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")!) { InitialCatalog = database }.ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Restaurant"] = connection,
            ["DemoSeed:Password"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!"
        }).Build();
        await using var cleanup = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).Options);
        var timer = Stopwatch.StartNew();
        try
        {
            for (var iteration = 0; iteration < 2; iteration++)
            {
                await DemoSeedCommand.ExecuteAsync("--seed-demo", configuration, "Testing");
                await using var sql = new SqlConnection(connection);
                await sql.OpenAsync();
                foreach (var (table, expected) in new[] { ("[identity].AspNetUsers", 5), ("seating.Ban", 2), ("catalog.Mon", 2), ("inventory.NguyenLieu", 2) })
                {
                    await using var command = sql.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM {table}";
                    Assert.Equal(expected, Convert.ToInt32(await command.ExecuteScalarAsync()));
                }
            }
            await DemoSeedCommand.ExecuteAsync("--reset-demo", configuration, "Testing");
            Assert.False(await cleanup.Database.CanConnectAsync());
            // A matching name is insufficient: existing data without the seed marker must survive.
            await cleanup.Database.EnsureCreatedAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeedCommand.ExecuteAsync("--reset-demo", configuration, "Testing"));
            Assert.True(await cleanup.Database.CanConnectAsync());
            Assert.True(timer.Elapsed < TimeSpan.FromMinutes(5), "Seed twice and reset exceeded five minutes.");
        }
        finally
        {
            Assert.Equal(database, new SqlConnectionStringBuilder(connection).InitialCatalog);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
