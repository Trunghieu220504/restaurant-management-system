using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SqlTestDatabaseTests
{
    [Theory]
    [InlineData("master")]
    [InlineData("RestaurantManagement")]
    [InlineData("RestaurantProduction")]
    [InlineData("RestaurantTest_not-a-generated-id")]
    public void Cleanup_refuses_an_application_database(string database) =>
        Assert.Throws<InvalidOperationException>(() =>
            SqlTestDatabase.ClearPool($"Server=localhost;Database={database};Integrated Security=True"));

    [Fact]
    public void Cleanup_accepts_only_its_generated_database_without_opening_a_connection() =>
        SqlTestDatabase.ClearPool($"Server=localhost;Database=RestaurantTest_{Guid.NewGuid():N};Integrated Security=True");
}
