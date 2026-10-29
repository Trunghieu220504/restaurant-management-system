using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Workforce.Infrastructure.Persistence;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Reporting.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<Type, string> Contexts => new()
    {
        { typeof(IdentityDbContext), "identity" },
        { typeof(SeatingDbContext), "seating" },
        { typeof(CatalogDbContext), "catalog" },
        { typeof(SalesDbContext), "sales" },
        { typeof(FulfillmentDbContext), "fulfillment" },
        { typeof(BillingDbContext), "billing" },
        { typeof(InventoryDbContext), "inventory" },
        { typeof(WorkforceDbContext), "workforce" },
        { typeof(AuditDbContext), "audit" },
        { typeof(ReportingDbContext), "reporting" },
    };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Context_owns_its_schema_and_migration_history(Type contextType, string schema)
    {
        using var scope = factory.Services.CreateScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.Equal(schema, context.Model.GetDefaultSchema());
        Assert.All(context.Model.GetEntityTypes(), entity => Assert.Equal(schema, entity.GetSchema()));
        var options = context.GetService<IDbContextOptions>();
        var relational = options.Extensions.OfType<RelationalOptionsExtension>().Single();
        Assert.Equal(schema, relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal(contextType.Assembly.FullName, relational.MigrationsAssembly);
    }

    [Fact]
    public void Identity_has_real_identity_tables_without_connecting_to_database()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("[identity].[AspNetUsers]", script);
        Assert.Contains("[identity].[AspNetRoles]", script);
        Assert.Contains("[identity].[AspNetUserTokens]", script);
        Assert.Equal(7, context.Model.GetEntityTypes().Count());
    }

    [Fact]
    public void All_modules_have_exactly_one_distinct_context()
    {
        Assert.Equal(ModuleRegistration.Names.Count, Contexts.Count);
        Assert.Equal(10, Contexts.Select(row => row[0]).Distinct().Count());
    }
}
