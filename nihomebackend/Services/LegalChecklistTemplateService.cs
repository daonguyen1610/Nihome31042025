using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class LegalChecklistTemplateService(AppDbContext db)
{
    public async Task<List<LegalChecklistTemplateResponse>> ListAsync(bool publishedOnly, CancellationToken ct)
    {
        var query = db.LegalChecklistTemplates.AsNoTracking();
        if (publishedOnly) query = query.Where(x => x.Status == LegalChecklistTemplateStatus.Published);
        return (await query.OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<LegalChecklistTemplateResponse?> GetAsync(int id, CancellationToken ct)
    {
        var entity = await db.LegalChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<LegalChecklistTemplateResponse?> SaveAsync(int? id, SaveLegalChecklistTemplateRequest request, int actor, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        LegalChecklistTemplate entity;
        if (id.HasValue)
        {
            entity = (await db.LegalChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct))!;
            if (entity is null) return null;
            RequireDraft(entity);
            CrmConcurrency.EnsureMatches(entity.RowVersion, request.RowVersion);
            CrmConcurrency.Apply(db, entity, request.RowVersion);
            if (!string.Equals(entity.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                throw Error("LEGAL_TEMPLATE_CODE_IMMUTABLE", "Không được đổi mã mẫu. Hãy tạo mẫu mới.");
        }
        else
        {
            if (await db.LegalChecklistTemplates.AnyAsync(x => x.Code == request.Code.Trim().ToUpper(), ct))
                throw Error("LEGAL_TEMPLATE_CODE_EXISTS", "Mã mẫu đã tồn tại. Hãy tạo phiên bản mới.");
            entity = new LegalChecklistTemplate { Code = request.Code.Trim().ToUpperInvariant(), CreatedByUserId = actor };
            db.LegalChecklistTemplates.Add(entity);
        }
        entity.NameVi = request.NameVi.Trim(); entity.Name = request.Name.Trim();
        entity.NameZh = request.NameZh.Trim(); entity.NameJa = request.NameJa.Trim();
        entity.ScopeVi = request.ScopeVi.Trim(); entity.Scope = request.Scope.Trim();
        entity.ScopeZh = request.ScopeZh.Trim(); entity.ScopeJa = request.ScopeJa.Trim();
        var authors = Definition(entity.ItemsJson).Authors.Append(entity.CreatedByUserId)
            .Append(entity.UpdatedByUserId).Append(actor).Where(id => id > 0).Distinct().ToList();
        // Contributor IDs live with the definition, independently of audit-log retention.
        entity.UpdatedByUserId = actor;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.ItemsJson = JsonSerializer.Serialize(new StoredDefinition(request.Items.Select(x => new LegalChecklistTemplateItem
        {
            PermitTypeCode = x.PermitTypeCode.Trim(),
            IsRequired = x.IsRequired,
        }).ToList(), authors));
        await PersistAsync(entity, actor, id.HasValue ? "DraftUpdated" : "DraftCreated", ct);
        return Map(entity);
    }

    public async Task<LegalChecklistTemplateResponse?> NewVersionAsync(int id, string token, int actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await db.LegalChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (source is null) return null;
        CrmConcurrency.EnsureMatches(source.RowVersion, token);
        if (source.Status == LegalChecklistTemplateStatus.Draft)
            throw Error("LEGAL_TEMPLATE_DRAFT_EXISTS", "Hãy chỉnh sửa bản nháp hiện có.");
        if (await db.LegalChecklistTemplates.AnyAsync(x => x.Code == source.Code && x.Status == LegalChecklistTemplateStatus.Draft, ct))
            throw Error("LEGAL_TEMPLATE_DRAFT_EXISTS", "Mã mẫu đã có phiên bản nháp.");
        var entity = new LegalChecklistTemplate
        {
            Code = source.Code,
            Version = await db.LegalChecklistTemplates.Where(x => x.Code == source.Code).MaxAsync(x => x.Version, ct) + 1,
            NameVi = source.NameVi,
            Name = source.Name,
            NameZh = source.NameZh,
            NameJa = source.NameJa,
            ScopeVi = source.ScopeVi,
            Scope = source.Scope,
            ScopeZh = source.ScopeZh,
            ScopeJa = source.ScopeJa,
            ItemsJson = JsonSerializer.Serialize(new StoredDefinition(Items(source.ItemsJson), [actor])),
            CreatedByUserId = actor,
            UpdatedByUserId = actor,
        };
        db.LegalChecklistTemplates.Add(entity);
        await PersistAsync(entity, actor, "VersionCreated", ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Map(entity);
    }

    public async Task<LegalChecklistTemplateResponse?> DecideAsync(int id, LegalChecklistTemplateDecisionRequest request, int actor, bool retire, CancellationToken ct)
    {
        var entity = await db.LegalChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        CrmConcurrency.EnsureMatches(entity.RowVersion, request.RowVersion);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        if (retire)
        {
            if (entity.Status != LegalChecklistTemplateStatus.Published)
                throw Error("LEGAL_TEMPLATE_STATE_INVALID", "Chỉ ngừng sử dụng mẫu đã xuất bản.");
            entity.Status = LegalChecklistTemplateStatus.Retired;
        }
        else
        {
            RequireDraft(entity);
            if (actor == entity.CreatedByUserId || actor == entity.UpdatedByUserId || Definition(entity.ItemsJson).Authors.Contains(actor))
                throw Error("LEGAL_TEMPLATE_SELF_APPROVAL", "Người soạn hoặc chỉnh sửa mẫu không được tự xuất bản.");
            if (string.IsNullOrWhiteSpace(request.ApprovalReference) || request.ApprovalReference.Length > 1000)
                throw Error("LEGAL_TEMPLATE_APPROVAL_REQUIRED", "Nguồn phê duyệt CM/NICON là bắt buộc, tối đa 1.000 ký tự.");
            await ValidateTypesAsync(Items(entity.ItemsJson), ct);
            entity.Status = LegalChecklistTemplateStatus.Published;
            entity.PublishedByUserId = actor; entity.PublishedAt = DateTime.UtcNow;
            entity.ApprovalReference = request.ApprovalReference.Trim();
        }
        entity.UpdatedAt = DateTime.UtcNow;
        await PersistAsync(entity, actor, retire ? "Retired" : "Published", ct);
        return Map(entity);
    }

    private async Task ValidateAsync(SaveLegalChecklistTemplateRequest request, CancellationToken ct)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            throw Error("LEGAL_TEMPLATE_INVALID", string.Join(" ", results.Select(x => x.ErrorMessage)));
        await ValidateTypesAsync(request.Items, ct);
    }

    private async Task ValidateTypesAsync(List<LegalChecklistTemplateItem> items, CancellationToken ct)
    {
        if (items.Count is < 1 or > 100 || items.Any(x => x is null || string.IsNullOrWhiteSpace(x.PermitTypeCode) || x.PermitTypeCode.Trim().Length > 60))
            throw Error("LEGAL_TEMPLATE_ITEMS_INVALID", "Chọn từ 1 đến 100 loại giấy phép hợp lệ.");
        var codes = items.Select(x => x.PermitTypeCode.Trim()).ToList();
        if (!items.Any(x => x.IsRequired) || codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count)
            throw Error("LEGAL_TEMPLATE_ITEMS_INVALID", "Mẫu cần ít nhất một mục bắt buộc và không được trùng loại giấy phép.");
        var active = await db.MasterDataOptions.AsNoTracking()
            .Where(x => x.Category == "permit_type" && x.IsActive).Select(x => x.Code).ToListAsync(ct);
        if (codes.Any(code => !active.Contains(code, StringComparer.OrdinalIgnoreCase)))
            throw Error("LEGAL_PERMIT_TYPE_INVALID", "Mẫu có loại giấy phép không tồn tại hoặc đã ngừng sử dụng.");
        foreach (var item in items)
            item.PermitTypeCode = active.Single(code => string.Equals(code, item.PermitTypeCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task PersistAsync(LegalChecklistTemplate entity, int actor, string action, CancellationToken ct)
    {
        // A single SaveChanges keeps the definition and its audit in the same transaction.
        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ActorUserId = actor,
            Action = $"legal.template.{action}",
            ResourceType = "LegalChecklistTemplate",
            ResourceId = $"{entity.Code}:{entity.Version}",
            Message = action,
            NewValueJson = JsonSerializer.Serialize(new
            {
                entity.Code,
                entity.Version,
                entity.Status,
                entity.ItemsJson,
                entity.NameVi,
                entity.Name,
                entity.NameZh,
                entity.NameJa,
                entity.ScopeVi,
                entity.Scope,
                entity.ScopeZh,
                entity.ScopeJa,
                entity.ApprovalReference
            }),
        });
        await CrmConcurrency.SaveChangesAsync(db, ct);
    }

    private sealed record StoredDefinition(List<LegalChecklistTemplateItem> Items, List<int> Authors);

    private static StoredDefinition Definition(string json)
    {
        using var document = JsonDocument.Parse(json);
        // Array definitions are retained for existing seed/fixture compatibility.
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new(JsonSerializer.Deserialize<List<LegalChecklistTemplateItem>>(json)!, [])
            : JsonSerializer.Deserialize<StoredDefinition>(json)!;
    }

    public static List<LegalChecklistTemplateItem> Items(string json) => Definition(json).Items;
    public static LegalChecklistTemplateResponse Map(LegalChecklistTemplate x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Version = x.Version,
        NameVi = x.NameVi,
        Name = x.Name,
        NameZh = x.NameZh,
        NameJa = x.NameJa,
        ScopeVi = x.ScopeVi,
        Scope = x.Scope,
        ScopeZh = x.ScopeZh,
        ScopeJa = x.ScopeJa,
        Status = x.Status.ToString(),
        Items = Items(x.ItemsJson),
        CreatedByUserId = x.CreatedByUserId,
        UpdatedByUserId = x.UpdatedByUserId,
        PublishedByUserId = x.PublishedByUserId,
        PublishedAt = x.PublishedAt,
        ApprovalReference = x.ApprovalReference,
        RowVersion = CrmConcurrency.Encode(x.RowVersion),
    };

    private static void RequireDraft(LegalChecklistTemplate entity)
    {
        if (entity.Status != LegalChecklistTemplateStatus.Draft)
            throw Error("LEGAL_TEMPLATE_IMMUTABLE", "Mẫu đã xuất bản không được sửa. Hãy tạo phiên bản mới.");
    }
    private static LegalActivationException Error(string code, string message) => new(code, message);
}
