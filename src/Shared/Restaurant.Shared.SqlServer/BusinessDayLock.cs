using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Restaurant.Shared.SqlServer;

public enum BusinessDayLockMode { Shared, Exclusive }

public static class BusinessDayLock
{
    public static async Task AcquireAsync(DbConnection connection, DbTransaction transaction, Guid dayId,
        BusinessDayLockMode mode, int timeoutMilliseconds = 10000, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (dayId == Guid.Empty) throw new ArgumentException("Day ID is required.", nameof(dayId));
        if (mode is not BusinessDayLockMode.Shared and not BusinessDayLockMode.Exclusive)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (timeoutMilliseconds is < 0 or > 60000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Use a bounded timeout between 0 and 60000 ms.");
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("An active transaction on the supplied connection is required.", nameof(transaction));
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must open the connection.");
        cancellationToken.ThrowIfCancellationRequested();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Command execution includes server scheduling/network time, not only the app-lock wait.
        command.CommandTimeout = Math.Max(30, timeoutMilliseconds / 1000 + 5);
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode=@mode,
                @LockOwner=N'Transaction', @LockTimeout=@timeout;
            SELECT @result;
            """;
        AddParameter(command, "@resource", DbType.String, "restaurant:salesday:" + dayId.ToString("N"));
        AddParameter(command, "@mode", DbType.String, mode.ToString());
        AddParameter(command, "@timeout", DbType.Int32, timeoutMilliseconds);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull) throw new InvalidOperationException("SQL Server did not return a lock result.");
        var result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        if (result >= 0) return;
        if (result == -1) throw new TimeoutException("Business day lock timed out; the transaction must not continue.");
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException($"Business day lock failed ({result}); roll back the transaction.");
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
