using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.HardDelete;

internal static class DurableHardDeleteStarter
{
    public static async Task<HardDeleteOperationResult?> StartAsync<TPlan>(
        AppDbContext db,
        IHardDeleteOperationService operations,
        Func<Task<TPlan?>> createPlan,
        Func<TPlan, CreateHardDeleteOperationRequest> createRequest,
        CancellationToken ct) where TPlan : class
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        HardDeleteOperationResult operation;
        try
        {
            var plan = await createPlan();
            if (plan is null) return null;
            operation = await operations.CreateAsync(createRequest(plan), ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            DesignProjectHardDeleteHandler.DetachRolledBackDomainEntries(db);
            foreach (var entry in db.ChangeTracker.Entries().Where(entry =>
                entry.Entity is HardDeleteOperation or HardDeleteItem).ToList())
                entry.State = EntityState.Detached;
            throw;
        }
        return await operations.ProcessAsync(operation.OperationId, ct);
    }
}