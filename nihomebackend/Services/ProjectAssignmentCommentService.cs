using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProjectAssignmentCommentService(AppDbContext db)
{
    public async Task<IReadOnlyList<OperationalProjectAssignmentCommentResponse>?> ListAsync(
        int projectId, int assignmentId, int? designProjectId, CancellationToken ct)
    {
        if (!await AssignmentExistsAsync(projectId, assignmentId, designProjectId, ct)) return null;
        return await db.OperationalProjectAssignmentComments.AsNoTracking()
            .Where(comment => comment.AssignmentId == assignmentId)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new OperationalProjectAssignmentCommentResponse(
                comment.Id,
                comment.AssignmentId,
                comment.Body,
                comment.CreatedByUserId,
                comment.CreatedByUser.FullName ?? comment.CreatedByUser.Email ?? string.Empty,
                comment.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<OperationalProjectAssignmentCommentResponse?> AddAsync(
        int projectId, int assignmentId, int? designProjectId, string body, int callerId,
        CancellationToken ct)
    {
        if (!await AssignmentExistsAsync(projectId, assignmentId, designProjectId, ct)) return null;
        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ProjectTeamOperationException("Nội dung bình luận không được để trống.");

        var comment = new OperationalProjectAssignmentComment
        {
            AssignmentId = assignmentId,
            Body = trimmed,
            CreatedByUserId = callerId,
        };
        db.OperationalProjectAssignmentComments.Add(comment);
        await db.SaveChangesAsync(ct);
        await db.Entry(comment).Reference(item => item.CreatedByUser).LoadAsync(ct);
        return Map(comment);
    }

    public async Task<bool?> DeleteAsync(
        int projectId, int assignmentId, int? designProjectId, long commentId, int callerId,
        bool canManage, CancellationToken ct)
    {
        if (!await AssignmentExistsAsync(projectId, assignmentId, designProjectId, ct)) return null;
        var comment = await db.OperationalProjectAssignmentComments.SingleOrDefaultAsync(item =>
            item.Id == commentId && item.AssignmentId == assignmentId, ct);
        if (comment is null) return false;
        if (!canManage && comment.CreatedByUserId != callerId) return false;
        db.OperationalProjectAssignmentComments.Remove(comment);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> AssignmentExistsAsync(
        int projectId, int assignmentId, int? designProjectId, CancellationToken ct) =>
        db.OperationalProjectAssignments.AsNoTracking().AnyAsync(item =>
            item.Id == assignmentId && item.OperationalProjectId == projectId &&
            item.AssigneeMember.DesignProjectId == designProjectId, ct);

    private static OperationalProjectAssignmentCommentResponse Map(
        OperationalProjectAssignmentComment comment) =>
        new(
            comment.Id,
            comment.AssignmentId,
            comment.Body,
            comment.CreatedByUserId,
            comment.CreatedByUser.FullName ?? comment.CreatedByUser.Email ?? string.Empty,
            comment.CreatedAt);
}
