using Microsoft.Data.SqlClient;
using Restaurant.Shared.SqlServer;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class BusinessDayLockSqlServerTests
{
    [SqlServerFact]
    public async Task Locks_are_transaction_owned_day_scoped_and_exclusive_against_writers()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")!;
        await using var a = new SqlConnection(connectionString);
        await using var b = new SqlConnection(connectionString);
        await a.OpenAsync();
        await b.OpenAsync();
        var day = Guid.NewGuid();
        await using var ta = await a.BeginTransactionAsync();
        await using var tb = await b.BeginTransactionAsync();
        await BusinessDayLock.AcquireAsync(a, ta, day, BusinessDayLockMode.Shared, 0);
        await BusinessDayLock.AcquireAsync(b, tb, day, BusinessDayLockMode.Shared, 0);
        await Assert.ThrowsAsync<TimeoutException>(() => BusinessDayLock.AcquireAsync(b, tb, day, BusinessDayLockMode.Exclusive, 0));
        await BusinessDayLock.AcquireAsync(b, tb, Guid.NewGuid(), BusinessDayLockMode.Exclusive, 0);
        await ta.RollbackAsync();
        await BusinessDayLock.AcquireAsync(b, tb, day, BusinessDayLockMode.Exclusive, 0);
        await using var tc = await a.BeginTransactionAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => BusinessDayLock.AcquireAsync(a, tc, day, BusinessDayLockMode.Shared, 0));
        await tb.CommitAsync();
        await BusinessDayLock.AcquireAsync(a, tc, day, BusinessDayLockMode.Exclusive, 0);
        await tc.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Rollback_and_cancellation_preserve_caller_ownership()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")!;
        await using var a = new SqlConnection(connectionString);
        await using var b = new SqlConnection(connectionString);
        await a.OpenAsync();
        await b.OpenAsync();
        var day = Guid.NewGuid();
        await using var owner = await a.BeginTransactionAsync();
        await using var contender = await b.BeginTransactionAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BusinessDayLock.AcquireAsync(a, owner, day, BusinessDayLockMode.Exclusive, 0, cancellation.Token));
        await BusinessDayLock.AcquireAsync(b, contender, day, BusinessDayLockMode.Exclusive, 0);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            BusinessDayLock.AcquireAsync(a, owner, day, BusinessDayLockMode.Shared, 0));
        await contender.RollbackAsync();
        await BusinessDayLock.AcquireAsync(a, owner, day, BusinessDayLockMode.Exclusive, 0);
        await owner.RollbackAsync();
        Assert.Equal(System.Data.ConnectionState.Open, a.State);
        Assert.Equal(System.Data.ConnectionState.Open, b.State);
    }

    [SqlServerFact]
    public async Task Helper_rejects_invalid_arguments_without_taking_ownership()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")!;
        await using var a = new SqlConnection(connectionString);
        await using var b = new SqlConnection(connectionString);
        await a.OpenAsync();
        await b.OpenAsync();
        await using var transaction = await a.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => BusinessDayLock.AcquireAsync(b, transaction, Guid.NewGuid(), BusinessDayLockMode.Shared, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => BusinessDayLock.AcquireAsync(a, transaction, Guid.Empty, BusinessDayLockMode.Shared, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BusinessDayLock.AcquireAsync(a, transaction, Guid.NewGuid(), (BusinessDayLockMode)99, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BusinessDayLock.AcquireAsync(a, transaction, Guid.NewGuid(), BusinessDayLockMode.Shared, -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BusinessDayLock.AcquireAsync(a, transaction, Guid.NewGuid(), BusinessDayLockMode.Shared, 60001));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BusinessDayLock.AcquireAsync(null!, transaction, Guid.NewGuid(), BusinessDayLockMode.Shared));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BusinessDayLock.AcquireAsync(a, null!, Guid.NewGuid(), BusinessDayLockMode.Shared));
        await transaction.RollbackAsync();
        Assert.Equal(System.Data.ConnectionState.Open, a.State);
    }
}
