using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public partial class ConstructionTaskService
{
    public async Task<ConstructionTaskDrawingsResponse?> GetDrawingsAsync(int id, CancellationToken ct = default)
    {
        var task = await db.ConstructionTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return null;
        var available = await ReleasedDrawings(task.DesignProjectId).ToListAsync(ct);
        var linkedIds = await db.ConstructionTaskDrawings.AsNoTracking()
            .Where(x => x.ConstructionTaskId == id).Select(x => x.IfcReleaseItemId).ToListAsync(ct);
        return new ConstructionTaskDrawingsResponse
        {
            Version = task.DrawingLinksVersion,
            Available = available,
            Linked = available.Where(x => linkedIds.Contains(x.IfcReleaseItemId)).ToList(),
        };
    }

    public async Task<ConstructionTaskDrawingsResponse?> SetDrawingsAsync(int id,
        SetConstructionTaskDrawingsRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (request.Version is null || request.IfcReleaseItemIds is null ||
            request.IfcReleaseItemIds.Count > 200 || request.IfcReleaseItemIds.Any(x => x <= 0) ||
            request.IfcReleaseItemIds.Distinct().Count() != request.IfcReleaseItemIds.Count)
            throw new ConstructionTaskOperationException(
                "Danh sách bản vẽ cần tối đa 200 ID dương, không trùng, và phiên bản dữ liệu hợp lệ.");

        var task = await db.ConstructionTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return null;
        var links = await db.ConstructionTaskDrawings.Where(x => x.ConstructionTaskId == id).ToListAsync(ct);
        var requested = request.IfcReleaseItemIds.ToHashSet();
        // A retry of the same desired state is harmless, including after a lost response.
        if (links.Select(x => x.IfcReleaseItemId).ToHashSet().SetEquals(requested))
            return await GetDrawingsAsync(id, ct);
        if (task.DrawingLinksVersion != request.Version.Value)
            throw new CrmConcurrencyException("Liên kết bản vẽ đã thay đổi. Vui lòng tải lại trước khi lưu.");

        var validIds = await ReleasedDrawings(task.DesignProjectId)
            .Where(x => requested.Contains(x.IfcReleaseItemId)).Select(x => x.IfcReleaseItemId).ToListAsync(ct);
        if (validIds.Count != requested.Count)
            throw new ConstructionTaskOperationException(
                "Bản vẽ phải thuộc cùng dự án và nằm trong gói IFC đã phát hành.");

        db.ConstructionTaskDrawings.RemoveRange(links.Where(x => !requested.Contains(x.IfcReleaseItemId)));
        var existing = links.Select(x => x.IfcReleaseItemId).ToHashSet();
        db.ConstructionTaskDrawings.AddRange(requested.Where(x => !existing.Contains(x)).Select(itemId =>
            new ConstructionTaskDrawing
            {
                ConstructionTaskId = id,
                IfcReleaseItemId = itemId,
                CreatedByUserId = callerUserId,
            }));
        task.DrawingLinksVersion = Guid.NewGuid();
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetDrawingsAsync(id, ct);
    }

    private IQueryable<ConstructionDrawingResponse> ReleasedDrawings(int designProjectId) =>
        db.IfcReleaseItems.AsNoTracking()
            .Where(x => x.IfcRelease.DesignProjectId == designProjectId &&
                x.ShopDrawing.DesignProjectId == designProjectId &&
                x.IfcRelease.Status == IfcReleaseStatus.Released &&
                x.IfcRelease.ReleaseDate != null && x.ShopDrawing.Status == ShopDrawingStatus.Released)
            .OrderBy(x => x.ShopDrawing.DrawingCode).ThenBy(x => x.IfcRelease.ReleaseNumber)
            .Select(x => new ConstructionDrawingResponse
            {
                IfcReleaseItemId = x.Id,
                IfcReleaseId = x.IfcReleaseId,
                ReleaseNumber = x.IfcRelease.ReleaseNumber,
                ReleaseDate = x.IfcRelease.ReleaseDate,
                ShopDrawingId = x.ShopDrawingId,
                DrawingCode = x.ShopDrawing.DrawingCode,
                Title = x.ShopDrawing.Title,
                ConstructionItem = x.ShopDrawing.ConstructionItem,
                FileName = x.ShopDrawing.OriginalFileName,
                ContentType = x.ShopDrawing.ContentType,
                HasFile = x.ShopDrawing.FilePath != null,
                // R0 is the original drawing. Later revision metadata cannot relabel a released packet.
                RevisionNumber = db.DrawingRevisions.Where(r => r.TargetType == DrawingRevisionTargetType.ShopDrawing &&
                    r.TargetId == x.ShopDrawingId && r.CreatedAt <= x.IfcRelease.ReleaseDate)
                    .Select(r => (int?)r.RevisionNumber).Max() ?? 0,
            });
}
