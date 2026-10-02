using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.Services;

/// <summary>
/// Allocates {prefix}{sequence} codes such as TD-2026-0007. The next sequence
/// is the highest one already used plus one. Counting rows instead breaks as
/// soon as one record is deleted: the count lands on a code that still exists
/// and every later insert collides with the unique index.
/// </summary>
internal static class SequentialCodes
{
    public static async Task<string> NextAsync(
        IQueryable<string> codes,
        string prefix,
        int width,
        CancellationToken ct)
    {
        var used = await codes
            .Where(code => code.StartsWith(prefix))
            .ToListAsync(ct);
        var highest = used
            .Select(code => code.Length > prefix.Length &&
                int.TryParse(code[prefix.Length..], out var sequence)
                    ? sequence
                    : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{(highest + 1).ToString().PadLeft(width, '0')}";
    }

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
