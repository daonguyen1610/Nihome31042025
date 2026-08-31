using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

public sealed class ProjectDriveClaimLostException() : Exception(
    "Quyền xử lý tệp Google Drive đã hết hạn hoặc được chuyển cho tiến trình khác.");

public interface IProjectDriveClaimRenewer
{
    Task<bool> RenewAsync(long documentId, Guid token, long generation, CancellationToken ct);
}

public sealed class ProjectDriveClaimRenewer(IServiceScopeFactory scopeFactory) : IProjectDriveClaimRenewer
{
    public async Task<bool> RenewAsync(long documentId, Guid token, long generation, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        return await db.ProjectDocuments
            .Where(document => document.Id == documentId && document.ClaimToken == token &&
                document.Generation == generation && document.SyncStatus == ProjectDocumentSyncStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.ClaimExpiresAt, now.Add(ProjectDriveClaimLease.ClaimDuration))
                .SetProperty(document => document.UpdatedAt, now), ct) == 1;
    }
}

public interface IProjectDriveClaimLease
{
    Task<T> RunAsync<T>(long documentId, Guid token, long generation,
        Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}

public sealed class ProjectDriveClaimLease : IProjectDriveClaimLease
{
    internal static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromMinutes(5);
    private readonly IProjectDriveClaimRenewer renewer;
    private readonly TimeSpan heartbeatInterval;

    public ProjectDriveClaimLease(IProjectDriveClaimRenewer renewer)
        : this(renewer, DefaultHeartbeatInterval)
    {
    }

    internal ProjectDriveClaimLease(IProjectDriveClaimRenewer renewer, TimeSpan heartbeatInterval)
    {
        this.renewer = renewer;
        this.heartbeatInterval = heartbeatInterval;
    }

    public async Task<T> RunAsync<T>(long documentId, Guid token, long generation,
        Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var operationTask = operation(operationCancellation.Token);
        try
        {
            while (!operationTask.IsCompleted)
            {
                var delayTask = Task.Delay(heartbeatInterval, operationCancellation.Token);
                if (await Task.WhenAny(operationTask, delayTask) == operationTask)
                    return await operationTask;

                if (await renewer.RenewAsync(documentId, token, generation, ct)) continue;

                if (operationTask.IsCompleted) return await operationTask;

                operationCancellation.Cancel();
                try
                {
                    await operationTask;
                }
                catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
                {
                }
                throw new ProjectDriveClaimLostException();
            }
            return await operationTask;
        }
        finally
        {
            operationCancellation.Cancel();
        }
    }
}