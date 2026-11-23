using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Billing.Application;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class WeekFourContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/inventory/catalog")]
    [InlineData("POST", "/api/inventory/purchases")]
    [InlineData("GET", "/api/salesday/a171ad08-a336-4415-bb2a-b97f288df2f3/close-checklist")]
    [InlineData("POST", "/api/salesday/a171ad08-a336-4415-bb2a-b97f288df2f3/begin-close")]
    [InlineData("POST", "/api/salesday/a171ad08-a336-4415-bb2a-b97f288df2f3/cancel-close")]
    [InlineData("POST", "/api/billing/orders/42/close")]
    public async Task Anonymous_access_is_rejected(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Migrations_cover_inventory_snapshots_and_atomic_close_history()
    {
        using var scope = factory.Services.CreateScope();
        var stock = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        Assert.False(stock.Database.HasPendingModelChanges());
        Assert.False(days.Database.HasPendingModelChanges());
        var inventorySql = stock.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("[inventory].[ChiTietPhieuNhap]", inventorySql);
        Assert.Contains("[HeSoChot]", inventorySql);
        Assert.Contains("[inventory].[KiemKeTon]", inventorySql);
        Assert.Contains("[salesday].[ClosingHistory]", days.GetService<IMigrator>().GenerateScript());
    }

    [Theory]
    [InlineData(100, 0, "DA_THU_DU")]
    [InlineData(100, 40, "THU_MOT_PHAN")]
    [InlineData(0, 100, "CHUA_THU")]
    public void Payment_status_tracks_the_current_balance(int collected, int due, string expected)
    {
        var bill = new InvoiceView(1, 2, "HD-2", Guid.NewGuid(), collected + due, collected, due, 0, due, [], [], [], []);
        Assert.Equal(expected, bill.PaymentStatus);
    }
}
