using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Modules;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Api.Configuration;

public static class DemoSeedCommand
{
    public static void ValidateTarget(string connectionString, string environment)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        const string prefix = "RestaurantDemo_";
        if (environment is not ("Development" or "Testing" or "Staging") ||
            !database.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(database[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Only an isolated RestaurantDemo_<32-hex-id> database in a non-production environment is allowed.");
    }

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1) return 2;
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            await ExecuteAsync(args[0], configuration, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
            Console.WriteLine(args[0] == "--reset-demo" ? "Isolated demo database reset." : "Demo ready: five role accounts, two tables, two menu items, topping and two ingredients. Credentials are not printed.");
            return 0;
        }
        catch
        {
            Console.Error.WriteLine("Demo command failed. Check the isolated database name, ownership marker, environment and DemoSeed__Password. No secrets are printed.");
            return 1;
        }
    }

    public static async Task ExecuteAsync(string mode, IConfiguration configuration, string environment)
    {
        if (mode is not ("--seed-demo" or "--reset-demo")) throw new ArgumentException("Unknown demo command.");
        var connection = configuration.GetConnectionString("Restaurant") ?? "";
        ValidateTarget(connection, environment);
        var password = configuration["DemoSeed:Password"];
        if (mode == "--seed-demo" && string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Demo password required through configuration.");
        await using var services = new ServiceCollection().AddSingleton(configuration).AddLogging().AddRestaurantModules().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var identity = p.GetRequiredService<IdentityDbContext>();
        var exists = await identity.Database.CanConnectAsync();
        if (exists)
        {
            var marker = await identity.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.HaiDemoSeed') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync();
            if (marker != 1 || await identity.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.HaiDemoSeed WHERE Owner=N'Hai249' AND Version=1").SingleAsync() != 1)
                throw new InvalidOperationException("Refusing an unmarked existing database.");
        }
        if (mode == "--reset-demo")
        {
            if (exists) await identity.Database.EnsureDeletedAsync();
            return;
        }
        await identity.Database.MigrateAsync();
        if (!exists)
            await identity.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.HaiDemoSeed (Id int PRIMARY KEY CHECK(Id=1), Owner nvarchar(32) NOT NULL, Version int NOT NULL); INSERT dbo.HaiDemoSeed VALUES(1,N'Hai249',1);");
        DbContext[] contexts = [p.GetRequiredService<SalesDayDbContext>(), p.GetRequiredService<CatalogDbContext>(),
            p.GetRequiredService<SeatingDbContext>(), p.GetRequiredService<SalesDbContext>(), p.GetRequiredService<BillingDbContext>(),
            p.GetRequiredService<InventoryDbContext>(), p.GetRequiredService<FulfillmentDbContext>(), p.GetRequiredService<AuditDbContext>()];
        foreach (var db in contexts) await db.Database.MigrateAsync();
        var users = p.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Guid manager = default;
        foreach (var role in new[] { "Admin", "Manager", "Staff", "Kitchen", "Bar" })
        {
            var name = "demo." + role.ToLowerInvariant();
            var user = await users.FindByNameAsync(name);
            if (user is null)
            {
                user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = name, Email = name + "@example.invalid" };
                if (!(await users.CreateAsync(user, password!)).Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded)
                    throw new InvalidOperationException("Demo account setup failed.");
            }
            if (!(await users.IsInRoleAsync(user, role))) throw new InvalidOperationException("Unexpected existing demo account role.");
            if (role == "Manager") manager = user.Id;
        }
        var days = p.GetRequiredService<IBusinessDays>();
        var day = await days.CurrentAsync(default);
        if (day is null)
        {
            var opened = await days.OpenAsync(new(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), 500000), manager,
                Guid.Parse("9e1d8e48-7f9e-4db0-a035-89b3b8fa7771"), default);
            day = opened.Day ?? throw new InvalidOperationException("Demo business day could not be opened.");
        }
        var seating = p.GetRequiredService<ISeatingCatalog>();
        var area = (await seating.ListAreasAsync(default)).SingleOrDefault(a => a.Code == "HAI_DEMO");
        area ??= (await seating.CreateAreaAsync(new("HAI_DEMO", "Hai demo area"), default)).Value
            ?? throw new InvalidOperationException("Demo area setup failed.");
        foreach (var code in new[] { "HAI_DEMO_01", "HAI_DEMO_02" })
            if (!(await seating.ListTablesAsync(default)).Any(t => t.Code == code) &&
                (await seating.CreateTableAsync(new(area.Id, code, code), default)).Error is not null)
                throw new InvalidOperationException("Demo table setup failed.");
        var catalog = p.GetRequiredService<ICatalogMenu>();
        var menu = await catalog.GetMenuAsync(default);
        var group = menu.Groups.SingleOrDefault(g => g.Name == "Hai demo menu");
        group ??= (await catalog.CreateGroupAsync(new("Hai demo menu", 100), default)).Value
            ?? throw new InvalidOperationException("Demo menu group setup failed.");
        foreach (var item in new[] { (Code: "HAI_DEMO_FOOD", Name: "Demo food", Station: "BEP"), (Code: "HAI_DEMO_TEA", Name: "Demo tea", Station: "BAR") })
            if (!(await catalog.GetMenuAsync(default)).Items.Any(i => i.Code == item.Code) &&
                (await catalog.CreateItemAsync(new(group.Id, item.Code, item.Name, item.Station, new("STD", "Standard", 20000)), default)).Error is not null)
                throw new InvalidOperationException("Demo item setup failed.");
        menu = await catalog.GetMenuAsync(default);
        var options = menu.OptionGroups.SingleOrDefault(g => g.Name == "Hai demo topping");
        options ??= (await catalog.CreateOptionGroupAsync(new("Hai demo topping", "TOPPING"), default)).Value
            ?? throw new InvalidOperationException("Demo topping group setup failed.");
        if (!options.Options.Any() && (await catalog.CreateOptionAsync(options.Id, new("Demo topping", 5000), default)).Error is not null)
            throw new InvalidOperationException("Demo topping setup failed.");
        var tea = menu.Items.Single(i => i.Code == "HAI_DEMO_TEA");
        if (!tea.OptionGroups.Any(g => g.OptionGroupId == options.Id) &&
            (await catalog.AssignOptionGroupAsync(tea.Id, options.Id, new(0, 1), default)).Error is not null)
            throw new InvalidOperationException("Demo topping assignment failed.");
        var inventory = p.GetRequiredService<IInventory>();
        foreach (var code in new[] { "HAI_DEMO_KG", "HAI_DEMO_L" })
        {
            var stock = await inventory.CatalogAsync(default);
            var unit = stock.Units.SingleOrDefault(u => u.Ma == code)?.Id;
            if (unit is null) unit = (await inventory.UnitAsync(new(code, code, code.EndsWith("KG", StringComparison.Ordinal) ? "KHOI_LUONG" : "THE_TICH"), default)).Id;
            if (unit is null) throw new InvalidOperationException("Demo unit setup failed.");
            if (!stock.Ingredients.Any(i => i.Ma == code) &&
                (await inventory.IngredientAsync(null, new(day.Id, code, code, unit.Value, "THEO_DOI_TON", false), default)).Error is not null)
                throw new InvalidOperationException("Demo ingredient setup failed.");
        }
    }
}
