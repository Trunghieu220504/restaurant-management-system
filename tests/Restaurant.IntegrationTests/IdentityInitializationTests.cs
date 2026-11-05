using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class IdentityInitializationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Permission_tables_have_module_local_foreign_keys()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("[identity].[Permission]", script);
        Assert.Contains("[identity].[RolePermission]", script);
        Assert.Contains("[identity].[AdminBootstrap]", script);
        Assert.All(db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()),
            fk => Assert.Equal("identity", fk.PrincipalEntityType.GetSchema()));
    }

    [Fact]
    public void Seed_has_five_roles_no_accounts_and_least_privilege_admin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var roles = model.GetEntityTypes().Single(e => e.GetTableName() == "AspNetRoles").GetSeedData().ToArray();
        Assert.Equal(new[] { "Admin", "Bar", "Kitchen", "Manager", "Staff" },
            roles.Select(r => (string)r["Name"]!).Order().ToArray());
        Assert.Empty(model.GetEntityTypes().Single(e => e.GetTableName() == "AspNetUsers").GetSeedData());
        var adminId = roles.Single(r => (string)r["Name"]! == "Admin")["Id"];
        var grants = model.GetEntityTypes().Single(e => e.GetTableName() == "RolePermission").GetSeedData().ToArray();
        var adminGrants = grants.Where(g => Equals(g["RoleId"], adminId)).Select(g => (string)g["PermissionCode"]!).ToArray();
        Assert.NotEmpty(adminGrants);
        Assert.All(adminGrants, code => Assert.StartsWith("identity.", code));
        Assert.DoesNotContain(grants.Where(g => !Equals(g["RoleId"], adminId)),
            g => (string)g["PermissionCode"]! == "identity.roles.manage");
    }

    [Fact]
    public void Initial_migration_generates_idempotent_sql_without_accounts()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Single(db.Database.GetMigrations());
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("IF NOT EXISTS", script);
        Assert.Contains("[identity].[Permission]", script);
        Assert.DoesNotContain("INSERT INTO [identity].[AspNetUsers]", script);
    }
}
