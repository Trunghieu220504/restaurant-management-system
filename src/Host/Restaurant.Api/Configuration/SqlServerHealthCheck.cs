using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Restaurant.Api.Configuration;

public sealed class SqlServerHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = new SqlConnectionStringBuilder(
            configuration.GetConnectionString("Restaurant"))
        { ConnectTimeout = 3 };
        try
        {
            await using var connection = new SqlConnection(connectionString.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (SqlException)
        {
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}
