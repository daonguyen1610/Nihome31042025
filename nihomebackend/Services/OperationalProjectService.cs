using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class OperationalProjectService(
    AppDbContext db,
    ILegacyProjectTeamSyncService projectTeamSync,
    IProjectDocumentStagingService projectDocuments,
    ILogger<OperationalProjectService> logger) : IOperationalProjectService
{
    private const int MaxPageSize = 100;

    public async Task<OperationalProjectListResponse> ListAsync(
        OperationalProjectListParams parameters,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, MaxPageSize);
        var query = db.OperationalProjects.AsNoTracking().AsQueryable();

        if (!canSeeAll)
        {
            query = query.Where(project =>
                project.ProjectManagerUserId == callerUserId ||
                project.CreatedByUserId == callerUserId ||
                project.TeamMembers.Any(member => member.UserId == callerUserId && member.EndedAt == null));
        }
        else if (parameters.ProjectManagerUserId.HasValue)
        {
            query = query.Where(project =>
                project.ProjectManagerUserId == parameters.ProjectManagerUserId.Value);
        }

        if (parameters.CustomerId.HasValue)
        {
            query = query.Where(project => project.CustomerId == parameters.CustomerId.Value);
        }
        if (parameters.Status.HasValue)
        {
            query = query.Where(project => project.Status == parameters.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = $"%{parameters.Search.Trim()}%";
            query = query.Where(project =>
                EF.Functions.Like(project.Code, term) ||
                EF.Functions.Like(project.Name, term) ||
                EF.Functions.Like(project.Customer.Name, term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(project => project.UpdatedAt)
            .ThenByDescending(project => project.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(project => new OperationalProjectListItemResponse
            {
                Id = project.Id,
                Code = project.Code,
                Name = project.Name,
                CustomerId = project.CustomerId,
                CustomerName = project.Customer.Name,
                ProjectManagerUserId = project.ProjectManagerUserId,
                ProjectManagerName = project.ProjectManager != null
                    ? project.ProjectManager.FullName
                    : null,
                Status = project.Status.ToString(),
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                OpportunityCount = project.Opportunities.Count,
                QuoteCount = project.Quotes.Count,
                ContractCount = project.Contracts.Count,
                UpdatedAt = project.UpdatedAt,
            })
            .ToListAsync(ct);

        return new OperationalProjectListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        };
    }

    public async Task<OperationalProjectResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Customer)
            .Include(item => item.ProjectManager)
            .Include(item => item.DesignProject)
            .Include(item => item.Opportunities)
                .ThenInclude(o => o.Customer)
            .Include(item => item.Opportunities)
                .ThenInclude(o => o.Owner)
            .Include(item => item.Quotes)
                .ThenInclude(q => q.Owner)
            .Include(item => item.Quotes)
                .ThenInclude(q => q.Opportunity)
                .ThenInclude(o => o!.Customer)
            .Include(item => item.Quotes)
                .ThenInclude(q => q.Items)
            .Include(item => item.Contracts)
                .ThenInclude(c => c.Customer)
            .Include(item => item.Contracts)
                .ThenInclude(c => c.Owner)
            .Include(item => item.TeamMembers)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (project is null || !CanView(project, callerUserId, canSeeAll)) return null;
        return Map(project);
    }

    public async Task<IReadOnlyList<OperationalProjectTimelineItemResponse>?> GetTimelineAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects
            .AsNoTracking()
            .Include(item => item.TeamMembers)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (project is null || !CanView(project, callerUserId, canSeeAll)) return null;

        var milestones = await db.ContractPaymentMilestones
            .AsNoTracking()
            .Where(item => item.Contract.OperationalProjectId == id)
            .Select(item => new
            {
                item.Id,
                item.ContractId,
                item.Contract.ContractNumber,
                item.Order,
                item.Name,
                item.PercentValue,
                ContractValue = item.Contract.Value,
                item.DueDate,
                item.ActualPaymentDate,
                item.Status,
                item.Note,
                item.UpdatedAt,
            })
            .ToListAsync(ct);

        return milestones
            .OrderBy(item => item.DueDate.HasValue ? 0 : 1)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.ContractNumber)
            .ThenBy(item => item.Order)
            .Select(item => new OperationalProjectTimelineItemResponse
            {
                Id = item.Id,
                ContractId = item.ContractId,
                ContractNumber = item.ContractNumber,
                Order = item.Order,
                Name = item.Name,
                PercentValue = item.PercentValue,
                Amount = Math.Round(
                    item.ContractValue * item.PercentValue / 100m,
                    2,
                    MidpointRounding.AwayFromZero),
                PlannedDate = item.DueDate,
                ActualDate = item.ActualPaymentDate,
                Status = item.Status.ToString(),
                Source = nameof(ContractPaymentMilestone),
                Note = item.Note,
                UpdatedAt = item.UpdatedAt,
            })
            .ToList();
    }

    public async Task<OperationalProjectResponse> CreateAsync(
        CreateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        await ValidateAsync(request, callerUserId, canSeeAll, ct);
        var now = DateTime.UtcNow;
        var year = now.Year;
        await using var allocationTransaction = await BeginCodeAllocationAsync(year, ct);

        var project = new OperationalProject
        {
            Code = await NextCodeAsync(year, ct),
            Name = request.Name.Trim(),
            CustomerId = request.CustomerId,
            ProjectManagerUserId = request.ProjectManagerUserId ?? callerUserId,
            Status = OperationalProjectStatus.Planning,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Note = TrimOrNull(request.Note),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync(ct);
        await projectTeamSync.SyncOperationalProjectManagerAsync(
            project.Id, project.ProjectManagerUserId, callerUserId, ct);
        await db.SaveChangesAsync(ct);
        if (allocationTransaction is not null) await allocationTransaction.CommitAsync(ct);

        logger.LogInformation(
            "OperationalProject {ProjectId} ({ProjectCode}) created by user {UserId}",
            project.Id,
            project.Code,
            callerUserId);
        return (await GetAsync(project.Id, callerUserId, canSeeAll, ct))!;
    }

    public async Task<OperationalProjectResponse?> UpdateAsync(
        int id,
        UpdateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (project is null || !CanManage(project, callerUserId, canSeeAll)) return null;

        await ValidateAsync(request, callerUserId, canSeeAll, ct);
        if (project.CustomerId != request.CustomerId &&
            await HasDependenciesAsync(project.Id, ct))
        {
            throw new OperationalProjectOperationException(
                "Không thể đổi Khách hàng khi Dự án đã có dữ liệu nghiệp vụ.");
        }
        ValidateTransition(project.Status, request.Status);
        CrmConcurrency.Apply(db, project, request.RowVersion);

        project.Name = request.Name.Trim();
        project.CustomerId = request.CustomerId;
        project.ProjectManagerUserId = request.ProjectManagerUserId ?? callerUserId;
        project.Status = request.Status;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Note = TrimOrNull(request.Note);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedByUserId = callerUserId;

        await projectTeamSync.SyncOperationalProjectManagerAsync(
            project.Id, project.ProjectManagerUserId, callerUserId, ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetAsync(id, callerUserId, canSeeAll, ct);
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (project is null || !CanManage(project, callerUserId, canSeeAll)) return null;
        return await DeletionImpactPlanner.ForOperationalProjectAsync(db, id, ct);
    }

    public async Task<bool> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (project is null || !CanManage(project, callerUserId, canSeeAll)) return false;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var impact = await DeletionImpactPlanner.ForOperationalProjectAsync(db, id, ct);
        if (impact is null) return false;
        if (!impact.CanDelete)
            throw new OperationalProjectOperationException(
                "Không thể xoá Dự án khi còn tệp hoặc thư mục Drive. Vui lòng dọn các mục bị chặn trước.");
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException(
                "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!string.Equals(request.Confirmation?.Trim(), impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new OperationalProjectOperationException(
                $"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new CrmConcurrencyTokenException(
                "Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại Dự án trước khi xoá.");

        CrmConcurrency.Apply(db, project, request.RowVersion);
        try
        {
            await AggregateDeletionService.DeleteOperationalProjectAsync(
                db, project, projectDocuments, callerUserId, ct);
        }
        catch (AggregateDeletionBlockedException ex)
        {
            throw new OperationalProjectOperationException(ex.Message);
        }
        try
        {
            await CrmConcurrency.SaveChangesAsync(db, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new CrmConcurrencyException(
                "Dự án vừa phát sinh dữ liệu liên quan. Vui lòng tải lại danh sách ảnh hưởng.");
        }
        logger.LogInformation("OperationalProject {ProjectId} hard-deleted", id);
        return true;
    }

    private async Task<bool> HasDependenciesAsync(int projectId, CancellationToken ct) =>
        await db.Opportunities.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.Quotes.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.Contracts.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.ProjectDocuments.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.DesignProjects.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.OperationalProjectMembers.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.OperationalProjectAssignments.AnyAsync(item => item.OperationalProjectId == projectId, ct) ||
        await db.OperationalProjectTeamHistory.AnyAsync(item => item.OperationalProjectId == projectId, ct);

    private async Task ValidateAsync(
        CreateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new OperationalProjectOperationException("Tên Dự án là bắt buộc.");
        }
        if (request.StartDate.HasValue && request.EndDate.HasValue &&
            request.EndDate.Value.Date < request.StartDate.Value.Date)
        {
            throw new OperationalProjectOperationException(
                "Ngày kết thúc Dự án phải bằng hoặc sau ngày bắt đầu.");
        }
        if (!await db.Customers.AnyAsync(customer => customer.Id == request.CustomerId, ct))
        {
            throw new OperationalProjectOperationException(
                $"Khách hàng #{request.CustomerId} không tồn tại.");
        }

        var managerId = request.ProjectManagerUserId ?? callerUserId;
        if (!await db.Users.AnyAsync(user => user.Id == managerId && user.IsActive, ct))
        {
            throw new OperationalProjectOperationException(
                $"PM #{managerId} không tồn tại hoặc đã ngừng hoạt động.");
        }
        if (!canSeeAll && managerId != callerUserId)
        {
            throw new OperationalProjectOperationException(
                "Bạn không có quyền phân công Dự án cho người khác.");
        }
    }

    private static void ValidateTransition(
        OperationalProjectStatus current,
        OperationalProjectStatus requested)
    {
        if (current == requested) return;
        var allowed = current switch
        {
            OperationalProjectStatus.Planning => requested is OperationalProjectStatus.Active or
                OperationalProjectStatus.Cancelled,
            OperationalProjectStatus.Active => requested is OperationalProjectStatus.OnHold or
                OperationalProjectStatus.Completed or OperationalProjectStatus.Cancelled,
            OperationalProjectStatus.OnHold => requested is OperationalProjectStatus.Active or
                OperationalProjectStatus.Cancelled,
            _ => false,
        };
        if (!allowed)
        {
            throw new OperationalProjectOperationException(
                $"Không thể chuyển trạng thái Dự án từ {current} sang {requested}.");
        }
    }

    private async Task<string> NextCodeAsync(int year, CancellationToken ct)
    {
        var prefix = $"PJ-{year}-";
        var codes = await db.OperationalProjects
            .Where(project => project.Code.StartsWith(prefix))
            .Select(project => project.Code)
            .ToListAsync(ct);
        var next = codes
            .Select(code => code.Length > prefix.Length &&
                int.TryParse(code[prefix.Length..], out var sequence)
                    ? sequence
                    : 0)
            .DefaultIfEmpty()
            .Max() + 1;
        return $"{prefix}{next:D4}";
    }

    private async Task<IDbContextTransaction?> BeginCodeAllocationAsync(
        int year,
        CancellationToken ct)
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
            var resource = $"operational-project-code-{year}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000;
                IF @result < 0
                    THROW 51000, 'Unable to acquire the project code allocation lock.', 1;
                """, ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static bool CanManage(
        OperationalProject project,
        int callerUserId,
        bool canSeeAll) => canSeeAll ||
            project.ProjectManagerUserId == callerUserId ||
            project.CreatedByUserId == callerUserId;

    private static bool CanView(
        OperationalProject project,
        int callerUserId,
        bool canSeeAll) => CanManage(project, callerUserId, canSeeAll) ||
            project.TeamMembers.Any(member => member.UserId == callerUserId && member.EndedAt == null);

    private static OperationalProjectResponse Map(OperationalProject project) => new()
    {
        Id = project.Id,
        Code = project.Code,
        Name = project.Name,
        CustomerId = project.CustomerId,
        CustomerName = project.Customer.Name,
        ProjectManagerUserId = project.ProjectManagerUserId,
        ProjectManagerName = project.ProjectManager?.FullName,
        Status = project.Status.ToString(),
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        Note = project.Note,
        OpportunityCount = project.Opportunities.Count,
        QuoteCount = project.Quotes.Count,
        ContractCount = project.Contracts.Count,
        DesignProjectId = project.DesignProject?.Id,
        DesignProjectCode = project.DesignProject?.ProjectCode,
        RowVersion = CrmConcurrency.Encode(project.RowVersion),
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt,
        Opportunities = project.Opportunities
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new OperationalProjectOpportunityResponse
            {
                Id = item.Id,
                Name = item.Name,
                Stage = item.Stage.ToString(),
                EstimatedValue = item.EstimatedValue,
                WinProbability = item.WinProbability,
                ExpectedCloseDate = item.ExpectedCloseDate,
                CustomerName = item.Customer?.Name,
                OwnerName = item.Owner?.FullName,
                LostReasonCode = item.LostReasonCode,
            })
            .ToList(),
        Quotes = project.Quotes
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new OperationalProjectQuoteResponse
            {
                Id = item.Id,
                Code = item.Code,
                Status = item.Status.ToString(),
                Method = item.Method.ToString(),
                Version = item.Version,
                AreaSqm = item.AreaSqm,
                UnitPricePerSqm = item.UnitPricePerSqm,
                PackageDescription = item.PackageDescription,
                Subtotal = item.Subtotal,
                DiscountPercent = item.DiscountPercent,
                VatPercent = item.VatPercent,
                GrandTotal = item.GrandTotal,
                ValidUntil = item.ValidUntil,
                IsExpired = item.ValidUntil < DateTime.UtcNow && item.Status != QuoteStatus.CustomerApproved && item.Status != QuoteStatus.Cancelled,
                Note = item.Note,
                CustomerName = item.Opportunity?.Customer?.Name,
                OwnerName = item.Owner?.FullName,
                SubmittedAt = item.SubmittedAt,
                ApprovedAt = item.ApprovedAt,
                SentAt = item.SentAt,
                CreatedAt = item.CreatedAt,
                Items = item.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new OperationalProjectQuoteItemResponse
                    {
                        Id = i.Id,
                        ItemCode = i.ItemCode,
                        Name = i.Name,
                        Unit = i.Unit,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Amount = i.Amount,
                    })
                    .ToList(),
            })
            .ToList(),
        Contracts = project.Contracts
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new OperationalProjectContractResponse
            {
                Id = item.Id,
                ContractNumber = item.ContractNumber,
                Status = item.Status.ToString(),
                Value = item.Value,
                SignedDate = item.SignedDate,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                ScopeOfWork = item.ScopeOfWork,
                Note = item.Note,
                CustomerName = item.Customer?.Name,
                OwnerName = item.Owner?.FullName,
                CreatedAt = item.CreatedAt,
            })
            .ToList(),
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
