using Microsoft.Data.SqlClient;

namespace Restaurant.IntegrationTests;

internal static class SqlTestDatabase
{
    // Clearing every pool can discard another fixture's session-scoped migration lock.
    // Only release idle connections for the database owned by this fixture.
    internal static void ClearPool(string connectionString)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var separator = database.LastIndexOf('_');
        if (!database.StartsWith("Restaurant", StringComparison.Ordinal) || separator < 0 ||
            !Guid.TryParseExact(database[(separator + 1)..], "N", out _))
        {
            throw new InvalidOperationException("Only a generated test database pool may be cleared.");
        }
        using var connection = new SqlConnection(connectionString);
        SqlConnection.ClearPool(connection);
    }
}
