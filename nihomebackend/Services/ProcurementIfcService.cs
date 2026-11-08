using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Services;

public sealed record ProcurementIfcDrawing(int ItemId, string Code, string Title, bool HasFile);
public sealed record ProcurementIfcPacket(int RecipientId, int ReleaseId, string ReleaseNumber, string Title,
    DateTime? ReleaseDate, string DossierCode, DateTime? AcknowledgedAt, int? AcknowledgedByUserId,
    string? AcknowledgementNote, string RowVersion, List<ProcurementIfcDrawing> Drawings);
public sealed record ProcurementIfcAcknowledgement(ProcurementIfcPacket Packet, bool Recorded);
public sealed record ProcurementIfcCandidate(int UserId, string Name);

public sealed class ProcurementIfcService(AppDbContext db, IPermissionService permissions)
{
    public const string ViewPermission = "proc.ifc.view";
    public const string AcknowledgePermission = "proc.ifc.acknowledge";

    private IQueryable<OperationalProjectMember> Members(int projectId) => db.OperationalProjectMembers
        .Where(m => m.OperationalProjectId == projectId && m.EndedAt == null &&
            m.StartedAt <= DateTime.UtcNow && m.User.IsActive);

    public async Task<bool> CanReceiveAsync(int designProjectId, int userId, CancellationToken ct)
    {
        var projectId = await db.DesignProjects.Where(x => x.Id == designProjectId)
            .Select(x => x.OperationalProjectId).SingleOrDefaultAsync(ct);
        return projectId.HasValue && await Members(projectId.Value).AnyAsync(x => x.UserId == userId, ct) &&
            await permissions.HasAsync(userId, ViewPermission, ct);
    }

    public async Task<List<ProcurementIfcCandidate>> CandidatesAsync(int designProjectId, CancellationToken ct)
    {
        var projectId = await db.DesignProjects.Where(x => x.Id == designProjectId)
            .Select(x => x.OperationalProjectId).SingleOrDefaultAsync(ct);
        if (!projectId.HasValue) return [];
        var candidates = await Members(projectId.Value)
            .Select(m => new { m.UserId, Name = m.User.FullName ?? m.User.PhoneNumber })
            .Distinct().OrderBy(x => x.Name).ToListAsync(ct);
        var allowed = await permissions.FilterByRolePermissionAsync(candidates.Select(x => x.UserId).ToArray(), ViewPermission, ct);
        return candidates.Where(x => allowed.Contains(x.UserId)).Select(x => new ProcurementIfcCandidate(x.UserId, x.Name)).ToList();
    }

    public async Task<bool> IsMemberAsync(int projectId, int userId, CancellationToken ct) =>
        await Members(projectId).AnyAsync(x => x.UserId == userId, ct);

    private IQueryable<IfcReleaseRecipient> Addressed(int projectId, int userId) => db.IfcReleaseRecipients
        .Where(x => x.RecipientUserId == userId && x.IfcRelease.Status == IfcReleaseStatus.Released &&
            x.IfcRelease.ReleaseDate != null && x.IfcRelease.DesignProject.OperationalProjectId == projectId);

    public async Task<object> ListAsync(int projectId, int userId, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var query = Addressed(projectId, userId);
        var total = await query.CountAsync(ct);
        var rows = await query.AsNoTracking().Include(x => x.IfcRelease).ThenInclude(x => x.DesignProject)
            .Include(x => x.IfcRelease).ThenInclude(x => x.Items).ThenInclude(x => x.ShopDrawing)
            .OrderByDescending(x => x.IfcRelease.ReleaseDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        return new { total, page, pageSize = 20, items = rows.Select(Map).ToList() };
    }

    public async Task<ProcurementIfcAcknowledgement?> AcknowledgeAsync(int projectId, int recipientId, int userId,
        AcknowledgeIfcReleaseRecipientRequest request, CancellationToken ct)
    {
        var row = await Addressed(projectId, userId).Include(x => x.IfcRelease).ThenInclude(x => x.DesignProject)
            .Include(x => x.IfcRelease).ThenInclude(x => x.Items).ThenInclude(x => x.ShopDrawing)
            .SingleOrDefaultAsync(x => x.Id == recipientId, ct);
        if (row is null) return null;
        if (row.AcknowledgedAt.HasValue) return new(Map(row), false);
        CrmConcurrency.Apply(db, row, request.RowVersion);
        row.AcknowledgedAt = DateTime.UtcNow;
        row.AcknowledgedByUserId = userId;
        row.AcknowledgementNote = string.IsNullOrWhiteSpace(request.AcknowledgementNote) ? null : request.AcknowledgementNote.Trim();
        try { await CrmConcurrency.SaveChangesAsync(db, ct); }
        catch (CrmConcurrencyException)
        {
            await db.Entry(row).ReloadAsync(ct);
            if (db.Entry(row).State != EntityState.Detached && row.AcknowledgedAt.HasValue && row.AcknowledgedByUserId == userId) return new(Map(row), false);
            throw;
        }
        return new(Map(row), true);
    }

    public Task<IfcReleaseItem?> ItemAsync(int projectId, int recipientId, int itemId, int userId, CancellationToken ct) =>
        Addressed(projectId, userId).Where(x => x.Id == recipientId).SelectMany(x => x.IfcRelease.Items)
            .Where(x => x.Id == itemId && x.ShopDrawing.DesignProjectId == x.IfcRelease.DesignProjectId &&
                x.ShopDrawing.Status == ShopDrawingStatus.Released)
            .Include(x => x.ShopDrawing).AsNoTracking().SingleOrDefaultAsync(ct);

    private static ProcurementIfcPacket Map(IfcReleaseRecipient row) => new(row.Id, row.IfcReleaseId,
        row.IfcRelease.ReleaseNumber, row.IfcRelease.Title,
        row.IfcRelease.ReleaseDate.HasValue ? DateTime.SpecifyKind(row.IfcRelease.ReleaseDate.Value, DateTimeKind.Utc) : null,
        row.IfcRelease.DesignProject.ProjectCode,
        row.AcknowledgedAt.HasValue ? DateTime.SpecifyKind(row.AcknowledgedAt.Value, DateTimeKind.Utc) : null, row.AcknowledgedByUserId,
        row.AcknowledgementNote, CrmConcurrency.Encode(row.RowVersion),
        row.IfcRelease.Items.Select(x => new ProcurementIfcDrawing(x.Id, x.ShopDrawing.DrawingCode,
            x.ShopDrawing.Title, !string.IsNullOrWhiteSpace(x.ShopDrawing.FilePath))).ToList());
}
