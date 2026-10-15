using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IDesignRecordDeletionService
{
    Task<DeletionImpactResponse?> GetImpactAsync(
        DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);

    Task<bool> DeleteAsync(
        DesignProjectResourceType resourceType,
        int resourceId,
        ConfirmDeletionRequest request,
        CancellationToken ct = default);
}

public sealed class DesignRecordDeletionService(
    AppDbContext db,
    IConceptOptionService conceptOptions,
    IBasicDesignDocService basicDocuments,
    IShopDrawingService shopDrawings,
    IIfcReleaseService ifcReleases) : IDesignRecordDeletionService
{
    public async Task<DeletionImpactResponse?> GetImpactAsync(
        DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default) =>
        await BuildImpactAsync(resourceType, resourceId, ct);

    public async Task<bool> DeleteAsync(
        DesignProjectResourceType resourceType,
        int resourceId,
        ConfirmDeletionRequest request,
        CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;

        var impact = await BuildImpactAsync(resourceType, resourceId, ct);
        if (impact is null) return false;
        Validate(impact, request);

        var reviewIds = await GetExclusiveReviewIdsAsync(resourceType, resourceId, ct);
        if (reviewIds.Count > 0)
        {
            var reviews = await db.DesignReviews
                .Where(review => reviewIds.Contains(review.Id))
                .ToListAsync(ct);
            db.DesignReviews.RemoveRange(reviews);
            await db.SaveChangesAsync(ct);
        }

        var deleted = resourceType switch
        {
            DesignProjectResourceType.ConceptOption => await conceptOptions.DeleteAsync(resourceId, ct),
            DesignProjectResourceType.BasicDesignDoc => await basicDocuments.DeleteAsync(resourceId, ct),
            DesignProjectResourceType.ShopDrawing => await shopDrawings.DeleteAsync(resourceId, ct),
            DesignProjectResourceType.IfcRelease => await ifcReleases.DeleteAsync(resourceId, ct),
            _ => throw new DesignRecordDeletionException("Loại hồ sơ thiết kế không hỗ trợ xóa vĩnh viễn."),
        };

        if (transaction is not null) await transaction.CommitAsync(ct);
        return deleted;
    }

    private async Task<DeletionImpactResponse?> BuildImpactAsync(
        DesignProjectResourceType resourceType, int resourceId, CancellationToken ct)
    {
        var root = await GetRootAsync(resourceType, resourceId, ct);
        if (root is null) return null;

        var items = new List<ImpactItem>();
        if (!root.CanDelete)
            items.Add(new ImpactItem("design.recordState", DeletionImpactActions.Block,
                [$"{resourceId}:{root.Status}"], [root.Status]));

        if (resourceType is DesignProjectResourceType.BasicDesignDoc or DesignProjectResourceType.ShopDrawing)
        {
            var targetType = resourceType == DesignProjectResourceType.BasicDesignDoc
                ? DrawingRevisionTargetType.BasicDesignDoc
                : DrawingRevisionTargetType.ShopDrawing;
            var revisions = await db.DrawingRevisions.AsNoTracking()
                .Where(revision => revision.TargetType == targetType && revision.TargetId == resourceId)
                .Select(revision => new { revision.Id, revision.RevisionNumber })
                .OrderBy(revision => revision.Id)
                .ToListAsync(ct);
            Add(items, "design.drawingRevisions", DeletionImpactActions.Delete,
                revisions.Select(revision => revision.Id.ToString()),
                revisions.Select(revision => $"Rev {revision.RevisionNumber}"));
        }

        if (resourceType == DesignProjectResourceType.ShopDrawing)
        {
            var releaseItems = await db.IfcReleaseItems.AsNoTracking()
                .Where(item => item.ShopDrawingId == resourceId)
                .Select(item => new { item.Id, item.IfcRelease.ReleaseNumber })
                .OrderBy(item => item.Id)
                .ToListAsync(ct);
            Add(items, "design.ifcReleaseItems", DeletionImpactActions.Block,
                releaseItems.Select(item => item.Id.ToString()),
                releaseItems.Select(item => item.ReleaseNumber));
        }

        if (resourceType == DesignProjectResourceType.IfcRelease)
        {
            var releaseItems = await db.IfcReleaseItems.AsNoTracking()
                .Where(item => item.IfcReleaseId == resourceId)
                .Select(item => new { item.Id, item.ShopDrawing.DrawingCode })
                .OrderBy(item => item.Id)
                .ToListAsync(ct);
            Add(items, "design.ifcReleaseItems", DeletionImpactActions.Delete,
                releaseItems.Select(item => item.Id.ToString()),
                releaseItems.Select(item => item.DrawingCode));

            var recipients = await db.IfcReleaseRecipients.AsNoTracking()
                .Where(recipient => recipient.IfcReleaseId == resourceId)
                .Select(recipient => new { recipient.Id, recipient.Name })
                .OrderBy(recipient => recipient.Id)
                .ToListAsync(ct);
            Add(items, "design.ifcRecipients", DeletionImpactActions.Delete,
                recipients.Select(recipient => recipient.Id.ToString()),
                recipients.Select(recipient => recipient.Name));
        }

        var entityType = EntityType(resourceType);
        var documents = await db.ProjectDocuments.AsNoTracking()
            .Where(document => document.SourceEntityType == entityType &&
                document.SourceRecordId == resourceId &&
                document.SyncStatus != ProjectDocumentSyncStatus.Deleted)
            .Select(document => new { document.Id, document.OriginalFileName })
            .OrderBy(document => document.Id)
            .ToListAsync(ct);
        Add(items, "design.filesPendingCleanup", DeletionImpactActions.Block,
            documents.Select(document => document.Id.ToString()),
            documents.Select(document => document.OriginalFileName));

        var reviewEvidence = await db.DesignReviews.AsNoTracking()
            .Where(review => review.Documents.Any(document =>
                document.SourceEntityTypeSnapshot == entityType &&
                document.SourceRecordIdSnapshot == resourceId))
            .Select(review => new
            {
                review.Id,
                review.Title,
                review.ClientSubmittedAt,
                IsExclusive = review.Documents.All(document =>
                    document.SourceEntityTypeSnapshot == entityType &&
                    document.SourceRecordIdSnapshot == resourceId),
                DocumentIds = review.Documents.OrderBy(document => document.ProjectDocumentId)
                    .Select(document => document.ProjectDocumentId).ToList(),
                CommentIds = review.Comments.OrderBy(comment => comment.Id)
                    .Select(comment => comment.Id).ToList(),
                RecipientIds = review.Recipients.OrderBy(recipient => recipient.UserId)
                    .Select(recipient => recipient.UserId).ToList(),
            })
            .OrderBy(review => review.Id)
            .ToListAsync(ct);

        var clientSubmitted = reviewEvidence.Where(review => review.ClientSubmittedAt.HasValue).ToList();
        Add(items, "design.clientSubmissionHistory", DeletionImpactActions.Block,
            clientSubmitted.Select(review => review.Id.ToString()),
            clientSubmitted.Select(review => review.Title));

        var exclusive = reviewEvidence
            .Where(review => review.IsExclusive && !review.ClientSubmittedAt.HasValue).ToList();
        Add(items, "design.reviewHistory", DeletionImpactActions.Delete,
            exclusive.Select(review => review.Id.ToString()), exclusive.Select(review => review.Title));
        Add(items, "design.reviewDocuments", DeletionImpactActions.Delete,
            exclusive.SelectMany(review => review.DocumentIds).Select(id => id.ToString()), []);
        Add(items, "design.reviewComments", DeletionImpactActions.Delete,
            exclusive.SelectMany(review => review.CommentIds).Select(id => id.ToString()), []);
        Add(items, "design.reviewRecipients", DeletionImpactActions.Delete,
            exclusive.SelectMany(review => review.RecipientIds)
                .Select((id, index) => $"{id}:{index}"), []);

        var shared = reviewEvidence.Where(review => !review.IsExclusive).ToList();
        Add(items, "design.sharedReviewEvidence", DeletionImpactActions.Block,
            shared.Select(review => review.Id.ToString()), shared.Select(review => review.Title));

        items.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        var tokenSource = string.Join('|', items.Select(item =>
            $"{item.Key}:{item.Action}:{string.Join(',', item.Ids)}"));
        var planToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{resourceType}:{resourceId}:{root.Label}:{root.Confirmation}:{root.Status}:{tokenSource}")))
            .ToLowerInvariant();

        return new DeletionImpactResponse
        {
            ResourceType = entityType,
            ResourceId = resourceId,
            ResourceLabel = root.Label,
            RequiredConfirmation = root.Confirmation,
            PlanToken = planToken,
            CanDelete = items.All(item => item.Action != DeletionImpactActions.Block),
            TotalAffected = 1 + items.Sum(item => item.Ids.Count),
            Items = items.Select(item => new DeletionImpactItemResponse
            {
                Key = item.Key,
                Action = item.Action,
                Count = item.Ids.Count,
                Examples = item.Labels,
            }).ToList(),
        };
    }

    private async Task<Root?> GetRootAsync(
        DesignProjectResourceType resourceType, int resourceId, CancellationToken ct) => resourceType switch
        {
            DesignProjectResourceType.ConceptOption => await db.ConceptOptions.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => new Root(item.Name, item.Name, item.Status.ToString(),
                    item.Status == ConceptOptionStatus.Discarded))
                .SingleOrDefaultAsync(ct),
            DesignProjectResourceType.BasicDesignDoc => await db.BasicDesignDocs.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => new Root($"{item.DocumentCode} · {item.Title}", item.DocumentCode,
                    item.Status.ToString(), item.Status == BasicDesignDocStatus.Rejected &&
                        item.DesignProject.CurrentStage != DesignProjectStage.Concept))
                .SingleOrDefaultAsync(ct),
            DesignProjectResourceType.ShopDrawing => await db.ShopDrawings.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => new Root($"{item.DrawingCode} · {item.Title}", item.DrawingCode,
                    item.Status.ToString(), item.Status == ShopDrawingStatus.Rejected &&
                        item.DesignProject.CurrentStage != DesignProjectStage.Concept &&
                        item.DesignProject.CurrentStage != DesignProjectStage.BasicDesign))
                .SingleOrDefaultAsync(ct),
            DesignProjectResourceType.IfcRelease => await db.IfcReleases.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => new Root($"{item.ReleaseNumber} · {item.Title}", item.ReleaseNumber,
                    item.Status.ToString(), item.Status == IfcReleaseStatus.Cancelled))
                .SingleOrDefaultAsync(ct),
            _ => null,
        };

    private async Task<List<int>> GetExclusiveReviewIdsAsync(
        DesignProjectResourceType resourceType, int resourceId, CancellationToken ct)
    {
        var entityType = EntityType(resourceType);
        return await db.DesignReviews
            .Where(review => review.Documents.Any(document =>
                    document.SourceEntityTypeSnapshot == entityType &&
                    document.SourceRecordIdSnapshot == resourceId) &&
                review.Documents.All(document =>
                    document.SourceEntityTypeSnapshot == entityType &&
                    document.SourceRecordIdSnapshot == resourceId) &&
                !review.ClientSubmittedAt.HasValue)
            .Select(review => review.Id)
            .ToListAsync(ct);
    }

    private static string EntityType(DesignProjectResourceType resourceType) => resourceType switch
    {
        DesignProjectResourceType.ConceptOption => nameof(ConceptOption),
        DesignProjectResourceType.BasicDesignDoc => nameof(BasicDesignDoc),
        DesignProjectResourceType.ShopDrawing => nameof(ShopDrawing),
        DesignProjectResourceType.IfcRelease => nameof(IfcRelease),
        _ => throw new DesignRecordDeletionException("Loại hồ sơ thiết kế không hỗ trợ xóa vĩnh viễn."),
    };

    private static void Validate(DeletionImpactResponse impact, ConfirmDeletionRequest request)
    {
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException(
                "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xóa.");
        if (!impact.CanDelete)
            throw new DesignRecordDeletionException(
                "Không thể xóa hồ sơ vì còn dữ liệu cần được dọn an toàn hoặc đang được hồ sơ khác tham chiếu.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new DesignRecordDeletionException(
                $"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
    }

    private static void Add(
        List<ImpactItem> items,
        string key,
        string action,
        IEnumerable<string> ids,
        IEnumerable<string> labels)
    {
        var distinctIds = ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (distinctIds.Count == 0) return;
        items.Add(new ImpactItem(key, action, distinctIds,
            labels.Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.Ordinal).OrderBy(label => label, StringComparer.Ordinal).Take(3).ToList()));
    }

    private sealed record Root(string Label, string Confirmation, string Status, bool CanDelete);
    private sealed record ImpactItem(string Key, string Action, List<string> Ids, List<string> Labels);
}

public sealed class DesignRecordDeletionException(string message) : Exception(message);
