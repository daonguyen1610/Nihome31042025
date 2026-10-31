using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class WorkTaskAttachmentService(
    AppDbContext db,
    IDepartmentWorkPackageService packages,
    IBusinessDocumentStorageService storage,
    ILogger<WorkTaskAttachmentService> logger)
{
    private const BusinessDocumentArea Area = BusinessDocumentArea.WorkTasks;

    public async Task<bool> CanUploadAsync(int packageId, int taskId, int callerId,
        bool canViewAll, CancellationToken ct)
    {
        var visible = await VisibleTaskAsync(packageId, taskId, callerId, canViewAll, ct);
        return visible is not null && CanWrite(visible.Value.Package, visible.Value.Task, callerId);
    }

    public async Task<IReadOnlyList<DepartmentWorkTaskAttachmentResponse>?> ListAsync(
        int packageId, int taskId, int callerId, bool canViewAll, CancellationToken ct)
    {
        if (await VisibleTaskAsync(packageId, taskId, callerId, canViewAll, ct) is null) return null;
        return await db.DepartmentWorkTaskAttachments.AsNoTracking()
            .Where(item => item.WorkTaskId == taskId && item.DeletedAt == null)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new DepartmentWorkTaskAttachmentResponse(
                item.Id, item.WorkTaskId, item.OriginalFileName, item.FileSize, item.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<DepartmentWorkTaskAttachmentResponse?> UploadAsync(
        int packageId, int taskId, IFormFile? file, int callerId, bool canViewAll, CancellationToken ct)
    {
        var visible = await VisibleTaskAsync(packageId, taskId, callerId, canViewAll, ct);
        if (visible is null) return null;
        EnsureCanWrite(visible.Value.Package, visible.Value.Task, callerId);
        if (file is null || file.Length == 0 || Path.GetFileName(file.FileName).Length is < 1 or > 255)
            throw new DepartmentWorkPackageOperationException(
                "Tệp đính kèm phải có tên từ 1 đến 255 ký tự và không được để trống.");

        var stored = await storage.StoreAsync(file, Area, ct);
        try
        {
            var content = storage.GetContent(Area, Path.GetFileName(stored.Path));
            var attachment = new DepartmentWorkTaskAttachment
            {
                WorkTaskId = taskId,
                Path = stored.Path,
                OriginalFileName = stored.OriginalFileName,
                FileSize = stored.FileSize,
                ContentType = content?.ContentType ?? "application/octet-stream",
                CreatedByUserId = callerId,
            };
            db.DepartmentWorkTaskAttachments.Add(attachment);
            await db.SaveChangesAsync(ct);
            return Map(attachment);
        }
        catch
        {
            if (!storage.Delete(stored.Path, Area))
                logger.LogError("Could not clean up failed task attachment upload {Path}", stored.Path);
            throw;
        }
    }

    public async Task<ManagedDocumentContent?> DownloadAsync(
        int packageId, int taskId, long attachmentId, int callerId, bool canViewAll, CancellationToken ct)
    {
        if (await VisibleTaskAsync(packageId, taskId, callerId, canViewAll, ct) is null) return null;
        var attachment = await db.DepartmentWorkTaskAttachments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attachmentId && item.WorkTaskId == taskId &&
                item.DeletedAt == null, ct);
        if (attachment is null) return null;
        var content = storage.GetContent(Area, Path.GetFileName(attachment.Path));
        return content is null ? null : content with { OriginalFileName = attachment.OriginalFileName };
    }

    public async Task<bool> DeleteAsync(
        int packageId, int taskId, long attachmentId, int callerId, bool canViewAll, CancellationToken ct)
    {
        var visible = await VisibleTaskAsync(packageId, taskId, callerId, canViewAll, ct);
        if (visible is null) return false;
        if (!CanDelete(visible.Value.Package, visible.Value.Task, callerId))
            throw new DepartmentWorkPackageScopeException(
                "Không có quyền xóa tệp ở đầu việc này.");
        var attachment = await db.DepartmentWorkTaskAttachments.SingleOrDefaultAsync(item =>
            item.Id == attachmentId && item.WorkTaskId == taskId, ct);
        if (attachment is null) return false;
        if (attachment.StorageDeletedAt.HasValue) return true;
        if (!storage.Delete(attachment.Path, Area))
            throw new DepartmentWorkPackageOperationException(
                "Không dọn được tệp trên máy chủ. Hãy thử xóa lại trước khi xóa nhóm công việc.");
        attachment.DeletedAt = attachment.StorageDeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<(DepartmentWorkPackageResponse Package, DepartmentWorkTaskResponse Task)?> VisibleTaskAsync(
        int packageId, int taskId, int callerId, bool canViewAll, CancellationToken ct)
    {
        var package = await packages.GetAsync(packageId, callerId, canViewAll, ct);
        var task = package?.Tasks.SingleOrDefault(item => item.Id == taskId);
        return package is null || task is null ? null : (package, task);
    }

    private static void EnsureCanWrite(DepartmentWorkPackageResponse package,
        DepartmentWorkTaskResponse task, int callerId)
    {
        if (!CanWrite(package, task, callerId))
            throw new DepartmentWorkPackageScopeException(
                "Không có quyền thêm hoặc xóa tệp ở đầu việc này.");
    }

    private static bool CanWrite(DepartmentWorkPackageResponse package,
        DepartmentWorkTaskResponse task, int callerId) =>
        package.Status is not ("Completed" or "Cancelled") && task.Status != "Cancelled" &&
        (package.CanAssignTasks || task.AssigneeUserId == callerId);

    private static bool CanDelete(DepartmentWorkPackageResponse package,
        DepartmentWorkTaskResponse task, int callerId) =>
        package.CanAssignTasks || CanWrite(package, task, callerId);

    private static DepartmentWorkTaskAttachmentResponse Map(DepartmentWorkTaskAttachment item) =>
        new(item.Id, item.WorkTaskId, item.OriginalFileName, item.FileSize, item.CreatedAt);
}
