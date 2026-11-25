using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Internal contract review (NICON feedback rows 8–9): Draft → UnderReview → Approved.
/// The chain is the people tagged by name, then the "contracts / sign" workflow in
/// Cấu hình → Luồng duyệt, decided one step at a time with the quote chain mechanism.
/// Only the current step's reviewer advances it; the author never reviews.
/// </summary>
public sealed class ContractReviewService(AppDbContext db, IPermissionService permissions,
    IContractService contracts, INotificationService notifications, ILogger<ContractReviewService> logger)
{
    public const string Module = "contracts";
    public const string Action = "sign";
    private const string TaggedStepName = "Review nội bộ";
    private static readonly string[] ReviewPermissions = ["crm.contracts.view"];

    public async Task<ContractReviewChainResponse> ChainAsync(int contractId, int callerId, CancellationToken ct)
    {
        var ownerId = await db.Contracts.AsNoTracking().Where(c => c.Id == contractId)
            .Select(c => c.OwnerUserId).FirstOrDefaultAsync(ct);
        int[] excluded = ownerId is int owner ? [callerId, owner] : [callerId];
        var steps = await QuoteApprovalChain.WithCandidatesAsync(db, permissions,
            await QuoteApprovalChain.ConfiguredAsync(db, Module, Action, ct), excluded, ReviewPermissions, ct);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !excluded.Contains(u.Id) && u.RoleEntity != null && u.RoleEntity.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Email, u.RoleEntity!.Code, u.RoleEntity.Name, u.RoleEntity.LabelKey })
            .ToListAsync(ct);
        var permitted = await permissions.FilterByRolePermissionsAsync(users.Select(u => u.Id).ToList(),
            ReviewPermissions, ct);
        var reviewers = users.Where(u => permitted.Contains(u.Id)).OrderBy(u => u.FullName ?? u.Email)
            .Select(u => new QuoteApprovalCandidateResponse(u.Id, u.FullName ?? u.Email, u.Email, u.Code, u.Name,
                u.LabelKey, null, null)).ToList();
        return new ContractReviewChainResponse(steps, reviewers);
    }

    public async Task<ContractResponse?> SubmitAsync(int id, SubmitContractReviewRequest req, int callerId,
        bool canSeeAll, CancellationToken ct)
    {
        var entity = await db.Contracts.FindAsync([id], ct);
        if (entity is null || !canSeeAll && entity.OwnerUserId != callerId) return null;
        CrmConcurrency.Apply(db, entity, req.RowVersion);
        if (entity.Status != ContractStatus.Draft)
            throw new ContractValidationException("Chỉ gửi xem xét Hợp đồng đang Nháp.", "CONTRACT_REVIEW_NOT_DRAFT");
        if (entity.Type == ContractType.Unclassified)
            throw new ContractValidationException("Vui lòng phân loại Hợp đồng trước khi chuyển trạng thái.");

        var chain = await ChainAsync(id, callerId, ct);
        var steps = new List<QuoteApprovalStep>();
        foreach (var reviewerId in req.ReviewerUserIds)
        {
            var person = chain.Reviewers.FirstOrDefault(r => r.Id == reviewerId)
                ?? throw new ContractValidationException(
                    "Người được tag review phải đang hoạt động, có quyền xem hợp đồng và khác người gửi, người phụ trách.",
                    "CONTRACT_REVIEW_INVALID_REVIEWER");
            steps.Add(new QuoteApprovalStep(steps.Count + 1, TaggedStepName, "", null, 24, person.Id, person.Name,
                null, null));
        }
        var picks = new Queue<int>(req.ApproverUserIds);
        for (var index = 0; index < chain.Steps.Count; index++)
        {
            var step = chain.Steps[index];
            var people = step.EveryoneApproves ? step.Candidates
                : picks.TryDequeue(out var chosen) && step.Candidates.FirstOrDefault(c => c.Id == chosen) is { } person
                    ? [person] : [];
            if (people.Count == 0)
                throw new ContractValidationException(
                    "Mỗi bước của luồng Ký hợp đồng cần một người duyệt đang hoạt động, đúng vai trò, có quyền xem hợp đồng và khác người gửi.",
                    "CONTRACT_REVIEW_STEP_UNCHOSEN");
            steps.AddRange(people.Select(p => new QuoteApprovalStep(step.Order, step.Name, step.RoleCode,
                step.RoleLabelKey, step.SlaHours, p.Id, p.Name, null, null)));
        }
        if (steps.Count == 0)
            throw new ContractValidationException(
                "Chọn ít nhất một người review, hoặc nhờ quản trị bật luồng \"Ký hợp đồng\" trong Cấu hình → Luồng duyệt.",
                "CONTRACT_REVIEW_EMPTY");
        if (steps.GroupBy(s => s.ApproverUserId).Any(g => g.Count() > 1))
            throw new ContractValidationException("Mỗi người chỉ duyệt một bước. Chọn người khác cho các bước còn lại.",
                "CONTRACT_REVIEW_PERSON_TWICE");

        steps[0] = steps[0] with { StartedAt = DateTime.UtcNow };
        entity.ReviewStepsJson = QuoteApprovalChain.Write(steps);
        entity.ReviewStepIndex = 0;
        entity.ReviewSubmittedByUserId = callerId;
        entity.Status = ContractStatus.UnderReview;
        Touch(entity, callerId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        await NotifyAsync(steps[0].ApproverUserId, "contract.review-requested", entity, null);
        return await contracts.GetAsync(id, callerId, canSeeAll: true, ct);
    }

    /// <summary>The current reviewer approves: the next step starts, or the last one approves the contract.</summary>
    public async Task<ContractResponse?> ApproveAsync(int id, ContractReviewDecisionRequest req, int callerId,
        CancellationToken ct)
    {
        var (entity, steps, index) = await CurrentStepAsync(id, req.RowVersion, callerId, ct);
        if (entity is null) return null;
        var now = DateTime.UtcNow;
        steps[index] = steps[index] with { DecidedAt = now };
        var last = index == steps.Count - 1;
        if (!last) steps[index + 1] = steps[index + 1] with { StartedAt = now };
        entity.ReviewStepsJson = QuoteApprovalChain.Write(steps);
        entity.ReviewStepIndex = last ? null : index + 1;
        if (last) entity.Status = ContractStatus.Approved;
        Touch(entity, callerId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (last)
            await NotifyAsync(entity.ReviewSubmittedByUserId, "contract.review-approved", entity, null);
        else
            await NotifyAsync(steps[index + 1].ApproverUserId, "contract.review-requested", entity, null);
        return await contracts.GetAsync(id, callerId, canSeeAll: true, ct);
    }

    /// <summary>The current reviewer sends the contract back to Draft with a reason; the chain ends.</summary>
    public async Task<ContractResponse?> RejectAsync(int id, ContractReviewDecisionRequest req, int callerId,
        CancellationToken ct)
    {
        var reason = req.Note?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ContractValidationException("Nhập lý do trả lại để người soạn biết cần sửa gì.",
                "CONTRACT_REVIEW_REASON_REQUIRED");
        var (entity, _, _) = await CurrentStepAsync(id, req.RowVersion, callerId, ct);
        if (entity is null) return null;
        var submitter = entity.ReviewSubmittedByUserId;
        ClearReview(entity);
        entity.Status = ContractStatus.Draft;
        Touch(entity, callerId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        await NotifyAsync(submitter, "contract.review-rejected", entity, reason);
        return await contracts.GetAsync(id, callerId, canSeeAll: true, ct);
    }

    public static void ClearReview(Contract entity)
    {
        entity.ReviewStepsJson = null;
        entity.ReviewStepIndex = null;
        entity.ReviewSubmittedByUserId = null;
    }

    /// <summary>Whether the user is anywhere in the contract's review chain, so they may read it.</summary>
    public static bool InChain(string? stepsJson, int userId) =>
        QuoteApprovalChain.Read(stepsJson).Any(step => step.ApproverUserId == userId);

    private async Task<(Contract? Entity, List<QuoteApprovalStep> Steps, int Index)> CurrentStepAsync(int id,
        string? rowVersion, int callerId, CancellationToken ct)
    {
        var entity = await db.Contracts.FindAsync([id], ct);
        if (entity is null || !InChain(entity.ReviewStepsJson, callerId)) return (null, [], 0);
        CrmConcurrency.Apply(db, entity, rowVersion);
        var steps = QuoteApprovalChain.Read(entity.ReviewStepsJson);
        if (entity.Status != ContractStatus.UnderReview || entity.ReviewStepIndex is not int index ||
            index < 0 || index >= steps.Count)
            throw new ContractValidationException("Hợp đồng không còn chờ xem xét. Tải lại trang.",
                "CONTRACT_REVIEW_NOT_PENDING");
        var current = steps[index];
        if (current.ApproverUserId != callerId)
            throw new ContractValidationException("Chưa đến lượt bạn: bước này đang chờ người khác xem xét.",
                "CONTRACT_REVIEW_NOT_YOUR_TURN");
        // The author never reviews their own contract, even if they joined the role later.
        if (callerId == entity.ReviewSubmittedByUserId || callerId == entity.OwnerUserId ||
            (await QuoteApprovalChain.CanDecideAsync(db, permissions, [(callerId, current.RoleCode)],
                ReviewPermissions, ct)).Count == 0)
            throw new ContractValidationException(
                "Bạn không còn đủ điều kiện xem xét bước này (vai trò hoặc quyền xem hợp đồng đã đổi).",
                "CONTRACT_REVIEW_NOT_ALLOWED");
        return (entity, steps, index);
    }

    private static void Touch(Contract entity, int callerId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerId;
    }

    private async Task NotifyAsync(int? userId, string template, Contract entity, string? reason)
    {
        if (userId is not int recipient) return;
        try
        {
            await notifications.NotifyFromTemplateAsync(recipient, template,
                new Dictionary<string, string>
                {
                    ["contractNumber"] = entity.ContractNumber,
                    ["reason"] = reason ?? "",
                },
                "Contract", entity.Id, $"/admin/contracts/{entity.Id}");
        }
        catch (Exception ex)
        {
            // The decision is saved; a failed notice must not undo it.
            logger.LogWarning(ex, "Contract {ContractId} notification {Template} failed.", entity.Id, template);
        }
    }
}
