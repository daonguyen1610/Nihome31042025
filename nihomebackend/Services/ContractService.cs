using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Config-lite CRUD for CRM Contract records (NIH-102 scope). Payment
/// milestones and variation orders belong to follow-up stories.
///
/// Sales users see only rows they own; roles with
/// <c>crm.contracts.view.all</c> (or the wildcard set) see everything.
/// </summary>
public class ContractService(
    AppDbContext db,
    IDesignProjectService designProjectService,
    ILogger<ContractService> logger,
    IWebHostEnvironment? env = null) : IContractService
{
    private const int MaxPageSize = 100;
    private readonly string _contentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();

    public async Task<ContractListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        ContractStatus? status = null,
        int? ownerUserId = null,
        int? customerId = null,
        string? search = null,
        DateTime? signedFrom = null,
        DateTime? signedTo = null,
        decimal? valueMin = null,
        decimal? valueMax = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = db.Contracts.AsNoTracking().AsQueryable();

        if (!canSeeAll)
        {
            query = query.Where(c => c.OwnerUserId == callerUserId);
        }
        else if (ownerUserId.HasValue)
        {
            query = query.Where(c => c.OwnerUserId == ownerUserId.Value);
        }

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (customerId.HasValue) query = query.Where(c => c.CustomerId == customerId.Value);
        if (signedFrom.HasValue) query = query.Where(c => c.SignedDate != null && c.SignedDate >= signedFrom.Value);
        if (signedTo.HasValue)
        {
            // Treat signedTo as end-of-day inclusive. Frontend supplies the
            // caller's chosen day at 00:00 UTC; a naive <= comparison would
            // drop every row signed later that same day.
            var upperBound = signedTo.Value.Date.AddDays(1);
            query = query.Where(c => c.SignedDate != null && c.SignedDate < upperBound);
        }
        if (valueMin.HasValue) query = query.Where(c => c.Value >= valueMin.Value);
        if (valueMax.HasValue) query = query.Where(c => c.Value <= valueMax.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.ContractNumber, like) ||
                EF.Functions.Like(c.Customer.Name, like));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(c => c.SignedDate ?? DateTime.MinValue)
            .ThenByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                Contract = c,
                CustomerName = c.Customer.Name,
                OpportunityTitle = c.Opportunity != null ? c.Opportunity.Name : null,
                QuoteCode = c.Quote != null ? c.Quote.Code : null,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                ApprovedVoTotal = db.ContractAppendices
                    .Where(v => v.ContractId == c.Id && v.Status == ContractAppendixStatus.Approved)
                    .Sum(v => (decimal?)v.ValueDelta) ?? 0m,
                HasSignedScan = db.ContractAttachments
                    .Any(a => a.ContractId == c.Id && a.Kind == ContractAttachmentKind.SignedScan),
                AttachmentCount = db.ContractAttachments.Count(a => a.ContractId == c.Id),
                AppendixCount = db.ContractAppendices.Count(v => v.ContractId == c.Id),
                DesignProject = db.DesignProjects
                    .Where(dp => dp.ContractId == c.Id)
                    .Select(dp => new { dp.Id, dp.ProjectCode, dp.Name, dp.CurrentStage })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return new ContractListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => MapToResponse(
                r.Contract, r.CustomerName, r.OpportunityTitle, r.QuoteCode, r.OwnerName,
                milestones: null,
                approvedVoTotal: r.ApprovedVoTotal,
                hasSignedScan: r.HasSignedScan,
                attachmentCount: r.AttachmentCount,
                appendixCount: r.AppendixCount,
                designProjectId: r.DesignProject?.Id,
                designProjectCode: r.DesignProject?.ProjectCode,
                designProjectName: r.DesignProject?.Name,
                designProjectStage: r.DesignProject?.CurrentStage)).ToList(),
        };
    }

    public async Task<ContractResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var row = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                Contract = c,
                CustomerName = c.Customer.Name,
                OpportunityTitle = c.Opportunity != null ? c.Opportunity.Name : null,
                QuoteCode = c.Quote != null ? c.Quote.Code : null,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                // Contract has no navigation to DesignProject — the foreign key
                // lives on the other side — so this reads as a correlated subquery,
                // the same shape the list projection already uses.
                DesignProject = db.DesignProjects
                    .Where(dp => dp.ContractId == c.Id)
                    .Select(dp => new { dp.Id, dp.ProjectCode, dp.Name, dp.CurrentStage })
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (row == null) return null;
        if (!canSeeAll && row.Contract.OwnerUserId != callerUserId) return null;

        var milestones = await db.ContractPaymentMilestones
            .AsNoTracking()
            .Where(m => m.ContractId == id)
            .OrderBy(m => m.Order)
            .ToListAsync(ct);

        var approvedVoTotal = await db.ContractAppendices
            .AsNoTracking()
            .Where(v => v.ContractId == id && v.Status == ContractAppendixStatus.Approved)
            .SumAsync(v => (decimal?)v.ValueDelta, ct) ?? 0m;

        var attachmentCount = await db.ContractAttachments
            .AsNoTracking()
            .CountAsync(a => a.ContractId == id, ct);

        var hasSignedScan = await db.ContractAttachments
            .AsNoTracking()
            .AnyAsync(a => a.ContractId == id && a.Kind == ContractAttachmentKind.SignedScan, ct);

        var appendixCount = await db.ContractAppendices
            .AsNoTracking()
            .CountAsync(v => v.ContractId == id, ct);

        return MapToResponse(
            row.Contract, row.CustomerName, row.OpportunityTitle, row.QuoteCode, row.OwnerName,
            milestones, approvedVoTotal, hasSignedScan, attachmentCount, appendixCount,
            designProjectId: row.DesignProject?.Id,
            designProjectCode: row.DesignProject?.ProjectCode,
            designProjectName: row.DesignProject?.Name,
            designProjectStage: row.DesignProject?.CurrentStage);
    }

    public async Task<ContractResponse> CreateAsync(UpsertContractRequest req, int callerUserId, bool canReassignOwner, CancellationToken ct = default)
    {
        if (req.PaymentMilestones is not null)
        {
            ValidatePaymentMilestones(req.PaymentMilestones);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var customerOwnerUserId = await ValidateReferencesAsync(req, ct);

        var number = string.IsNullOrWhiteSpace(req.ContractNumber)
            ? await GenerateNumberAsync(ct)
            : req.ContractNumber.Trim();

        if (await NumberExistsAsync(number, excludeId: null, ct))
        {
            throw new ContractDuplicateNumberException(number);
        }

        if (!canReassignOwner && customerOwnerUserId.HasValue && customerOwnerUserId.Value != callerUserId)
        {
            throw new ContractValidationException("Customer is outside the caller's ownership scope.");
        }
        var ownerUserId = canReassignOwner && req.OwnerUserId.HasValue
            ? req.OwnerUserId.Value
            : customerOwnerUserId ?? callerUserId;

        var entity = new Contract
        {
            ContractNumber = number,
            CustomerId = req.CustomerId,
            OpportunityId = req.OpportunityId,
            QuoteId = req.QuoteId,
            OwnerUserId = ownerUserId,
            Status = req.Status,
            SignedDate = req.SignedDate,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Value = req.Value,
            ScopeOfWork = string.IsNullOrWhiteSpace(req.ScopeOfWork) ? null : req.ScopeOfWork,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.Contracts.Add(entity);
        await db.SaveChangesAsync(ct);

        if (req.PaymentMilestones != null)
        {
            await ReplaceMilestonesAsync(entity.Id, req.PaymentMilestones, ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        logger.LogInformation("Created contract {Id} ({Number})", entity.Id, entity.ContractNumber);

        var read = await GetAsync(entity.Id, callerUserId, canSeeAll: true, ct);
        return read!;
    }

    public async Task<ContractResponse?> UpdateAsync(
        int id, UpsertContractRequest req, int callerUserId, bool canSeeAll, bool canReassignOwner, CancellationToken ct = default)
    {
        var entity = await db.Contracts.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;
        if (!canSeeAll && entity.OwnerUserId != callerUserId) return null;

        CrmConcurrency.Apply(db, entity, req.RowVersion);
        if (req.PaymentMilestones is not null)
        {
            ValidatePaymentMilestones(req.PaymentMilestones);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var customerOwnerUserId = await ValidateReferencesAsync(req, ct);

        var newNumber = string.IsNullOrWhiteSpace(req.ContractNumber)
            ? entity.ContractNumber
            : req.ContractNumber.Trim();
        if (!string.Equals(newNumber, entity.ContractNumber, StringComparison.Ordinal)
            && await NumberExistsAsync(newNumber, excludeId: id, ct))
        {
            throw new ContractDuplicateNumberException(newNumber);
        }

        entity.ContractNumber = newNumber;
        var customerChanged = entity.CustomerId != req.CustomerId;
        if (!canReassignOwner && customerChanged
            && customerOwnerUserId.HasValue
            && customerOwnerUserId.Value != callerUserId)
        {
            throw new ContractValidationException("Customer is outside the caller's ownership scope.");
        }
        entity.CustomerId = req.CustomerId;
        entity.OpportunityId = req.OpportunityId;
        entity.QuoteId = req.QuoteId;
        // Same safeguard as CreateAsync: only manager-tier callers can
        // reassign ownership. Sales users have their attempt silently
        // ignored so they cannot lose their own record via the API.
        if (canReassignOwner && req.OwnerUserId.HasValue)
        {
            entity.OwnerUserId = req.OwnerUserId.Value;
        }
        else if (canReassignOwner && customerChanged)
        {
            entity.OwnerUserId = customerOwnerUserId ?? callerUserId;
        }
        entity.Status = req.Status;
        entity.SignedDate = req.SignedDate;
        entity.StartDate = req.StartDate;
        entity.EndDate = req.EndDate;
        entity.Value = req.Value;
        entity.ScopeOfWork = string.IsNullOrWhiteSpace(req.ScopeOfWork) ? null : req.ScopeOfWork;
        entity.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;

        await CrmConcurrency.SaveChangesAsync(db, ct);

        // Milestone list is only replaced when the caller sent a value.
        // Null == leave alone, empty == wipe the schedule.
        if (req.PaymentMilestones != null)
        {
            await ReplaceMilestonesAsync(entity.Id, req.PaymentMilestones, ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        logger.LogInformation("Updated contract {Id} ({Number})", entity.Id, entity.ContractNumber);
        return await GetAsync(entity.Id, callerUserId, canSeeAll: true, ct);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default, string? rowVersion = null)
    {
        var entity = await db.Contracts.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;
        if (!canSeeAll && entity.OwnerUserId != callerUserId) return false;

        CrmConcurrency.Apply(db, entity, rowVersion);
        var managedFiles = await db.ContractAttachments
            .Where(attachment => attachment.ContractId == id)
            .Select(attachment => attachment.FilePath)
            .Concat(db.ContractAppendices
                .Where(appendix => appendix.ContractId == id && appendix.FilePath != null)
                .Select(appendix => appendix.FilePath!))
            .ToListAsync(ct);

        db.Contracts.Remove(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        foreach (var filePath in managedFiles) DeleteManagedFile(filePath);
        logger.LogInformation("Deleted contract {Id} ({Number})", entity.Id, entity.ContractNumber);
        return true;
    }

    private void DeleteManagedFile(string? filePath)
    {
        const string expectedPrefix = "/files/contracts/";
        if (string.IsNullOrWhiteSpace(filePath)
            || !filePath.StartsWith(expectedPrefix, StringComparison.Ordinal)) return;
        var fileName = Path.GetFileName(filePath);
        if (!string.Equals(filePath, $"{expectedPrefix}{fileName}", StringComparison.Ordinal)) return;
        var fullPath = Path.Combine(_contentRoot, "wwwroot", "files", "contracts", fileName);
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public async Task<ContractResponse?> TransitionStatusAsync(
        int id, ContractStatus newStatus, int callerUserId, bool canSeeAll, CancellationToken ct = default, string? rowVersion = null)
    {
        var entity = await db.Contracts.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;
        if (!canSeeAll && entity.OwnerUserId != callerUserId) return null;

        CrmConcurrency.Apply(db, entity, rowVersion);

        if (entity.Status == newStatus)
        {
            // No-op — return the current state so the caller doesn't need
            // to special-case a double click on the same action button.
            // Ownership was already validated above so re-read with the
            // manager-scope to avoid a redundant permission check.
            return await GetAsync(id, callerUserId, canSeeAll: true, ct);
        }

        EnsureTransitionAllowed(entity.Status, newStatus);
        await EnsureTransitionPreconditionsAsync(entity, newStatus, ct);

        // Signed → InProgress: stamp SignedDate if the caller forgot.
        if (newStatus == ContractStatus.Signed && entity.SignedDate is null)
        {
            entity.SignedDate = DateTime.UtcNow;
        }

        entity.Status = newStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        logger.LogInformation("Transitioned contract {Id} to {Status}", entity.Id, newStatus);

        // NIH-113 AC #1: opening execution of a contract seeds a design
        // project. The helper is idempotent so double-clicks or manual
        // pre-creation don't spawn duplicates.
        if (newStatus == ContractStatus.InProgress)
        {
            try
            {
                await designProjectService.EnsureForContractAsync(entity, callerUserId, ct);
            }
            catch (Exception ex)
            {
                // Auto-create is best-effort — a downstream failure must
                // not block the contract transition itself.
                logger.LogWarning(ex,
                    "Failed to auto-create DesignProject for contract {Id}", entity.Id);
            }
        }

        return await GetAsync(entity.Id, callerUserId, canSeeAll: true, ct);
    }

    public async Task<ContractResponse?> UpdateMilestoneStatusAsync(
        int contractId, int milestoneId, PaymentMilestoneStatus newStatus,
        int callerUserId, bool canSeeAll, CancellationToken ct = default, string? rowVersion = null)
    {
        var contract = await db.Contracts.FindAsync(new object?[] { contractId }, ct);
        if (contract == null) return null;
        if (!canSeeAll && contract.OwnerUserId != callerUserId) return null;

        CrmConcurrency.Apply(db, contract, rowVersion);

        var milestone = await db.ContractPaymentMilestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.ContractId == contractId, ct);
        if (milestone == null) return null;

        milestone.Status = newStatus;
        milestone.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        logger.LogInformation(
            "Milestone {Milestone} of contract {Contract} → {Status}",
            milestoneId, contractId, newStatus);

        return await GetAsync(contractId, callerUserId, canSeeAll: true, ct);
    }

    /// <summary>
    /// Contract state machine. Only these transitions are allowed:
    /// <list type="bullet">
    ///   <item>Draft → Signed | Cancelled</item>
    ///   <item>Signed → InProgress | Cancelled</item>
    ///   <item>InProgress → OnHold | Completed | Cancelled</item>
    ///   <item>OnHold → InProgress | Cancelled</item>
    /// </list>
    /// Completed and Cancelled are terminal states.
    /// </summary>
    private static void EnsureTransitionAllowed(ContractStatus from, ContractStatus to)
    {
        bool ok = (from, to) switch
        {
            (ContractStatus.Draft, ContractStatus.Signed) => true,
            (ContractStatus.Draft, ContractStatus.Cancelled) => true,
            (ContractStatus.Signed, ContractStatus.InProgress) => true,
            (ContractStatus.Signed, ContractStatus.Cancelled) => true,
            (ContractStatus.InProgress, ContractStatus.OnHold) => true,
            (ContractStatus.InProgress, ContractStatus.Completed) => true,
            (ContractStatus.InProgress, ContractStatus.Cancelled) => true,
            (ContractStatus.OnHold, ContractStatus.InProgress) => true,
            (ContractStatus.OnHold, ContractStatus.Cancelled) => true,
            _ => false,
        };
        if (!ok)
        {
            throw new ContractValidationException(
                $"Illegal contract transition {from} → {to}.");
        }
    }

    private async Task EnsureTransitionPreconditionsAsync(Contract entity, ContractStatus newStatus, CancellationToken ct)
    {
        switch (newStatus)
        {
            case ContractStatus.InProgress when entity.Status == ContractStatus.Signed:
                {
                    var hasScan = await db.ContractAttachments
                        .AsNoTracking()
                        .AnyAsync(a => a.ContractId == entity.Id && a.Kind == ContractAttachmentKind.SignedScan, ct);
                    if (!hasScan)
                    {
                        throw new ContractValidationException(
                            "Cần đính kèm bản scan hợp đồng đã ký trước khi chuyển sang Đang thực hiện.");
                    }
                    break;
                }

            case ContractStatus.Completed:
                {
                    var anyMilestone = await db.ContractPaymentMilestones
                        .AsNoTracking()
                        .AnyAsync(m => m.ContractId == entity.Id, ct);
                    if (!anyMilestone)
                    {
                        throw new ContractValidationException(
                            "Chưa có lịch thanh toán để hoàn thành hợp đồng.");
                    }
                    var anyUnpaid = await db.ContractPaymentMilestones
                        .AsNoTracking()
                        .AnyAsync(m => m.ContractId == entity.Id && m.Status != PaymentMilestoneStatus.Paid, ct);
                    if (anyUnpaid)
                    {
                        throw new ContractValidationException(
                            "Chỉ hoàn thành hợp đồng sau khi tất cả các đợt thanh toán đã Đã thanh toán.");
                    }
                    break;
                }
        }
    }

    // -------- helpers --------

    private async Task<int?> ValidateReferencesAsync(UpsertContractRequest req, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(customer => customer.Id == req.CustomerId)
            .Select(customer => new { customer.OwnerUserId })
            .SingleOrDefaultAsync(ct);
        if (customer is null)
        {
            throw new ContractValidationException($"Customer {req.CustomerId} does not exist.");
        }

        if (req.OpportunityId.HasValue)
        {
            var opportunityCustomerId = await db.Opportunities.AsNoTracking()
                .Where(opportunity => opportunity.Id == req.OpportunityId.Value)
                .Select(opportunity => (int?)opportunity.CustomerId)
                .SingleOrDefaultAsync(ct);
            if (!opportunityCustomerId.HasValue)
            {
                throw new ContractValidationException($"Opportunity {req.OpportunityId} does not exist.");
            }
            if (opportunityCustomerId.Value != req.CustomerId)
            {
                throw new ContractValidationException("Opportunity does not belong to the selected customer.");
            }
        }

        if (req.QuoteId.HasValue)
        {
            var quoteOpportunity = await db.Quotes.AsNoTracking()
                .Where(quote => quote.Id == req.QuoteId.Value)
                .Select(quote => new { quote.OpportunityId, quote.Opportunity.CustomerId })
                .SingleOrDefaultAsync(ct);
            if (quoteOpportunity is null)
            {
                throw new ContractValidationException($"Quote {req.QuoteId} does not exist.");
            }
            if (quoteOpportunity.CustomerId != req.CustomerId)
            {
                throw new ContractValidationException("Quote does not belong to the selected customer.");
            }
            if (req.OpportunityId.HasValue && quoteOpportunity.OpportunityId != req.OpportunityId.Value)
            {
                throw new ContractValidationException("Quote does not belong to the selected opportunity.");
            }
        }

        if (req.StartDate.HasValue && req.EndDate.HasValue && req.EndDate.Value < req.StartDate.Value)
        {
            throw new ContractValidationException("End date must be on or after start date.");
        }
        return customer.OwnerUserId;
    }

    private async Task<bool> NumberExistsAsync(string number, int? excludeId, CancellationToken ct) =>
        await db.Contracts.AsNoTracking()
            .AnyAsync(c => c.ContractNumber == number && (excludeId == null || c.Id != excludeId), ct);

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"HD-{year}-";

        // Numbers can also be typed by hand with any suffix, and a non-numeric one
        // sorts above the generated ones — "HD-2026-MUS" beats "HD-2026-0005" as a
        // string. Taking only the top row meant a single such contract made the
        // parse fail, reset the sequence to 1, and collide with whoever already
        // held 0001. So read every candidate and pick the highest that parses.
        var numbers = await db.Contracts
            .AsNoTracking()
            .Where(c => c.ContractNumber.StartsWith(prefix))
            .Select(c => c.ContractNumber)
            .ToListAsync(ct);

        var highestSeq = numbers
            .Select(number => number[prefix.Length..])
            .Select(tail => int.TryParse(tail, out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{highestSeq + 1:0000}";
    }

    private static void ValidatePaymentMilestones(List<ContractPaymentMilestoneRequest> milestones)
    {
        if (milestones.Count == 0) return;

        var orderSet = new HashSet<int>();
        foreach (var m in milestones)
        {
            if (!orderSet.Add(m.Order))
            {
                throw new ContractValidationException(
                    $"Duplicate payment milestone order '{m.Order}'.");
            }
        }

        var sum = milestones.Sum(m => m.PercentValue);
        // Guard against float-drift when the caller composed decimals from
        // JavaScript number arithmetic. Anything outside a 0.01 window is a
        // real business error.
        if (Math.Abs(sum - 100m) > 0.01m)
        {
            throw new ContractValidationException(
                $"Payment milestones must sum to 100% (got {sum}).");
        }
    }

    private async Task ReplaceMilestonesAsync(
        int contractId, List<ContractPaymentMilestoneRequest> milestones, CancellationToken ct)
    {
        // Drop existing rows and re-insert. The write set is expected to be
        // small (typically ≤ 8 milestones), so avoiding a diff-and-patch keeps
        // the code simpler than trying to preserve ids across a re-order.
        var existing = await db.ContractPaymentMilestones
            .Where(m => m.ContractId == contractId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            db.ContractPaymentMilestones.RemoveRange(existing);
        }

        // Normalise the input: strip empties and re-number Order to be
        // contiguous 1..N based on the caller's supplied ordering.
        var normalised = milestones
            .OrderBy(m => m.Order)
            .Select((m, idx) => new ContractPaymentMilestone
            {
                ContractId = contractId,
                Order = idx + 1,
                Name = m.Name.Trim(),
                PercentValue = m.PercentValue,
                DueDate = m.DueDate,
                Status = m.Status,
                Note = string.IsNullOrWhiteSpace(m.Note) ? null : m.Note.Trim(),
            })
            .ToList();
        if (normalised.Count > 0)
        {
            db.ContractPaymentMilestones.AddRange(normalised);
        }
        await db.SaveChangesAsync(ct);
    }

    private static ContractResponse MapToResponse(
        Contract entity,
        string? customerName,
        string? opportunityTitle,
        string? quoteCode,
        string? ownerName,
        IReadOnlyList<ContractPaymentMilestone>? milestones = null,
        decimal approvedVoTotal = 0m,
        bool hasSignedScan = false,
        int attachmentCount = 0,
        int appendixCount = 0,
        int? designProjectId = null,
        string? designProjectCode = null,
        string? designProjectName = null,
        DesignProjectStage? designProjectStage = null)
    {
        // CurrentValue = base value + approved VO deltas. Milestone Amount
        // still divides the base <c>entity.Value</c> — % refer to the
        // signed contract before amendments; VO settlements happen out of
        // band per the ops flow.
        var currentValue = entity.Value + approvedVoTotal;
        return new ContractResponse
        {
            Id = entity.Id,
            ContractNumber = entity.ContractNumber,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            OpportunityId = entity.OpportunityId,
            OpportunityTitle = opportunityTitle,
            QuoteId = entity.QuoteId,
            QuoteCode = quoteCode,
            DesignProjectId = designProjectId,
            DesignProjectCode = designProjectCode,
            DesignProjectName = designProjectName,
            // Converted here rather than in the query: the enum is stored via a
            // string conversion and ToString() does not translate to SQL.
            DesignProjectCurrentStage = designProjectStage?.ToString(),
            OwnerUserId = entity.OwnerUserId,
            OwnerName = ownerName,
            Status = entity.Status,
            SignedDate = entity.SignedDate,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Value = entity.Value,
            ApprovedVoTotal = approvedVoTotal,
            CurrentValue = currentValue,
            HasSignedScan = hasSignedScan,
            AttachmentCount = attachmentCount,
            AppendixCount = appendixCount,
            ScopeOfWork = entity.ScopeOfWork,
            Note = entity.Note,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            RowVersion = CrmConcurrency.Encode(entity.RowVersion),
            PaymentMilestones = (milestones ?? Array.Empty<ContractPaymentMilestone>())
                .OrderBy(m => m.Order)
                .Select(m => new ContractPaymentMilestoneResponse
                {
                    Id = m.Id,
                    Order = m.Order,
                    Name = m.Name,
                    PercentValue = m.PercentValue,
                    Amount = Math.Round(entity.Value * m.PercentValue / 100m, 2),
                    DueDate = m.DueDate,
                    Status = m.Status,
                    Note = m.Note,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                })
                .ToList(),
        };
    }
}
