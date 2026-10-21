using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// Serialize dossier edits and submission before reading their guards. The
/// transaction also makes the checked file set and the frozen revision atomic.
/// </summary>
internal static class ConceptDossierTransaction
{
    public static async Task<IDbContextTransaction?> BeginAsync(AppDbContext db, int optionId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (db.Database.IsSqlServer())
            {
                var resource = $"concept-dossier-{optionId}";
                var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC {result} = sys.sp_getapplock
                        @Resource = {resource}, @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction', @LockTimeout = 15000;
                    """, ct);
                if ((int)result.Value < 0)
                    throw new CrmConcurrencyException("Hồ sơ Concept đang được cập nhật. Vui lòng tải lại và thử lại.");
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public static async Task<IDbContextTransaction?> BeginForReviewAsync(
        AppDbContext db, int projectId, int reviewId, CancellationToken ct)
    {
        var optionId = await db.DesignReviews.AsNoTracking()
            .Where(item => item.Id == reviewId && item.DesignProjectId == projectId && item.ConceptRevisionId != null)
            .Select(item => (int?)item.ConceptRevision!.ConceptOptionId).SingleOrDefaultAsync(ct);
        return optionId.HasValue ? await BeginAsync(db, optionId.Value, ct) : null;
    }
}
