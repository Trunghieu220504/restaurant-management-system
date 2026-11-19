using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace Restaurant.Shared.SqlServer;

/// <summary>
/// Shared plumbing for the technical Outbox and Inbox tables. Each module owns its tables
/// (<c>[schema].[Outbox]</c>, <c>[schema].[Inbox]</c>) through its own migrations; this code only reads and writes them
/// over a connection and transaction owned by the caller, like <see cref="BusinessDayLock"/>.
/// </summary>
internal static class MessagingSql
{
    private static readonly Regex SchemaPattern = new("^[A-Za-z][A-Za-z0-9_]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Schema names are developer constants, but they are spliced into SQL, so they are checked strictly.</summary>
    public static string Table(string schema, string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (!SchemaPattern.IsMatch(schema)) throw new ArgumentException("Use a plain schema name.", nameof(schema));
        return "[" + schema + "].[" + table + "]";
    }

    public static void RequireOpen(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must open the connection.");
    }

    public static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public static DateTime Utc(DateTimeOffset value) => value.UtcDateTime;

    public static DateTimeOffset FromUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

/// <summary>
/// One Outbox row as read by the dispatcher. It mirrors the shared contract type of the same shape so this
/// helper stays free of project references; the host maps it when it hands the message to handlers.
/// </summary>
public sealed record OutboxMessage(
    Guid EventId,
    string AggregateId,
    int AggregateVersion,
    string Type,
    int SchemaVersion,
    string Payload,
    DateTimeOffset OccurredAt,
    int Attempts);
