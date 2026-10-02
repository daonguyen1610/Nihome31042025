using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// Allocates PJ-{year}-{seq} codes. Shared by the project screen and by the
/// lead conversion that opens a project together with its opportunity, so both
/// paths serialize on the same SQL Server application lock.
/// </summary>
internal static class OperationalProjectCodeAllocator
{
    public static bool IsSqlServer(AppDbContext db) => string.Equals(
        db.Database.ProviderName,
        "Microsoft.EntityFrameworkCore.SqlServer",
        StringComparison.Ordinal);

    /// <summary>
    /// Takes the per-year allocation lock inside the caller's open transaction.
    /// The lock is owned by that transaction and released when it ends.
    /// </summary>
    public static async Task AcquireLockAsync(AppDbContext db, int year, CancellationToken ct)
    {
        if (!IsSqlServer(db) || db.Database.CurrentTransaction is null) return;

        var resource = $"operational-project-code-{year}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @result < 0
                THROW 51000, 'Unable to acquire the project code allocation lock.', 1;
            """, ct);
    }

    public static Task<string> NextCodeAsync(AppDbContext db, int year, CancellationToken ct) =>
        SequentialCodes.NextAsync(db.OperationalProjects.Select(project => project.Code), $"PJ-{year}-", 4, ct);
}
