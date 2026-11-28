using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Configuration;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class GuestOrdersSqlServerTests : IAsyncLifetime
{
    private const string GuestApi = "/api/public/guest-access";
    private IConfiguration? configuration;
    private WebApplicationFactory<Program>? factory;
    private Guid manager;
    private BatchLine line = null!;
    private IServiceProvider Services => factory!.Services;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied)
        {
            InitialCatalog = "RestaurantDemo_" + Guid.NewGuid().ToString("N")
        }.ConnectionString;
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Restaurant"] = connection,
            ["DemoSeed:Password"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!"
        };
        configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        await DemoSeedCommand.ExecuteAsync("--seed-demo", configuration, "Testing");
        factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values)));
        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        manager = await provider.GetRequiredService<IdentityDbContext>().Users.Where(u => u.UserName == "demo.manager")
            .Select(u => u.Id).SingleAsync();
        var item = (await provider.GetRequiredService<ICatalogMenu>().GetMenuAsync(default)).Items.Single(i => i.Code == "HAI_DEMO_TEA");
        line = new(item.Id, item.Variants.Single().Id, [], null);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        if (configuration is not null) await DemoSeedCommand.ExecuteAsync("--reset-demo", configuration, "Testing");
    }

    [SqlServerFact]
    public async Task Guest_can_submit_and_track_only_its_own_batch_and_replay_once()
    {
        var first = await OpenAsync("HAI_DEMO_01");
        var second = await OpenAsync("HAI_DEMO_02");
        var original = await GuestAsync(first);
        var sameSession = await GuestAsync(first);
        var otherSession = await GuestAsync(second);
        using var client = factory!.CreateClient();
        var key = Guid.NewGuid();
        using var anonymous = await SubmitAsync(client, null, key);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var locator = await SubmitAsync(client, original.TableCode, key);
        Assert.Equal(HttpStatusCode.Unauthorized, locator.StatusCode);
        using var created = await SubmitAsync(client, original.Token, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = (await created.Content.ReadFromJsonAsync<GuestBatchReceipt>())!;
        Assert.Equal("CHO_XAC_NHAN", receipt.Status);
        using var replay = await SubmitAsync(client, original.Token, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<GuestBatchReceipt>());
        using var own = await TrackAsync(client, original.Token, receipt.Id);
        Assert.Equal(receipt, await own.Content.ReadFromJsonAsync<GuestBatchReceipt>());
        using var other = await TrackAsync(client, otherSession.Token, receipt.Id);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var sameTableOtherGuest = await TrackAsync(client, sameSession.Token, receipt.Id);
        Assert.Equal(HttpStatusCode.NotFound, sameTableOtherGuest.StatusCode);

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var sales = provider.GetRequiredService<SalesDbContext>();
        var order = await sales.Orders.AsNoTracking().SingleAsync();
        var batch = await sales.Batches.AsNoTracking().SingleAsync();
        Assert.Equal(first, order.PhienSuDungId);
        Assert.NotNull(batch.QuyenTruyCapKhachId);
        var result = await provider.GetRequiredService<IOrderCommands>().ConfirmBatchAsync(new(order.Id, batch.SoDot, manager), default);
        Assert.Null(result.Error);
        using var confirmed = await TrackAsync(client, original.Token, receipt.Id);
        Assert.Equal("DA_XAC_NHAN", (await confirmed.Content.ReadFromJsonAsync<GuestBatchReceipt>())!.Status);
    }

    [SqlServerFact]
    public async Task Guest_cancels_only_its_own_pending_batch_and_never_one_staff_already_decided()
    {
        var session = await OpenAsync("HAI_DEMO_01");
        var otherSession = await OpenAsync("HAI_DEMO_02");
        var owner = await GuestAsync(session);
        var sameTable = await GuestAsync(session);
        var elsewhere = await GuestAsync(otherSession);
        using var client = factory!.CreateClient();
        var pending = await SubmitReceiptAsync(client, owner.Token);

        using (var anonymous = await CancelAsync(client, null, pending.Id))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using (var locator = await CancelAsync(client, owner.TableCode, pending.Id))
            Assert.Equal(HttpStatusCode.Unauthorized, locator.StatusCode);
        // A batch of another guest or another session is indistinguishable from a missing one.
        foreach (var stranger in new[] { sameTable.Token, elsewhere.Token })
        {
            using var denied = await CancelAsync(client, stranger, pending.Id);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        Assert.Equal("CHO_XAC_NHAN", (await TrackReceiptAsync(client, owner.Token, pending.Id)).Status);

        using var cancelled = await CancelAsync(client, owner.Token, pending.Id);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(new GuestBatchReceipt(pending.Id, "DA_HUY"), await cancelled.Content.ReadFromJsonAsync<GuestBatchReceipt>());
        using var again = await CancelAsync(client, owner.Token, pending.Id);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("DA_HUY", (await TrackReceiptAsync(client, owner.Token, pending.Id)).Status);

        var confirmed = await SubmitReceiptAsync(client, owner.Token);
        var rejected = await SubmitReceiptAsync(client, owner.Token);
        await using (var scope = Services.CreateAsyncScope())
        {
            var commands = scope.ServiceProvider.GetRequiredService<IOrderCommands>();
            var sales = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            var batches = await sales.Batches.AsNoTracking().ToListAsync();
            var orderId = batches.Select(x => x.DonHangId).Distinct().Single();
            Assert.Null((await commands.ConfirmBatchAsync(new(orderId, BatchNumber(batches, confirmed.Id), manager), default)).Error);
            Assert.Null((await commands.RejectBatchAsync(new(orderId, BatchNumber(batches, rejected.Id), manager, "Het mon"), default)).Error);
            var withdrawnPortions = await sales.Portions.AsNoTracking()
                .Where(x => x.DotGoiMonId == long.Parse(pending.Id)).ToListAsync();
            Assert.NotEmpty(withdrawnPortions);
            Assert.All(withdrawnPortions, portion => Assert.Equal("DA_HUY", portion.TrangThai));
        }
        foreach (var (receipt, expected) in new[] { (confirmed, "DA_XAC_NHAN"), (rejected, "DA_TU_CHOI") })
        {
            using var late = await CancelAsync(client, owner.Token, receipt.Id);
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
            using var problem = JsonDocument.Parse(await late.Content.ReadAsStringAsync());
            Assert.Equal("sales.batch.not_pending", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(expected, (await TrackReceiptAsync(client, owner.Token, receipt.Id)).Status);
        }
    }

    [SqlServerFact]
    public async Task Expired_and_revoked_grants_cannot_submit_and_close_cannot_race_past_submission()
    {
        var unused = await OpenAsync("HAI_DEMO_01");
        var expired = await GuestAsync(unused);
        await using (var scope = Services.CreateAsyncScope())
        {
            var seating = scope.ServiceProvider.GetRequiredService<SeatingDbContext>();
            var now = DateTime.UtcNow;
            await seating.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE seating.QuyenTruyCapKhach SET TaoLuc={now.AddHours(-6)}, HetHanLuc={now.AddHours(-2)}
                WHERE PhienSuDungId={unused}
                """);
        }
        using var client = factory!.CreateClient();
        using var expiredResult = await SubmitAsync(client, expired.Token, Guid.NewGuid());
        await AssertUnauthorizedAsync(expiredResult, "qr.token.expired");
        using var expiredCancel = await CancelAsync(client, expired.Token, "1");
        await AssertUnauthorizedAsync(expiredCancel, "qr.token.expired");
        var revoked = await GuestAsync(unused);
        Assert.Null((await CloseAsync(unused)).Error);
        using var revokedResult = await SubmitAsync(client, revoked.Token, Guid.NewGuid());
        await AssertUnauthorizedAsync(revokedResult, "qr.token.revoked");
        using var revokedCancel = await CancelAsync(client, revoked.Token, "1");
        await AssertUnauthorizedAsync(revokedCancel, "qr.token.revoked");

        var active = await OpenAsync("HAI_DEMO_02");
        var guest = await GuestAsync(active);
        var submitting = SubmitAsync(client, guest.Token, Guid.NewGuid());
        var closing = CloseAsync(active);
        await Task.WhenAll(submitting, closing);
        using var sent = await submitting;
        var closed = await closing;
        await using var read = Services.CreateAsyncScope();
        var sales = read.ServiceProvider.GetRequiredService<SalesDbContext>();
        var count = await sales.Orders.CountAsync(x => x.PhienSuDungId == active);
        if (closed.Error is null)
        {
            await AssertUnauthorizedAsync(sent, "qr.token.revoked");
            Assert.Equal(0, count);
        }
        else
        {
            Assert.Equal(SeatingErrors.SessionHasOpenOrders, closed.Error);
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
            Assert.Equal(1, count);
        }
    }

    private async Task<long> OpenAsync(string code)
    {
        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var table = (await provider.GetRequiredService<ISeatingCatalog>().ListTablesAsync(default)).Single(t => t.Code == code);
        var result = await provider.GetRequiredService<ISeatingSessions>().OpenAsync(table.Id, manager, Guid.NewGuid(), default);
        Assert.Null(result.Error);
        return result.Value!.Id;
    }

    private async Task<(string TableCode, string Token)> GuestAsync(long session)
    {
        await using var scope = Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<IGuestAccess>();
        var invite = await access.InviteAsync(session, manager, default);
        Assert.Null(invite.Error);
        var exchanged = await access.ExchangeAsync(new(invite.Value!.TableCode, invite.Value.JoinCode), default);
        Assert.Null(exchanged.Error);
        return (invite.Value.TableCode, exchanged.Value!.Token);
    }

    private async Task<SeatingResult<SessionDto>> CloseAsync(long session)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISeatingSessions>().CloseAsync(session, manager, Guid.NewGuid(), default);
    }

    private async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string? token, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GuestApi + "/batches")
        {
            Content = JsonContent.Create(new { lines = new[] { line } })
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        if (token is not null) request.Headers.Authorization = new("Guest", token);
        return await client.SendAsync(request);
    }

    private async Task<GuestBatchReceipt> SubmitReceiptAsync(HttpClient client, string token)
    {
        using var response = await SubmitAsync(client, token, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuestBatchReceipt>())!;
    }

    private static async Task<GuestBatchReceipt> TrackReceiptAsync(HttpClient client, string token, string id)
    {
        using var response = await TrackAsync(client, token, id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuestBatchReceipt>())!;
    }

    private static int BatchNumber(IEnumerable<DotGoiMon> batches, string id) =>
        batches.Single(x => x.Id == long.Parse(id)).SoDot;

    private static async Task<HttpResponseMessage> CancelAsync(HttpClient client, string? token, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GuestApi + "/batches/" + id + "/cancel");
        if (token is not null) request.Headers.Authorization = new("Guest", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> TrackAsync(HttpClient client, string token, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GuestApi + "/batches/" + id);
        request.Headers.Authorization = new("Guest", token);
        return await client.SendAsync(request);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response, string expectedCode)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
    }
}
