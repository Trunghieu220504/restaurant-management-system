using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Domain;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

internal static class SeatingRules
{
    private static readonly Regex CodePattern = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool TryCode(string? value, out string code)
    {
        code = value?.Trim() ?? "";
        return CodePattern.IsMatch(code);
    }

    public static bool TryName(string? value, out string name)
    {
        name = value?.Trim() ?? "";
        return name.Length is >= 1 and <= 150;
    }

    public static bool TryReason(string? value, out string reason)
    {
        reason = value?.Trim() ?? "";
        return reason.Length is >= 1 and <= 1000;
    }

    public static string EncodeVersion(byte[] version) => Convert.ToBase64String(version);

    /// <summary>Row versions are 8 bytes; anything else cannot match a stored version.</summary>
    public static bool TryDecodeVersion(string? value, out byte[] version)
    {
        version = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            version = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return false;
        }
        return version.Length == 8;
    }

    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>128-bit random, URL-safe locator printed on the table. It is never authorization (ADR-02).</summary>
    public static string NewTableLocator() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal static class SeatingSql
{
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;
    public const int Deadlock = 1205;
    public const int DayLockTimeout = 51000;

    public static bool IsUniqueViolation(SqlException ex) =>
        ex.Number is UniqueIndexViolation or UniqueConstraintViolation;
}

internal static class SeatingLocks
{
    /// <summary>Row lock held until the transaction ends, so archive, open and transfer serialize per table.</summary>
    public static async Task<Ban?> LockTableAsync(this SeatingDbContext db, long id, CancellationToken ct) =>
        (await db.Tables
            .FromSqlInterpolated($"SELECT * FROM [seating].[Ban] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .ToListAsync(ct)).SingleOrDefault();

    public static async Task<KhuVuc?> LockAreaAsync(this SeatingDbContext db, long id, CancellationToken ct) =>
        (await db.Areas
            .FromSqlInterpolated($"SELECT * FROM [seating].[KhuVuc] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .ToListAsync(ct)).SingleOrDefault();

    /// <summary>
    /// Takes the business-day lock in Shared mode for the current transaction (ADR-01: day, then session).
    /// The resource name matches the shared protocol of BusinessDayLock; swap to that helper or ISalesDayGate
    /// once modules may reference the shared projects. A timeout throws SQL error 51000.
    /// </summary>
    public static Task AcquireDaySharedAsync(this SeatingDbContext db, Guid dayId, CancellationToken ct)
    {
        var resource = "restaurant:salesday:" + dayId.ToString("N");
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int;
            EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode=N'Shared', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @r < 0 THROW 51000, N'Business day lock timeout', 1;
            """, ct);
    }
}
