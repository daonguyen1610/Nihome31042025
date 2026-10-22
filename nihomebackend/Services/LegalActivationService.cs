using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public sealed class LegalActivationService(
    AppDbContext db,
    IContractService contracts,
    IOpportunityClosureInvariantService closureInvariant,
    ILogger<LegalActivationService> logger) : ILegalActivationService
{
    private const string PermitTypeCategory = "permit_type";

    public async Task<LegalActivationPreviewResponse?> PreviewContractAsync(
        int contractId, int callerUserId, CancellationToken ct = default)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contractId, ct);
        if (contract is null) return null;
        if (!contract.OperationalProjectId.HasValue)
            throw Error("LEGAL_PROJECT_REQUIRED", "Hợp đồng phải gắn với Dự án vận hành trước khi thiết lập Pháp lý.");
        return await BuildPreviewAsync(contract.OperationalProjectId.Value, callerUserId, contract, ct);
    }

    public async Task<LegalActivationPreviewResponse?> PreviewProjectAsync(
        int projectId, int callerUserId, CancellationToken ct = default)
    {
        if (!await db.OperationalProjects.AsNoTracking().AnyAsync(item => item.Id == projectId, ct)) return null;
        return await BuildPreviewAsync(projectId, callerUserId, null, ct);
    }

    public async Task<LegalActivationResultResponse?> ActivateContractAsync(
        int contractId,
        ActivateContractWithLegalRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var contract = await db.Contracts.SingleOrDefaultAsync(item => item.Id == contractId, ct);
        if (contract is null) return null;
        if (!contract.OperationalProjectId.HasValue)
            throw Error("LEGAL_PROJECT_REQUIRED", "Hợp đồng phải gắn với Dự án vận hành trước khi thiết lập Pháp lý.");

        await EnsureProjectManagerOrAdminAsync(contract.OperationalProjectId.Value, callerUserId, ct);
        var existingActivation = await db.ContractLegalActivations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ContractId == contractId, ct);
        if (existingActivation is not null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return await BuildResultAsync(contractId, existingActivation.WorkPackageId, reused: true, callerUserId, ct);
        }

        EnsureEligibleContract(contract);
        CrmConcurrency.EnsureMatches(contract.RowVersion, request.RowVersion);
        CrmConcurrency.Apply(db, contract, request.RowVersion);

        if (!await db.ContractAttachments.AsNoTracking().AnyAsync(
                item => item.ContractId == contractId && item.Kind == ContractAttachmentKind.SignedScan, ct))
            throw Error("SIGNED_SCAN_REQUIRED", "Cần đính kèm bản scan hợp đồng đã ký trước khi chuyển sang Đang thực hiện.");

        var setup = new SetupProjectLegalRequest
        {
            TemplateId = request.TemplateId,
            TemplateRowVersion = request.TemplateRowVersion,
            WorkPackageRowVersion = request.WorkPackageRowVersion,
            LegalLeadUserId = request.LegalLeadUserId,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            Permits = request.Permits,
        };
        var (package, reused) = await CreateOrExtendPackageAsync(
            contract.OperationalProjectId.Value, setup, callerUserId, ct);

        await closureInvariant.EnsureContractMutationPreservesWonAsync(
            contract, contract.OpportunityId, contract.CustomerId, ContractStatus.InProgress,
            contract.SignedDate, deleting: false, ct: ct);
        contract.Status = ContractStatus.InProgress;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedByUserId = callerUserId;
        db.ContractLegalActivations.Add(new ContractLegalActivation
        {
            ContractId = contract.Id,
            OperationalProjectId = contract.OperationalProjectId.Value,
            WorkPackageId = package.Id,
            ActivatedAt = DateTime.UtcNow,
            ActivatedByUserId = callerUserId,
            ConfigurationJson = JsonSerializer.Serialize(request),
        });
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Contract {ContractId} activated with legal package {PackageId}", contractId, package.Id);
        return await BuildResultAsync(contractId, package.Id, reused, callerUserId, ct);
    }

    public async Task<LegalActivationResultResponse?> SetupProjectAsync(
        int projectId,
        SetupProjectLegalRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (!await db.OperationalProjects.AnyAsync(item => item.Id == projectId, ct)) return null;
        await EnsureProjectManagerOrAdminAsync(projectId, callerUserId, ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var (package, reused) = await CreateOrExtendPackageAsync(projectId, request, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        var contractId = await db.Contracts.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.Status == ContractStatus.InProgress)
            .OrderBy(item => item.Id).Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
        return new LegalActivationResultResponse
        {
            Contract = contractId.HasValue
                ? (await contracts.GetAsync(contractId.Value, callerUserId, canSeeAll: true, ct))!
                : new ContractResponse(),
            WorkPackageId = package.Id,
            PermitIds = await db.PermitChecklistItems.AsNoTracking()
                .Where(item => item.OperationalProjectId == projectId).Select(item => item.Id).ToListAsync(ct),
            ReusedExistingPackage = reused,
        };
    }

    private async Task<(DepartmentWorkPackage Package, bool Reused)> CreateOrExtendPackageAsync(
        int projectId,
        SetupProjectLegalRequest request,
        int callerUserId,
        CancellationToken ct)
    {
        ValidateRequest(request);
        var project = await db.OperationalProjects.AsNoTracking()
            .SingleAsync(item => item.Id == projectId, ct);
        var members = await LoadActiveMembersAsync(projectId, ct);
        var memberByUser = members.ToDictionary(item => item.UserId);
        if (!memberByUser.TryGetValue(request.LegalLeadUserId, out var legalLead) || !legalLead.CanLeadLegal)
            throw Error("LEGAL_LEAD_INVALID", "Người chủ trì Pháp lý phải là thành viên đang hoạt động của dự án và có vai trò Pháp lý.");
        foreach (var item in request.Permits)
        {
            if (!memberByUser.ContainsKey(item.AssigneeUserId) || !memberByUser.ContainsKey(item.ApproverUserId))
                throw Error("LEGAL_MEMBER_INVALID", "Người thực hiện và người duyệt phải là thành viên đang hoạt động của đúng dự án.");
            if (item.AssigneeUserId == item.ApproverUserId)
                throw Error("LEGAL_SELF_APPROVAL", "Người thực hiện không được tự duyệt giấy phép của chính mình.");
        }

        var codes = request.Permits.Select(item => item.PermitTypeCode.Trim()).ToList();
        var validTypes = await db.MasterDataOptions.AsNoTracking()
            .Where(item => item.Category == PermitTypeCategory && item.IsActive && codes.Contains(item.Code))
            .Select(item => new { item.Code, item.Name }).ToListAsync(ct);
        if (validTypes.Count != codes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw Error("LEGAL_PERMIT_TYPE_INVALID", "Danh sách có loại giấy phép không tồn tại hoặc đã ngừng sử dụng.");
        var typeNameByCode = validTypes.ToDictionary(item => item.Code, item => item.Name,
            StringComparer.OrdinalIgnoreCase);

        var existing = await db.PermitChecklistItems
            .Where(item => item.OperationalProjectId == projectId)
            .ToDictionaryAsync(item => item.PermitTypeCode, StringComparer.OrdinalIgnoreCase, ct);
        var package = await db.DepartmentWorkPackages
            .Include(item => item.ParticipantGroups)
            .SingleOrDefaultAsync(item => item.OperationalProjectId == projectId &&
                item.Purpose == WorkPackagePurpose.LegalPermitting, ct);
        LegalChecklistTemplateResponse? template = null;
        var applyingTemplate = false;
        if (package?.LegalChecklistSnapshotJson is not null)
        {
            template = JsonSerializer.Deserialize<LegalChecklistTemplateResponse>(package.LegalChecklistSnapshotJson)!;
            if (request.TemplateId.HasValue && request.TemplateId != template.Id)
                throw Error("LEGAL_TEMPLATE_CHANGE_BLOCKED", "Không được đổi mẫu đã áp dụng cho dự án.");
        }
        else if (request.TemplateId.HasValue)
        {
            var selectedTemplate = await db.LegalChecklistTemplates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.TemplateId && x.Status == LegalChecklistTemplateStatus.Published, ct)
                ?? throw Error("LEGAL_TEMPLATE_NOT_PUBLISHED", "Chọn một mẫu Pháp lý đã được xuất bản.");
            CrmConcurrency.EnsureMatches(selectedTemplate.RowVersion, request.TemplateRowVersion);
            template = LegalChecklistTemplateService.Map(selectedTemplate);
            applyingTemplate = true;
            if (package is not null)
            {
                CrmConcurrency.EnsureMatches(package.RowVersion, request.WorkPackageRowVersion);
                CrmConcurrency.Apply(db, package, request.WorkPackageRowVersion);
            }
        }
        else if (package is null)
            throw Error("LEGAL_TEMPLATE_REQUIRED", "Phải chọn mẫu checklist Pháp lý đã xuất bản trước khi tạo gói mới.");

        if (template is not null)
        {
            if (applyingTemplate && package is not null && request.Permits.Any(item =>
                    (!existing.TryGetValue(item.PermitTypeCode.Trim(), out var existingPermit) || !existingPermit.WorkTaskId.HasValue) &&
                    (package.PlannedStart.HasValue && item.PlannedStart < package.PlannedStart ||
                     package.PlannedEnd.HasValue && item.PlannedEnd > package.PlannedEnd)))
                throw Error("LEGAL_TASK_DATES_INVALID", "Thời gian giấy phép mới phải nằm trong lịch gói Pháp lý hiện có; áp dụng mẫu không thay đổi lịch gói.");
            var missing = template.Items.Where(x => x.IsRequired &&
                !request.Permits.Any(p => string.Equals(p.PermitTypeCode.Trim(), x.PermitTypeCode, StringComparison.OrdinalIgnoreCase)) &&
                (!existing.TryGetValue(x.PermitTypeCode, out var permit) || !permit.WorkTaskId.HasValue))
                .Select(x => x.PermitTypeCode).ToList();
            if (missing.Count > 0)
                throw Error("LEGAL_REQUIRED_PERMITS_MISSING", $"Thiếu mục Pháp lý bắt buộc: {string.Join(", ", missing)}.");
        }
        if (request.Permits.Count == 0 && existing.Count == 0)
            throw Error("LEGAL_PERMITS_REQUIRED", "Phải chọn ít nhất một giấy phép.");

        var constructionGroup = await db.RoleGroups.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == "CONSTRUCTION" && item.IsActive, ct)
            ?? throw Error("LEGAL_DEPARTMENT_MISSING", "Nhóm phòng ban CONSTRUCTION chưa được cấu hình.");
        var reused = package is not null;
        if (package is null)
        {
            package = new DepartmentWorkPackage
            {
                Code = $"LEGAL-{projectId}",
                Kind = WorkPackageKind.Project,
                Purpose = WorkPackagePurpose.LegalPermitting,
                Name = $"Pháp lý và xin phép - {project.Name}",
                OperationalProjectId = projectId,
                DepartmentRoleGroupId = constructionGroup.Id,
                LeadUserId = request.LegalLeadUserId,
                Status = WorkPackageStatus.Active,
                PlannedStart = request.PlannedStart.ToUniversalTime(),
                PlannedEnd = request.PlannedEnd.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = callerUserId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = callerUserId,
            };
            db.DepartmentWorkPackages.Add(package);
            await db.SaveChangesAsync(ct);
        }
        else if (!applyingTemplate)
        {
            package.LeadUserId = request.LegalLeadUserId;
            package.PlannedStart = request.PlannedStart.ToUniversalTime();
            package.PlannedEnd = request.PlannedEnd.ToUniversalTime();
            package.UpdatedAt = DateTime.UtcNow;
            package.UpdatedByUserId = callerUserId;
        }

        if (applyingTemplate)
        {
            package.LegalChecklistTemplateId = template!.Id;
            package.LegalChecklistSnapshotJson = JsonSerializer.Serialize(template);
            package.LegalChecklistAppliedByUserId = callerUserId;
            package.LegalChecklistAppliedAt = DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;
            package.UpdatedByUserId = callerUserId;
            foreach (var permit in existing.Values)
                permit.IsRequired = template.Items.Any(x => x.IsRequired &&
                    string.Equals(x.PermitTypeCode, permit.PermitTypeCode, StringComparison.OrdinalIgnoreCase));
            db.DepartmentWorkPackageHistory.Add(new DepartmentWorkPackageHistory
            {
                WorkPackageId = package.Id,
                EntityType = "Package",
                EntityId = package.Id,
                Action = "LegalChecklistTemplateApplied",
                SnapshotJson = package.LegalChecklistSnapshotJson,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = callerUserId,
            });
        }

        var designProjectId = await db.DesignProjects.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId).OrderBy(item => item.Id)
            .Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
        var createdTaskByCode = new Dictionary<string, DepartmentWorkTask>(StringComparer.OrdinalIgnoreCase);
        var createdPermits = new List<PermitChecklistItem>();
        var linkedLegacyPermits = new List<PermitChecklistItem>();
        var nextOrder = await db.DepartmentWorkTasks.CountAsync(item => item.WorkPackageId == package.Id, ct);
        foreach (var item in request.Permits)
        {
            var code = item.PermitTypeCode.Trim();
            if (existing.TryGetValue(code, out var existingPermit) && existingPermit.WorkTaskId.HasValue) continue;
            var assignee = memberByUser[item.AssigneeUserId];
            var task = new DepartmentWorkTask
            {
                WorkPackageId = package.Id,
                WbsCode = $"L-{++nextOrder:00}",
                Title = typeNameByCode[code],
                DepartmentRoleGroupId = assignee.RoleGroupId,
                AssigneeUserId = item.AssigneeUserId,
                ApproverUserId = item.ApproverUserId,
                PlannedStart = item.PlannedStart.ToUniversalTime(),
                PlannedEnd = item.PlannedEnd.ToUniversalTime(),
                Status = WorkTaskStatus.Planned,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = callerUserId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = callerUserId,
            };
            db.DepartmentWorkTasks.Add(task);
            await db.SaveChangesAsync(ct);
            PermitChecklistItem permit;
            if (existingPermit is not null)
            {
                permit = existingPermit;
                permit.WorkTaskId = task.Id;
                permit.OwnerUserId = item.AssigneeUserId;
                permit.TargetDeadline = item.PlannedEnd.ToUniversalTime();
                if (!string.IsNullOrWhiteSpace(item.IssuingAgency))
                    permit.IssuingAgency = item.IssuingAgency.Trim();
                permit.UpdatedAt = DateTime.UtcNow;
                permit.UpdatedByUserId = callerUserId;
                linkedLegacyPermits.Add(permit);
            }
            else
            {
                permit = new PermitChecklistItem
                {
                    OperationalProjectId = projectId,
                    DesignProjectId = designProjectId,
                    WorkTaskId = task.Id,
                    PermitTypeCode = code,
                    IsRequired = template?.Items.Any(x => x.IsRequired &&
                        string.Equals(x.PermitTypeCode, code, StringComparison.OrdinalIgnoreCase)) == true,
                    IssuingAgency = TrimOrNull(item.IssuingAgency),
                    OwnerUserId = item.AssigneeUserId,
                    TargetDeadline = item.PlannedEnd.ToUniversalTime(),
                    Status = PermitStatus.NotStarted,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = callerUserId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = callerUserId,
                };
                db.PermitChecklistItems.Add(permit);
                createdPermits.Add(permit);
            }
            createdTaskByCode[code] = task;
        }
        await db.SaveChangesAsync(ct);

        var allTasksByPermit = await db.PermitChecklistItems.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.WorkTaskId.HasValue)
            .ToDictionaryAsync(item => item.PermitTypeCode, item => item.WorkTaskId!.Value,
                StringComparer.OrdinalIgnoreCase, ct);
        foreach (var item in request.Permits)
        {
            if (!createdTaskByCode.TryGetValue(item.PermitTypeCode.Trim(), out var task)) continue;
            foreach (var predecessorCode in item.PredecessorPermitTypeCodes.Distinct(StringComparer.OrdinalIgnoreCase))
                db.DepartmentWorkTaskDependencies.Add(new DepartmentWorkTaskDependency
                {
                    WorkTaskId = task.Id,
                    PredecessorTaskId = allTasksByPermit[predecessorCode.Trim()],
                });
        }
        var participantIds = request.Permits
            .SelectMany(item => new[] { memberByUser[item.AssigneeUserId].RoleGroupId, memberByUser[item.ApproverUserId].RoleGroupId })
            .Append(constructionGroup.Id).Distinct().ToList();
        var existingParticipantIds = package.ParticipantGroups.Select(item => item.RoleGroupId).ToHashSet();
        foreach (var groupId in participantIds.Where(id => !existingParticipantIds.Contains(id)))
            package.ParticipantGroups.Add(new DepartmentWorkPackageParticipantGroup { RoleGroupId = groupId });
        if (!reused)
            db.DepartmentWorkPackageHistory.Add(new DepartmentWorkPackageHistory
            {
                WorkPackageId = package.Id,
                EntityType = "Package",
                EntityId = package.Id,
                Action = "CreatedByLegalSetup",
                SnapshotJson = JsonSerializer.Serialize(new { package.Code, package.Name, package.LeadUserId }),
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = callerUserId,
            });
        foreach (var permit in createdPermits)
        {
            var task = createdTaskByCode[permit.PermitTypeCode];
            db.DepartmentWorkPackageHistory.Add(new DepartmentWorkPackageHistory
            {
                WorkPackageId = package.Id,
                EntityType = "Task",
                EntityId = task.Id,
                Action = "CreatedByLegalSetup",
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    task.WbsCode,
                    task.Title,
                    task.AssigneeUserId,
                    task.ApproverUserId,
                    task.PlannedStart,
                    task.PlannedEnd,
                }),
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = callerUserId,
            });
            db.PermitChecklistHistory.Add(new PermitChecklistHistory
            {
                PermitChecklistItemId = permit.Id,
                FromStatus = null,
                ToStatus = permit.Status,
                Action = "CreatedByLegalSetup",
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    permit.PermitTypeCode,
                    permit.IssuingAgency,
                    permit.WorkTaskId,
                }),
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = callerUserId,
            });
        }
        foreach (var permit in linkedLegacyPermits)
            db.PermitChecklistHistory.Add(new PermitChecklistHistory
            {
                PermitChecklistItemId = permit.Id,
                FromStatus = permit.Status,
                ToStatus = permit.Status,
                Action = "LinkedByLegalSetup",
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    permit.PermitTypeCode,
                    permit.IssuingAgency,
                    permit.WorkTaskId,
                }),
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = callerUserId,
            });
        await db.SaveChangesAsync(ct);
        return (package, reused);
    }

    private async Task<LegalActivationPreviewResponse> BuildPreviewAsync(
        int projectId, int callerUserId, Contract? contract, CancellationToken ct)
    {
        await EnsureProjectManagerOrAdminAsync(projectId, callerUserId, ct);
        var project = await db.OperationalProjects.AsNoTracking().SingleAsync(item => item.Id == projectId, ct);
        if (!project.ProjectManagerUserId.HasValue)
            throw Error("LEGAL_PM_REQUIRED", "Dự án phải có PM trước khi thiết lập Pháp lý.");
        var members = await LoadActiveMembersAsync(projectId, ct);
        var existingPermits = await db.PermitChecklistItems.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => new { item.PermitTypeCode, item.WorkTaskId }).ToListAsync(ct);
        var package = await db.DepartmentWorkPackages.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.Purpose == WorkPackagePurpose.LegalPermitting)
            .SingleOrDefaultAsync(ct);
        var activated = contract is not null && await db.ContractLegalActivations.AsNoTracking()
            .AnyAsync(item => item.ContractId == contract.Id, ct);
        var eligible = contract is null || IsEligibleContract(contract) || activated;
        return new LegalActivationPreviewResponse
        {
            ContractId = contract?.Id ?? 0,
            OperationalProjectId = project.Id,
            ProjectCode = project.Code,
            ProjectName = project.Name,
            ProjectManagerUserId = project.ProjectManagerUserId.Value,
            Eligible = eligible,
            AlreadyActivated = activated,
            ExistingWorkPackageId = package?.Id,
            WorkPackageRowVersion = package is null ? null : CrmConcurrency.Encode(package.RowVersion),
            ExistingLegalLeadUserId = package?.LeadUserId,
            ExistingPlannedStart = package?.PlannedStart,
            ExistingPlannedEnd = package?.PlannedEnd,
            AppliedTemplate = package?.LegalChecklistSnapshotJson is null ? null :
                JsonSerializer.Deserialize<LegalChecklistTemplateResponse>(package.LegalChecklistSnapshotJson),
            TemplateAppliedAt = package?.LegalChecklistAppliedAt,
            TemplateAppliedByUserId = package?.LegalChecklistAppliedByUserId,
            Templates = await new LegalChecklistTemplateService(db).ListAsync(true, ct),
            ReasonCode = eligible ? null : "LEGAL_CONTRACT_NOT_ELIGIBLE",
            Message = eligible ? null : "Chỉ hợp đồng Upstream loại Thiết kế, Thi công hoặc D&B ở trạng thái Đã ký mới kích hoạt Pháp lý.",
            PermitTypes = (await db.MasterDataOptions.AsNoTracking()
                .Where(item => item.Category == PermitTypeCategory && item.IsActive)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
                .Select(item => new { item.Code, item.Name })
                .ToListAsync(ct))
                .Select(item => new LegalPermitTypeOptionResponse
                {
                    Code = item.Code,
                    Name = item.Name,
                    AlreadyExists = existingPermits.Any(existing => existing.PermitTypeCode == item.Code),
                    RequiresSetup = existingPermits.Any(existing => existing.PermitTypeCode == item.Code &&
                        !existing.WorkTaskId.HasValue),
                }).ToList(),
            ProjectMembers = members,
        };
    }

    private async Task<List<LegalProjectMemberOptionResponse>> LoadActiveMembersAsync(int projectId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.OperationalProjectMembers.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.User.IsActive &&
                item.StartedAt <= now && (!item.EndedAt.HasValue || item.EndedAt > now))
            .Select(item => new LegalProjectMemberOptionResponse
            {
                UserId = item.UserId,
                Name = item.User.FullName ?? item.User.Email,
                Email = item.User.Email!,
                RoleGroupId = item.User.RoleEntity != null && item.User.RoleEntity.RoleGroupId.HasValue
                    ? item.User.RoleEntity.RoleGroupId.Value : 0,
                RoleGroupCode = item.User.RoleEntity != null && item.User.RoleEntity.RoleGroup != null
                    ? item.User.RoleEntity.RoleGroup.Code : string.Empty,
                CanLeadLegal = item.Roles.Any(role => role.RoleCode == ProjectTeamRoleCode.LegalOfficer &&
                    role.StartedAt <= now && (!role.EndedAt.HasValue || role.EndedAt > now)) ||
                    item.User.RoleEntity != null && item.User.RoleEntity.Code == "LEGAL_OFFICER",
            }).Where(item => item.RoleGroupId > 0).ToListAsync(ct);
    }

    private async Task EnsureProjectManagerOrAdminAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        var admin = await db.Users.AsNoTracking().AnyAsync(item => item.Id == callerUserId && item.IsActive &&
            (item.Role == UserRole.ADMIN || item.Role == UserRole.SUPER_ADMIN || item.RoleEntity != null &&
                item.RoleEntity.IsActive && item.RoleEntity.IsSystem &&
                (item.RoleEntity.Code == "ADMIN" || item.RoleEntity.Code == "SUPER_ADMIN")), ct);
        if (admin) return;
        if (!await db.OperationalProjects.AsNoTracking().AnyAsync(
                item => item.Id == projectId && item.ProjectManagerUserId == callerUserId, ct))
            throw Error("LEGAL_ACTIVATION_FORBIDDEN", "Chỉ PM của Dự án hoặc Admin được thiết lập Pháp lý.");
    }

    private static void ValidateRequest(SetupProjectLegalRequest request)
    {
        if (request.PlannedEnd < request.PlannedStart)
            throw Error("LEGAL_PACKAGE_DATES_INVALID", "Ngày kết thúc gói Pháp lý không được trước ngày bắt đầu.");
        var codes = request.Permits.Select(item => item.PermitTypeCode.Trim()).ToList();
        if (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count)
            throw Error("LEGAL_PERMIT_DUPLICATE", "Mỗi loại giấy phép chỉ được chọn một lần.");
        foreach (var item in request.Permits)
        {
            if (item.PlannedEnd < item.PlannedStart || item.PlannedStart < request.PlannedStart || item.PlannedEnd > request.PlannedEnd)
                throw Error("LEGAL_TASK_DATES_INVALID", "Thời gian từng giấy phép phải hợp lệ và nằm trong thời gian của gói Pháp lý.");
            foreach (var predecessor in item.PredecessorPermitTypeCodes)
                if (!codes.Contains(predecessor.Trim(), StringComparer.OrdinalIgnoreCase) ||
                    string.Equals(predecessor.Trim(), item.PermitTypeCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw Error("LEGAL_PREDECESSOR_INVALID", "Công tác trước phải là một giấy phép khác trong cùng cấu hình.");
        }
        var map = request.Permits.ToDictionary(item => item.PermitTypeCode.Trim(),
            item => item.PredecessorPermitTypeCodes.Select(code => code.Trim()).ToList(), StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Visit(string code)
        {
            if (!active.Add(code)) return true;
            if (done.Contains(code)) { active.Remove(code); return false; }
            if (map[code].Any(Visit)) return true;
            active.Remove(code); done.Add(code); return false;
        }
        if (map.Keys.Any(Visit)) throw Error("LEGAL_PREDECESSOR_CYCLE", "Quan hệ công tác trước tạo thành vòng lặp.");
    }

    private async Task<LegalActivationResultResponse> BuildResultAsync(
        int contractId, int packageId, bool reused, int callerUserId, CancellationToken ct)
    {
        var contract = await contracts.GetAsync(contractId, callerUserId, canSeeAll: true, ct)
            ?? throw Error("LEGAL_CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng sau khi kích hoạt.");
        var projectId = await db.DepartmentWorkPackages.AsNoTracking()
            .Where(item => item.Id == packageId).Select(item => item.OperationalProjectId!.Value).SingleAsync(ct);
        return new LegalActivationResultResponse
        {
            Contract = contract,
            WorkPackageId = packageId,
            PermitIds = await db.PermitChecklistItems.AsNoTracking()
                .Where(item => item.OperationalProjectId == projectId).Select(item => item.Id).ToListAsync(ct),
            ReusedExistingPackage = reused,
        };
    }

    private static bool IsEligibleContract(Contract contract) =>
        contract.Direction == ContractDirection.Upstream && contract.Status == ContractStatus.Signed &&
        contract.Type is ContractType.Design or ContractType.Construction or ContractType.DesignAndBuild;

    private static void EnsureEligibleContract(Contract contract)
    {
        if (!IsEligibleContract(contract))
            throw Error("LEGAL_CONTRACT_NOT_ELIGIBLE", "Chỉ hợp đồng Upstream loại Thiết kế, Thi công hoặc D&B ở trạng thái Đã ký mới kích hoạt Pháp lý.");
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static LegalActivationException Error(string code, string message) => new(code, message);
}
