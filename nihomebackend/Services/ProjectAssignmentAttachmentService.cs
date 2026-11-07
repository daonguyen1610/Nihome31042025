using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProjectAssignmentAttachmentService(
    AppDbContext db,
    IBusinessDocumentStorageService storage,
    ILogger<ProjectAssignmentAttachmentService> logger)
{
    private const BusinessDocumentArea Area = BusinessDocumentArea.ProjectTeam;

    public async Task<IReadOnlyList<OperationalProjectAssignmentAttachmentResponse>?> ListAsync(
        int projectId, int assignmentId, int? designProjectId, CancellationToken ct)
    {
        if (!await AssignmentExistsAsync(projectId, assignmentId, designProjectId, ct)) return null;
        return await db.OperationalProjectAssignmentAttachments.AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId && item.DeletedAt == null)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new OperationalProjectAssignmentAttachmentResponse(
                item.Id, item.AssignmentId, item.OriginalFileName, item.FileSize, item.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationalProjectAssignmentAttachmentResponse?> UploadAsync(
        int projectId, int assignmentId, int? designProjectId, IFormFile? file, int callerId,
        CancellationToken ct)
    {
        var assignment = await FindAssignmentAsync(projectId, assignmentId, designProjectId, ct);
        if (assignment is null) return null;
        EnsureMutable(assignment);
        if (file is null || file.Length == 0 || Path.GetFileName(file.FileName).Length is < 1 or > 255)
            throw new ProjectTeamOperationException(
                "Tệp đính kèm phải có tên từ 1 đến 255 ký tự và không được để trống.");

        var stored = await storage.StoreAsync(file, Area, ct);
        try
        {
            var content = storage.GetContent(Area, Path.GetFileName(stored.Path));
            var attachment = new OperationalProjectAssignmentAttachment
            {
                AssignmentId = assignmentId,
                Path = stored.Path,
                OriginalFileName = stored.OriginalFileName,
                FileSize = stored.FileSize,
                ContentType = content?.ContentType ?? "application/octet-stream",
                CreatedByUserId = callerId,
            };
            db.OperationalProjectAssignmentAttachments.Add(attachment);
            await db.SaveChangesAsync(ct);
            return Map(attachment);
        }
        catch
        {
            if (!storage.Delete(stored.Path, Area))
                logger.LogError("Could not clean up failed assignment attachment upload {Path}", stored.Path);
            throw;
        }
    }

    public async Task<ManagedDocumentContent?> DownloadAsync(
        int projectId, int assignmentId, long attachmentId, int? designProjectId, CancellationToken ct)
    {
        if (!await AssignmentExistsAsync(projectId, assignmentId, designProjectId, ct)) return null;
        var attachment = await db.OperationalProjectAssignmentAttachments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attachmentId && item.AssignmentId == assignmentId &&
                item.DeletedAt == null, ct);
        if (attachment is null) return null;
        var content = storage.GetContent(Area, Path.GetFileName(attachment.Path));
        return content is null ? null : content with { OriginalFileName = attachment.OriginalFileName };
    }

    public async Task<bool> DeleteAsync(
        int projectId, int assignmentId, long attachmentId, int? designProjectId, CancellationToken ct)
    {
        var assignment = await FindAssignmentAsync(projectId, assignmentId, designProjectId, ct);
        if (assignment is null) return false;
        EnsureMutable(assignment);
        var attachment = await db.OperationalProjectAssignmentAttachments.SingleOrDefaultAsync(item =>
            item.Id == attachmentId && item.AssignmentId == assignmentId, ct);
        if (attachment is null) return false;
        if (attachment.StorageDeletedAt.HasValue) return true;
        if (!storage.Delete(attachment.Path, Area))
            throw new ProjectTeamOperationException(
                "Không dọn được tệp trên máy chủ. Hãy thử xoá lại trước khi xoá công việc.");
        attachment.DeletedAt = attachment.StorageDeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> AssignmentExistsAsync(
        int projectId, int assignmentId, int? designProjectId, CancellationToken ct) =>
        db.OperationalProjectAssignments.AsNoTracking().AnyAsync(item =>
            item.Id == assignmentId && item.OperationalProjectId == projectId &&
            item.AssigneeMember.DesignProjectId == designProjectId, ct);

    private Task<OperationalProjectAssignment?> FindAssignmentAsync(
        int projectId, int assignmentId, int? designProjectId, CancellationToken ct) =>
        db.OperationalProjectAssignments.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == assignmentId && item.OperationalProjectId == projectId &&
            item.AssigneeMember.DesignProjectId == designProjectId, ct);

    private static void EnsureMutable(OperationalProjectAssignment assignment)
    {
        if (assignment.Status is ProjectAssignmentStatus.Completed
            or ProjectAssignmentStatus.Cancelled
            or ProjectAssignmentStatus.Reassigned)
        {
            throw new ProjectTeamOperationException(
                "Phân công đã hoàn tất hoặc đã huỷ là dữ liệu lịch sử nên không thể thay đổi tệp đính kèm.");
        }
    }

    private static OperationalProjectAssignmentAttachmentResponse Map(
        OperationalProjectAssignmentAttachment item) =>
        new(item.Id, item.AssignmentId, item.OriginalFileName, item.FileSize, item.CreatedAt);
}
