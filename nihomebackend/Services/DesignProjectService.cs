using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Services;

/// <summary>
/// M2 DesignProject service — see <see cref="IDesignProjectService"/>.
/// </summary>
public class DesignProjectService(
    AppDbContext db,
    IPermitChecklistService permitChecklistService,
    ILogger<DesignProjectService> logger,
    IProjectAccessService projectAccess,
    ILegacyProjectTeamSyncService projectTeamSync,
    IProjectHardDeletePlanService hardDeletePlans,
    IHardDeleteOperationService hardDeleteOperations) : IDesignProjectService
{
    private const int MaxPageSize = 100;

    public async Task<DesignProjectListResponse> ListAsync(
        DesignProjectListParams p,
        int callerUserId,
        CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.DesignProjects
            .AsNoTracking()
            .Include(dp => dp.Customer)
            .Include(dp => dp.Contract)
            .Include(dp => dp.ProjectManager)
            .Include(dp => dp.DesignLead)
            .AsQueryable();

        if (!await projectAccess.HasAdministrativeBypassAsync(callerUserId, ct))
        {
            var accessibleProjectIds = await projectAccess.GetAccessibleOperationalProjectIdsAsync(callerUserId, ct);
            q = q.Where(dp =>
                dp.OperationalProjectId.HasValue && accessibleProjectIds.Contains(dp.OperationalProjectId.Value) ||
                !dp.OperationalProjectId.HasValue &&
                (dp.ProjectManagerUserId == callerUserId || dp.DesignLeadUserId == callerUserId));
        }

        if (p.CustomerId.HasValue) q = q.Where(dp => dp.CustomerId == p.CustomerId.Value);
        if (p.ContractId.HasValue) q = q.Where(dp => dp.ContractId == p.ContractId.Value);
        if (p.ProjectManagerUserId.HasValue) q = q.Where(dp => dp.ProjectManagerUserId == p.ProjectManagerUserId.Value);
        if (p.DesignLeadUserId.HasValue) q = q.Where(dp => dp.DesignLeadUserId == p.DesignLeadUserId.Value);

        if (!string.IsNullOrWhiteSpace(p.Stage))
        {
            var stages = ParseEnumCsv<DesignProjectStage>(p.Stage);
            if (stages.Count > 0) q = q.Where(dp => stages.Contains(dp.CurrentStage));
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = ParseEnumCsv<DesignProjectStatus>(p.Status);
            if (statuses.Count > 0) q = q.Where(dp => statuses.Contains(dp.Status));
        }

        if (p.DeadlineFrom.HasValue) q = q.Where(dp => dp.Deadline >= p.DeadlineFrom.Value);
        if (p.DeadlineTo.HasValue) q = q.Where(dp => dp.Deadline <= p.DeadlineTo.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(dp => EF.Functions.Like(dp.Name, $"%{term}%")
                            || EF.Functions.Like(dp.ProjectCode, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(dp => dp.UpdatedAt)
            .ThenByDescending(dp => dp.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new DesignProjectListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(MapListItem).ToList(),
        };
    }

    public async Task<DesignProjectResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.DesignProjects
            .AsNoTracking()
            .Include(dp => dp.Customer)
            .Include(dp => dp.Contract)
            .Include(dp => dp.ProjectManager)
            .Include(dp => dp.DesignLead)
            .FirstOrDefaultAsync(dp => dp.Id == id, ct);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<DesignProjectResponse> CreateAsync(CreateDesignProjectRequest request, int callerUserId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DesignProjectOperationException("Tên dự án là bắt buộc.");
        }
        await EnsureRelationsAsync(request, excludeDesignProjectId: null, ct);
        var allocationYear = DateTime.UtcNow.Year;
        await using var allocationTransaction = await BeginCodeAllocationAsync(allocationYear, ct);

        var entity = new DesignProject
        {
            OperationalProjectId = request.OperationalProjectId,
            ProjectCode = await NextCodeAsync(allocationYear, ct),
            Name = name,
            CustomerId = request.CustomerId,
            ContractId = request.ContractId,
            ProjectManagerUserId = request.ProjectManagerUserId,
            DesignLeadUserId = request.DesignLeadUserId,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            CurrentStage = DesignProjectStage.Concept,
            Status = DesignProjectStatus.Active,
            Note = TrimOrNull(request.Note),
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DesignProjects.Add(entity);
        if (entity.OperationalProjectId.HasValue)
        {
            await projectTeamSync.SyncDesignProjectRolesAsync(
                entity.OperationalProjectId.Value,
                entity.ProjectManagerUserId,
                entity.DesignLeadUserId,
                callerUserId,
                ct);
        }
        await db.SaveChangesAsync(ct);
        if (allocationTransaction is not null)
        {
            await allocationTransaction.CommitAsync(ct);
        }

        logger.LogInformation("DesignProject {Id} ({Code}) created by user {UserId}",
            entity.Id, entity.ProjectCode, callerUserId);

        await SeedPermitChecklistAsync(entity.Id, callerUserId, ct);

        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<DesignProjectResponse?> UpdateAsync(int id, UpdateDesignProjectRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == id, ct);
        if (entity is null) return null;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DesignProjectOperationException("Tên dự án là bắt buộc.");
        }

        request.OperationalProjectId ??= entity.OperationalProjectId;
        if (request.OperationalProjectId != entity.OperationalProjectId)
        {
            throw new DesignProjectOperationException(
                "Không thể chuyển dự án thiết kế sang Dự án vận hành khác.");
        }
        await EnsureRelationsAsync(request, id, ct);

        entity.Name = name;
        entity.OperationalProjectId = request.OperationalProjectId;
        entity.CustomerId = request.CustomerId;
        entity.ContractId = request.ContractId;
        entity.ProjectManagerUserId = request.ProjectManagerUserId;
        entity.DesignLeadUserId = request.DesignLeadUserId;
        entity.StartDate = request.StartDate;
        entity.Deadline = request.Deadline;
        entity.Note = TrimOrNull(request.Note);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<DesignProjectStatus>(request.Status, true, out var status))
            {
                throw new DesignProjectOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
            }
            entity.Status = status;
        }

        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        if (entity.OperationalProjectId.HasValue)
        {
            await projectTeamSync.SyncDesignProjectRolesAsync(
                entity.OperationalProjectId.Value,
                entity.ProjectManagerUserId,
                entity.DesignLeadUserId,
                callerUserId,
                ct);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("DesignProject {Id} updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(
        int id, CancellationToken ct = default) =>
        (await hardDeletePlans.ForDesignProjectAsync(id, ct))?.Impact;

    public async Task<HardDeleteOperationResult?> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (!await db.DesignProjects.AsNoTracking().AnyAsync(dp => dp.Id == id, ct)) return null;
        var plan = await hardDeletePlans.ForDesignProjectAsync(id, ct);
        if (plan is null) return null;
        ValidateDeletionConfirmation(plan.Impact, request);
        var operation = await hardDeleteOperations.CreateAsync(new CreateHardDeleteOperationRequest(
            EntityTypes.DesignProject,
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.Impact.ResourceLabel,
            plan.Impact.PlanToken,
            request.Confirmation!,
            callerUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.Items), ct);
        var result = await hardDeleteOperations.ProcessAsync(operation.OperationId, ct);
        logger.LogInformation("DesignProject {Id} durable hard-delete is {Status}", id, result.Status);
        return result;
    }

    private static void ValidateDeletionConfirmation(
        DeletionImpactResponse impact,
        ConfirmDeletionRequest request)
    {
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException(
                "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!impact.CanDelete)
            throw new DesignProjectOperationException(
                "Không thể xoá Dự án vì còn dữ liệu cần được dọn an toàn trước.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new DesignProjectOperationException(
                $"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
    }

    public async Task<DesignProjectResponse> EnsureForContractAsync(Contract contract, int? callerUserId, CancellationToken ct = default)
    {
        var allocationYear = DateTime.UtcNow.Year;
        await using var allocationTransaction = await BeginCodeAllocationAsync(allocationYear, ct);
        var existing = await db.DesignProjects
            .FirstOrDefaultAsync(dp => dp.ContractId == contract.Id, ct);
        if (existing is not null)
        {
            return (await GetAsync(existing.Id, ct))!;
        }

        var entity = new DesignProject
        {
            OperationalProjectId = contract.OperationalProjectId,
            ProjectCode = await NextCodeAsync(allocationYear, ct),
            // Auto-created rows get a predictable, human-friendly name
            // derived from the contract number so the operator can find
            // it in the list without opening the contract. They can
            // rename it later via the edit form.
            Name = $"Dự án hợp đồng {contract.ContractNumber}",
            CustomerId = contract.CustomerId,
            ContractId = contract.Id,
            ProjectManagerUserId = null,
            DesignLeadUserId = null,
            StartDate = contract.StartDate,
            Deadline = contract.EndDate,
            CurrentStage = DesignProjectStage.Concept,
            Status = DesignProjectStatus.Active,
            Note = $"Tạo tự động từ hợp đồng {contract.ContractNumber}.",
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DesignProjects.Add(entity);
        await db.SaveChangesAsync(ct);
        if (allocationTransaction is not null)
        {
            await allocationTransaction.CommitAsync(ct);
        }
        logger.LogInformation(
            "DesignProject {Id} ({Code}) auto-created for contract {ContractId} ({ContractNumber})",
            entity.Id, entity.ProjectCode, contract.Id, contract.ContractNumber);

        await SeedPermitChecklistAsync(entity.Id, callerUserId, ct);

        return (await GetAsync(entity.Id, ct))!;
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<IReadOnlyCollection<ProjectDocumentMoveDescriptor>> GetMoveDescriptorsAsync(
        DesignProject project, CancellationToken ct)
    {
        var files = new List<ProjectDocumentMoveDescriptor>();
        var basicDocuments = await db.BasicDesignDocs
            .Where(document => document.DesignProjectId == project.Id && document.FilePath != null)
            .ToListAsync(ct);
        files.AddRange(basicDocuments.Select(document => new ProjectDocumentMoveDescriptor(
                ProjectDocumentCategory.DesignBasic, ProjectDocumentSourceModule.Design,
                nameof(BasicDesignDoc), "file", document.Id, document.FilePath!,
                document.OriginalFileName ?? Path.GetFileName(document.FilePath!),
                project.CustomerId, project.ContractId)));
        var shopDrawings = await db.ShopDrawings
            .Where(document => document.DesignProjectId == project.Id && document.FilePath != null)
            .ToListAsync(ct);
        files.AddRange(shopDrawings.Select(document => new ProjectDocumentMoveDescriptor(
                ProjectDocumentCategory.DesignShopDrawing, ProjectDocumentSourceModule.Design,
                nameof(ShopDrawing), "file", document.Id, document.FilePath!,
                document.OriginalFileName ?? Path.GetFileName(document.FilePath!),
                project.CustomerId, project.ContractId)));

        var permits = await db.PermitChecklistItems
            .Where(document => document.DesignProjectId == project.Id &&
                (document.SubmittedFilePath != null || document.IssuedFilePath != null))
            .ToListAsync(ct);
        foreach (var permit in permits)
        {
            if (!string.IsNullOrWhiteSpace(permit.SubmittedFilePath))
                files.Add(new(ProjectDocumentCategory.LegalPermits, ProjectDocumentSourceModule.Design,
                    nameof(PermitChecklistItem), "submittedPackage", permit.Id, permit.SubmittedFilePath,
                    Path.GetFileName(permit.SubmittedFilePath), project.CustomerId, project.ContractId));
            if (!string.IsNullOrWhiteSpace(permit.IssuedFilePath))
                files.Add(new(ProjectDocumentCategory.LegalPermits, ProjectDocumentSourceModule.Design,
                    nameof(PermitChecklistItem), "issuedPermit", permit.Id, permit.IssuedFilePath,
                    Path.GetFileName(permit.IssuedFilePath), project.CustomerId, project.ContractId));
        }

        var acceptanceRecords = await db.AcceptanceRecords
            .Where(record => record.DesignProjectId == project.Id && record.Documents != null)
            .ToListAsync(ct);
        foreach (var record in acceptanceRecords)
            foreach (var path in ManagedPaths(record.Documents, "/files/business-documents/acceptance/"))
                files.Add(new(ProjectDocumentCategory.ConstructionAcceptance, ProjectDocumentSourceModule.Acceptance,
                    nameof(AcceptanceRecord), "documents", record.Id, path, Path.GetFileName(path),
                    project.CustomerId, project.ContractId));

        var asBuiltDocuments = await db.AsBuiltDocuments
            .Where(document => document.DesignProjectId == project.Id && document.FileUrl != null)
            .ToListAsync(ct);
        foreach (var document in asBuiltDocuments)
            if (document.FileUrl!.StartsWith("/files/business-documents/as-built/", StringComparison.OrdinalIgnoreCase))
                files.Add(new(ProjectDocumentCategory.ConstructionAcceptance, ProjectDocumentSourceModule.Acceptance,
                    nameof(AsBuiltDocument), "file", document.Id, document.FileUrl,
                    Path.GetFileName(document.FileUrl), project.CustomerId, project.ContractId));

        var handovers = await db.HandoverRecords
            .Where(record => record.DesignProjectId == project.Id && record.Documents != null)
            .ToListAsync(ct);
        foreach (var record in handovers)
            foreach (var path in ManagedPaths(record.Documents, "/files/business-documents/handover/"))
                files.Add(new(ProjectDocumentCategory.ConstructionAcceptance, ProjectDocumentSourceModule.Handover,
                    nameof(HandoverRecord), "documents", record.Id, path, Path.GetFileName(path),
                    project.CustomerId, project.ContractId));
        return files;
    }

    private static IEnumerable<string> ManagedPaths(string? json, string prefix)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        List<string>? paths;
        try { paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json); }
        catch (System.Text.Json.JsonException) { yield break; }
        if (paths is null) yield break;
        foreach (var path in paths.Select(value => value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)))
            if (path!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) yield return path;
    }

    /// <summary>
    /// Auto-generate the M3 permit checklist for a freshly created design
    /// project. Best-effort: a downstream failure never blocks the design
    /// project create path (the operator can retry via the "Regenerate"
    /// button on the permits page).
    /// </summary>
    private async Task SeedPermitChecklistAsync(int designProjectId, int? callerUserId, CancellationToken ct)
    {
        try
        {
            await permitChecklistService.EnsureForProjectAsync(designProjectId, callerUserId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to seed permit checklist for design project {ProjectId}", designProjectId);
        }
    }

    private async Task EnsureRelationsAsync(
        CreateDesignProjectRequest request,
        int? excludeDesignProjectId,
        CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            throw new DesignProjectOperationException($"Khách hàng #{request.CustomerId} không tồn tại.");
        }
        if (request.ContractId.HasValue)
        {
            var contract = await db.Contracts
                .AsNoTracking()
                .Where(item => item.Id == request.ContractId.Value)
                .Select(item => new { item.CustomerId, item.OperationalProjectId })
                .SingleOrDefaultAsync(ct);
            if (contract is null)
            {
                throw new DesignProjectOperationException(
                    $"Hợp đồng #{request.ContractId} không tồn tại.");
            }
            if (contract.CustomerId != request.CustomerId)
            {
                throw new DesignProjectOperationException(
                    "Dự án thiết kế và Hợp đồng phải thuộc cùng một Khách hàng.");
            }
            if (request.OperationalProjectId.HasValue &&
                contract.OperationalProjectId.HasValue &&
                request.OperationalProjectId != contract.OperationalProjectId)
            {
                throw new DesignProjectOperationException(
                    "Dự án thiết kế và Hợp đồng phải thuộc cùng một Dự án vận hành.");
            }
            request.OperationalProjectId ??= contract.OperationalProjectId;
        }
        if (request.OperationalProjectId.HasValue)
        {
            var projectCustomerId = await db.OperationalProjects
                .AsNoTracking()
                .Where(item => item.Id == request.OperationalProjectId.Value)
                .Select(item => (int?)item.CustomerId)
                .SingleOrDefaultAsync(ct);
            if (!projectCustomerId.HasValue)
            {
                throw new DesignProjectOperationException(
                    $"Dự án #{request.OperationalProjectId.Value} không tồn tại.");
            }
            if (projectCustomerId.Value != request.CustomerId)
            {
                throw new DesignProjectOperationException(
                    "Dự án thiết kế và Dự án vận hành phải thuộc cùng một Khách hàng.");
            }
            var alreadyLinked = await db.DesignProjects
                .AsNoTracking()
                .AnyAsync(item =>
                    item.OperationalProjectId == request.OperationalProjectId.Value &&
                    (!excludeDesignProjectId.HasValue || item.Id != excludeDesignProjectId.Value), ct);
            if (alreadyLinked)
            {
                throw new DesignProjectOperationException(
                    "Dự án vận hành đã có một luồng thiết kế.");
            }
        }
        if (request.ProjectManagerUserId.HasValue &&
            !await db.Users.AnyAsync(u =>
                u.Id == request.ProjectManagerUserId.Value && u.IsActive, ct))
        {
            throw new DesignProjectOperationException(
                $"PM #{request.ProjectManagerUserId} không tồn tại hoặc đã ngừng hoạt động.");
        }
        if (request.DesignLeadUserId.HasValue &&
            !await db.Users.AnyAsync(u =>
                u.Id == request.DesignLeadUserId.Value && u.IsActive, ct))
        {
            throw new DesignProjectOperationException(
                $"Design Lead #{request.DesignLeadUserId} không tồn tại hoặc đã ngừng hoạt động.");
        }
        if (request.StartDate.HasValue && request.Deadline.HasValue &&
            request.Deadline.Value < request.StartDate.Value)
        {
            throw new DesignProjectOperationException("Deadline phải sau ngày bắt đầu.");
        }
    }

    private async Task<string> NextCodeAsync(int year, CancellationToken ct)
    {
        var prefix = $"DP-{year}-";
        var existingCodes = await db.DesignProjects
            .Where(dp => dp.ProjectCode.StartsWith(prefix))
            .Select(dp => dp.ProjectCode)
            .ToListAsync(ct);
        var next = existingCodes
            .Select(code => code.Length > prefix.Length
                && int.TryParse(code[prefix.Length..], out var sequence)
                    ? sequence
                    : 0)
            .DefaultIfEmpty()
            .Max() + 1;
        return $"{prefix}{next:D4}";
    }

    private async Task<IDbContextTransaction?> BeginCodeAllocationAsync(int year, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;

        var isSqlServer = string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.Ordinal);
        var transaction = await db.Database.BeginTransactionAsync(
            isSqlServer ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            ct);

        if (!isSqlServer) return transaction;

        try
        {
            var resource = $"design-project-code-{year}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000;
                IF @result < 0
                    THROW 51000, 'Unable to acquire the design project code allocation lock.', 1;
                """, ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static List<TEnum> ParseEnumCsv<TEnum>(string csv) where TEnum : struct, Enum
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<TEnum>(s, true, out var v) ? (TEnum?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static DesignProjectListItemResponse MapListItem(DesignProject dp) => new()
    {
        Id = dp.Id,
        OperationalProjectId = dp.OperationalProjectId,
        ProjectCode = dp.ProjectCode,
        Name = dp.Name,
        CustomerId = dp.CustomerId,
        CustomerName = dp.Customer?.Name,
        ContractId = dp.ContractId,
        ContractNumber = dp.Contract?.ContractNumber,
        ProjectManagerUserId = dp.ProjectManagerUserId,
        ProjectManagerName = dp.ProjectManager?.FullName,
        DesignLeadUserId = dp.DesignLeadUserId,
        DesignLeadName = dp.DesignLead?.FullName,
        StartDate = dp.StartDate,
        Deadline = dp.Deadline,
        CurrentStage = dp.CurrentStage.ToString(),
        Status = dp.Status.ToString(),
        UpdatedAt = dp.UpdatedAt,
    };

    private static DesignProjectResponse MapDetail(DesignProject dp) => new()
    {
        Id = dp.Id,
        OperationalProjectId = dp.OperationalProjectId,
        ProjectCode = dp.ProjectCode,
        Name = dp.Name,
        CustomerId = dp.CustomerId,
        CustomerName = dp.Customer?.Name,
        ContractId = dp.ContractId,
        ContractNumber = dp.Contract?.ContractNumber,
        ProjectManagerUserId = dp.ProjectManagerUserId,
        ProjectManagerName = dp.ProjectManager?.FullName,
        DesignLeadUserId = dp.DesignLeadUserId,
        DesignLeadName = dp.DesignLead?.FullName,
        StartDate = dp.StartDate,
        Deadline = dp.Deadline,
        CurrentStage = dp.CurrentStage.ToString(),
        Status = dp.Status.ToString(),
        Note = dp.Note,
        CreatedAt = dp.CreatedAt,
        UpdatedAt = dp.UpdatedAt,
    };
}
