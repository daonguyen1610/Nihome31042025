using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record ProjectDocumentMoveDescriptor(
    ProjectDocumentCategory Category,
    ProjectDocumentSourceModule SourceModule,
    string SourceEntityType,
    string SourceSlot,
    long SourceRecordId,
    string LocalPath,
    string OriginalFileName,
    int? CustomerId,
    int? ContractId);

public interface IProjectDocumentStagingService
{
    Task<ProjectDocument> StageExistingManagedFileAsync(
        int projectId, ProjectDocumentCategory category, ProjectDocumentSourceModule sourceModule,
        string sourceEntityType, string sourceSlot, long sourceRecordId, string localPath,
        string originalFileName, int? customerId, int? contractId, int? userId,
        CancellationToken ct = default);

    Task<bool> StageExistingManagedFileDeleteAsync(
        int projectId, ProjectDocumentSourceModule sourceModule, string sourceEntityType,
        string sourceSlot, long sourceRecordId, string localPath, int? userId,
        CancellationToken ct = default);

    Task<bool> RetryExistingManagedFileAsync(
        int projectId, ProjectDocumentSourceModule sourceModule, string sourceEntityType,
        string sourceSlot, long sourceRecordId, string localPath, int? userId,
        CancellationToken ct = default);

    Task StageExistingManagedFilesMoveAsync(
        int? oldProjectId, int? newProjectId,
        IReadOnlyCollection<ProjectDocumentMoveDescriptor> files,
        int? userId, CancellationToken ct = default);
}