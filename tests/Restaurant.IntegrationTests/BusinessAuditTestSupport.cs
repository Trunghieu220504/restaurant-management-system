using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Api.Modules;
using Microsoft.AspNetCore.Http;
using Restaurant.Modules.Fulfillment.Application;

namespace Restaurant.IntegrationTests;

/// <summary>Explicit stand-in only for existing isolated module tests. Host integration tests use the real writer.</summary>
internal sealed class TestBusinessAudit : IBusinessAudit
{
    public static TestBusinessAudit Instance { get; } = new();
    public Task AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class BusinessAuditTestSupport
{
    public static IBusinessAudit Writer(DbContext db) => new BusinessAudit(new ContextProvider(db), new HttpContextAccessor(), TimeProvider.System);
    private sealed class ContextProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == db.GetType() ? db : null;
    }
    public static async Task MigrateAsync(string connection)
    {
        await using var db = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")).Options);
        await db.Database.MigrateAsync();
    }
}

internal sealed class TestFulfillmentContext : IFulfillmentIncidentContext
{
    public static TestFulfillmentContext Instance { get; } = new();
    public Task<string?> RequireOpenAsync(System.Data.Common.DbTransaction transaction, long portion, CancellationToken ct) => Task.FromResult<string?>(null);
    public Task<string?> ReduceObligationAsync(System.Data.Common.DbTransaction transaction, long portion, long incident,
        Guid actor, Guid key, string reason, CancellationToken ct) => throw new NotSupportedException();
}
