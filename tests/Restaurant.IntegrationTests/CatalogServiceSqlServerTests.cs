using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>Catalog acceptance against the real migration and SQL Server rowversions, never an in-memory provider.</summary>
public sealed class CatalogServiceSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantCatalogApiTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (!databaseName.StartsWith("RestaurantCatalogApiTest_", StringComparison.Ordinal) ||
            new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName)
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        SqlTestDatabase.ClearPool(connectionString);
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    private CatalogDbContext NewContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new CatalogDbContext(options.Options);
    }

    private async Task<(MenuGroupDto Group, MenuItemDto Item, VariantDto Variant, OptionGroupDto Options, OptionDto Option)> SeedAsync()
    {
        await using var db = NewContext();
        var service = new CatalogMenu(db);
        var group = Ok(await service.CreateGroupAsync(new("Món chính", 1), default));
        var item = Ok(await service.CreateItemAsync(new(group.Id, "PHO", "Phở bò", "BEP", new("LON", "Tô lớn", 65000)), default));
        var variant = Assert.Single(item.Variants);
        var options = Ok(await service.CreateOptionGroupAsync(new("Thêm topping", "TOPPING"), default));
        var option = Ok(await service.CreateOptionAsync(options.Id, new("Thêm bò", 15000), default));
        Ok(await service.AssignOptionGroupAsync(item.Id, options.Id, new(0, 1), default));
        return (group, item, variant, options, option);
    }

    [SqlServerFact]
    public async Task Complete_dish_with_variant_and_topping_is_persisted_and_returned_by_menu()
    {
        var seeded = await SeedAsync();
        await using var db = NewContext();
        var menu = await new CatalogMenu(db).GetMenuAsync(default);
        var item = Assert.Single(menu.Items);
        Assert.Equal(seeded.Item.Id, item.Id);
        Assert.Equal(seeded.Group.Id, item.GroupId);
        Assert.Equal("Phở bò", item.Name);
        Assert.Null(item.ImageObjectKey);
        Assert.Equal(65000L, Assert.Single(item.Variants).Price);
        var assignment = Assert.Single(item.OptionGroups);
        Assert.Equal(seeded.Options.Id, assignment.OptionGroupId);
        Assert.Equal(0, assignment.MinSelections);
        Assert.Equal(1, assignment.MaxSelections);
        var topping = Assert.Single(Assert.Single(menu.OptionGroups).Options);
        Assert.Equal("Thêm bò", topping.Name);
        Assert.Equal(15000L, topping.ExtraPrice);
        Assert.Equal(8, Convert.FromBase64String(item.Version).Length);
        Assert.Equal(8, Convert.FromBase64String(topping.Version).Length);
    }

    [SqlServerFact]
    public async Task Ownership_invalid_ranges_and_negative_prices_are_rejected_without_partial_writes()
    {
        var seeded = await SeedAsync();
        await using var db = NewContext();
        var service = new CatalogMenu(db);
        var otherItem = Ok(await service.CreateItemAsync(new(seeded.Group.Id, "BUN", "Bún bò", "BEP", new("STD", "Chuẩn", 1000)), default));
        var otherGroup = Ok(await service.CreateOptionGroupAsync(new("Nước dùng", "TUY_CHON"), default));
        Assert.Equal(CatalogErrors.VariantNotFound, (await service.UpdateVariantAsync(otherItem.Id, seeded.Variant.Id,
            new("LON", "Sai món", 1, true, seeded.Variant.Version), default)).Error);
        Assert.Equal(CatalogErrors.OptionNotFound, (await service.UpdateOptionAsync(otherGroup.Id, seeded.Option.Id,
            new("Sai nhóm", 1, true, seeded.Option.Version), default)).Error);
        foreach (var range in new[] { new AssignOptionGroupRequest(-1, 1), new(2, 1), new(0, 0), new(0, 2), new(2, 2) })
            Assert.Equal(CatalogErrors.SelectionRangeInvalid,
                (await service.AssignOptionGroupAsync(seeded.Item.Id, seeded.Options.Id, range, default)).Error);
        Assert.Equal(CatalogErrors.InputInvalid,
            (await service.CreateVariantAsync(seeded.Item.Id, new("BAD", "Giá sai", -1), default)).Error);
        Assert.Equal(CatalogErrors.InputInvalid,
            (await service.CreateOptionAsync(seeded.Options.Id, new("Giá sai", -1), default)).Error);
        await using var verify = NewContext();
        Assert.Equal("Tô lớn", (await verify.Set<BienThe>().SingleAsync(x => x.Id == seeded.Variant.Id)).Ten);
        Assert.Equal("Thêm bò", (await verify.Set<TuyChon>().SingleAsync()).Ten);
        Assert.Equal(1, (await verify.Set<MonNhomTuyChon>().SingleAsync()).ChonToiDa);
    }

    [SqlServerFact]
    public async Task Stale_versions_and_duplicate_codes_preserve_the_current_entity()
    {
        var seeded = await SeedAsync();
        await using var first = NewContext();
        var firstService = new CatalogMenu(first);
        var changed = Ok(await firstService.UpdateItemAsync(seeded.Item.Id,
            new(seeded.Group.Id, "PHO", "Phở đặc biệt", "BEP", true, seeded.Item.Version), default));
        Assert.NotEqual(seeded.Item.Version, changed.Version);
        await using var second = NewContext();
        var secondService = new CatalogMenu(second);
        Assert.Equal(CatalogErrors.ConcurrencyConflict, (await secondService.UpdateItemAsync(seeded.Item.Id,
            new(seeded.Group.Id, "PHO", "Ghi đè cũ", "BAR", true, seeded.Item.Version), default)).Error);
        Assert.Equal(CatalogErrors.ItemCodeConflict,
            (await secondService.CreateItemAsync(new(seeded.Group.Id, "PHO", "Trùng mã", "BEP", new("STD", "Chuẩn", 1000)), default)).Error);
        Assert.Equal(CatalogErrors.VariantCodeConflict,
            (await secondService.CreateVariantAsync(seeded.Item.Id, new("LON", "Trùng mã", 1000), default)).Error);
        var current = Assert.Single((await secondService.GetMenuAsync(default)).Items);
        Assert.Equal("Phở đặc biệt", current.Name);
        Assert.Equal("BEP", current.Station);
        Assert.Single(current.Variants);
    }

    [SqlServerFact]
    public async Task Active_dish_requires_a_valid_initial_variant_and_keeps_one_active_variant()
    {
        var seed = await SeedAsync();
        await using var db = NewContext();
        var service = new CatalogMenu(db);
        Assert.Equal(CatalogErrors.InputInvalid, (await service.CreateItemAsync(
            new(seed.Group.Id, "BAD", "Món lỗi", "BEP", new("STD", "Chuẩn", -1)), default)).Error);
        Assert.Single((await service.GetMenuAsync(default)).Items);
        var disable = new UpdateVariantRequest("LON", "Tô lớn", 65000, false, seed.Variant.Version);
        Assert.Equal(CatalogErrors.VariantRequired, (await service.UpdateVariantAsync(seed.Item.Id, seed.Variant.Id, disable, default)).Error);
        Ok(await service.CreateVariantAsync(seed.Item.Id, new("NHO", "Tô nhỏ", 45000), default));
        Assert.False(Ok(await service.UpdateVariantAsync(seed.Item.Id, seed.Variant.Id, disable, default)).Active);
    }

    [SqlServerFact]
    public async Task Required_option_cannot_be_deactivated_until_assignment_is_removed()
    {
        var seeded = await SeedAsync();
        await using var db = NewContext();
        var service = new CatalogMenu(db);
        Assert.Equal(CatalogErrors.OptionRequired, (await service.UpdateOptionAsync(seeded.Options.Id, seeded.Option.Id,
            new("Thêm bò", 15000, false, seeded.Option.Version), default)).Error);
        Ok(await service.RemoveOptionGroupAsync(seeded.Item.Id, seeded.Options.Id, default));
        var inactive = Ok(await service.UpdateOptionAsync(seeded.Options.Id, seeded.Option.Id,
            new("Thêm bò", 15000, false, seeded.Option.Version), default));
        Assert.False(inactive.Active);
        Assert.Equal(CatalogErrors.SelectionRangeInvalid,
            (await service.AssignOptionGroupAsync(seeded.Item.Id, seeded.Options.Id, new(0, 1), default)).Error);
        var menu = await service.GetMenuAsync(default);
        Assert.Empty(Assert.Single(menu.Items).OptionGroups);
        Assert.False(Assert.Single(Assert.Single(menu.OptionGroups).Options).Active);
    }

    [SqlServerFact]
    public async Task Concurrent_same_code_creation_returns_one_success_and_a_conflict()
    {
        Guid groupId;
        await using (var seed = NewContext())
            groupId = Ok(await new CatalogMenu(seed).CreateGroupAsync(new("Nhóm", 0), default)).Id;
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = NewContext();
            return await new CatalogMenu(db).CreateItemAsync(new(groupId, "SAME", "Món", "BEP", new("STD", "Chuẩn", 1000)), default);
        }));
        Assert.Single(results, x => x.Error is null);
        Assert.All(results.Where(x => x.Error is not null), x =>
            Assert.Contains(x.Error, new[] { CatalogErrors.ItemCodeConflict, CatalogErrors.ConcurrencyConflict }));
        await using var verify = NewContext();
        Assert.Single(await verify.Set<Mon>().ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Wrapped_deadlock_returns_conflict_and_does_not_replay_failed_changes()
    {
        Guid groupId;
        await using (var seed = NewContext())
        {
            groupId = Ok(await new CatalogMenu(seed).CreateGroupAsync(new("Nhóm", 0), default)).Id;
            await seed.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.CatalogDeadlockProbe (Id int PRIMARY KEY, Value int NOT NULL);
                INSERT dbo.CatalogDeadlockProbe VALUES (1, 0), (2, 0);
                """);
        }

        var fault = new DeadlockOnFirstItemInsert();
        await using var blocker = new SqlConnection(connectionString);
        await blocker.OpenAsync();
        await using var blockerTransaction = (SqlTransaction)await blocker.BeginTransactionAsync();
        await using var holdFirst = new SqlCommand("""
            SET DEADLOCK_PRIORITY HIGH;
            UPDATE dbo.CatalogDeadlockProbe WITH (ROWLOCK) SET Value = 1 WHERE Id = 1;
            """, blocker, blockerTransaction);
        await holdFirst.ExecuteNonQueryAsync();
        var completeBlocker = Task.Run(async () =>
        {
            await fault.SecondRowLocked.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var waitSecond = new SqlCommand(
                "UPDATE dbo.CatalogDeadlockProbe WITH (ROWLOCK) SET Value = 1 WHERE Id = 2;", blocker, blockerTransaction);
            await waitSecond.ExecuteNonQueryAsync();
            await blockerTransaction.CommitAsync();
        });
        await using var db = NewContext(fault);
        var service = new CatalogMenu(db);
        var failed = await service.CreateItemAsync(new(groupId, "FAILED", "Món lỗi", "BEP", new("STD", "Chuẩn", 1000)), default);
        await completeBlocker.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(fault.Injected);
        Assert.Equal(CatalogErrors.ConcurrencyConflict, failed.Error);
        Assert.Empty(db.ChangeTracker.Entries());

        await db.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY NORMAL;");
        var created = Ok(await service.CreateItemAsync(new(groupId, "AFTER", "Món sau", "BEP", new("STD", "Chuẩn", 1000)), default));
        await using var verify = NewContext();
        var saved = Assert.Single(await verify.Set<Mon>().ToArrayAsync());
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal("AFTER", saved.Ma);
    }

    private sealed class DeadlockOnFirstItemInsert : DbCommandInterceptor
    {
        public bool Injected { get; private set; }
        public TaskCompletionSource SecondRowLocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Injected && command.CommandText.Contains("INSERT INTO [catalog].[Mon]", StringComparison.Ordinal))
            {
                Injected = true;
                // The blocker holds row 1; this transaction holds row 2. Both then request the other's row.
                // A lower priority makes this EF transaction the real SQL Server deadlock victim.
                await using var holdSecond = command.Connection!.CreateCommand();
                holdSecond.Transaction = command.Transaction;
                holdSecond.CommandText = """
                    SET DEADLOCK_PRIORITY LOW;
                    UPDATE dbo.CatalogDeadlockProbe WITH (ROWLOCK) SET Value = 2 WHERE Id = 2;
                    """;
                await holdSecond.ExecuteNonQueryAsync(cancellationToken);
                SecondRowLocked.TrySetResult();
                command.CommandText = "UPDATE dbo.CatalogDeadlockProbe WITH (ROWLOCK) SET Value = 2 WHERE Id = 1;";
            }
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [SqlServerFact]
    public async Task Sales_pricing_rejects_a_foreign_variant_or_unassigned_option_on_real_SQL()
    {
        var seeded = await SeedAsync();
        await using var db = NewContext();
        var menu = new CatalogMenu(db);
        var other = Ok(await menu.CreateItemAsync(new(seeded.Group.Id, "OTHER", "Other", "BAR", new("STD", "Standard", 1000)), default));
        var pricing = new MenuPricing(db);
        var day = Guid.NewGuid();
        var variant = Assert.Single(other.Variants);
        var wrongVariant = await pricing.PriceAsync(new(day, [new(1, seeded.Item.Id, variant.Id, [])]), default);
        Assert.Equal(CatalogErrorCodes.VariantNotOwned, Assert.Single(wrongVariant.Errors).Code);
        Assert.Empty(wrongVariant.Lines);
        var wrongOption = await pricing.PriceAsync(new(day, [new(1, other.Id, variant.Id, [seeded.Option.Id])]), default);
        Assert.Equal(CatalogErrorCodes.OptionNotAllowed, Assert.Single(wrongOption.Errors).Code);
        Assert.Empty(wrongOption.Lines);
        var valid = await pricing.PriceAsync(new(day, [new(1, seeded.Item.Id, seeded.Variant.Id, [seeded.Option.Id])]), default);
        Assert.Empty(valid.Errors);
        Assert.Equal(80000, Assert.Single(valid.Lines).UnitPrice);
    }

    private static T Ok<T>(CatalogResult<T> result)
    {
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        return result.Value!;
    }
}
