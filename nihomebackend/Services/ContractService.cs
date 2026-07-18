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
public class ContractService(AppDbContext db, ILogger<ContractService> logger) : IContractService
{
    private const int MaxPageSize = 100;

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
            })
            .ToListAsync(ct);

        return new ContractListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => MapToResponse(
                r.Contract, r.CustomerName, r.OpportunityTitle, r.QuoteCode, r.OwnerName)).ToList(),
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
            })
            .FirstOrDefaultAsync(ct);

        if (row == null) return null;
        if (!canSeeAll && row.Contract.OwnerUserId != callerUserId) return null;

        return MapToResponse(row.Contract, row.CustomerName, row.OpportunityTitle, row.QuoteCode, row.OwnerName);
    }

    public async Task<ContractResponse> CreateAsync(UpsertContractRequest req, int callerUserId, bool canReassignOwner, CancellationToken ct = default)
    {
        await ValidateReferencesAsync(req, ct);

        var number = string.IsNullOrWhiteSpace(req.ContractNumber)
            ? await GenerateNumberAsync(ct)
            : req.ContractNumber.Trim();

        if (await NumberExistsAsync(number, excludeId: null, ct))
        {
            throw new ContractDuplicateNumberException(number);
        }

        // Sales users without view.all cannot reassign a new record to a
        // different owner: force ownership to the caller so the row stays
        // visible to them under the ownership scope.
        var ownerUserId = canReassignOwner ? (req.OwnerUserId ?? callerUserId) : callerUserId;

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

        await ValidateReferencesAsync(req, ct);

        var newNumber = string.IsNullOrWhiteSpace(req.ContractNumber)
            ? entity.ContractNumber
            : req.ContractNumber.Trim();
        if (!string.Equals(newNumber, entity.ContractNumber, StringComparison.Ordinal)
            && await NumberExistsAsync(newNumber, excludeId: id, ct))
        {
            throw new ContractDuplicateNumberException(newNumber);
        }

        entity.ContractNumber = newNumber;
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
        entity.Status = req.Status;
        entity.SignedDate = req.SignedDate;
        entity.StartDate = req.StartDate;
        entity.EndDate = req.EndDate;
        entity.Value = req.Value;
        entity.ScopeOfWork = string.IsNullOrWhiteSpace(req.ScopeOfWork) ? null : req.ScopeOfWork;
        entity.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated contract {Id} ({Number})", entity.Id, entity.ContractNumber);
        return await GetAsync(entity.Id, callerUserId, canSeeAll: true, ct);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var entity = await db.Contracts.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;
        if (!canSeeAll && entity.OwnerUserId != callerUserId) return false;

        db.Contracts.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted contract {Id} ({Number})", entity.Id, entity.ContractNumber);
        return true;
    }

    // -------- helpers --------

    private async Task ValidateReferencesAsync(UpsertContractRequest req, CancellationToken ct)
    {
        var customerExists = await db.Customers.AsNoTracking().AnyAsync(c => c.Id == req.CustomerId, ct);
        if (!customerExists)
        {
            throw new ContractValidationException($"Customer {req.CustomerId} does not exist.");
        }

        if (req.OpportunityId.HasValue)
        {
            var oppExists = await db.Opportunities.AsNoTracking().AnyAsync(o => o.Id == req.OpportunityId.Value, ct);
            if (!oppExists)
            {
                throw new ContractValidationException($"Opportunity {req.OpportunityId} does not exist.");
            }
        }

        if (req.QuoteId.HasValue)
        {
            var quoteExists = await db.Quotes.AsNoTracking().AnyAsync(q => q.Id == req.QuoteId.Value, ct);
            if (!quoteExists)
            {
                throw new ContractValidationException($"Quote {req.QuoteId} does not exist.");
            }
        }

        if (req.StartDate.HasValue && req.EndDate.HasValue && req.EndDate.Value < req.StartDate.Value)
        {
            throw new ContractValidationException("End date must be on or after start date.");
        }
    }

    private async Task<bool> NumberExistsAsync(string number, int? excludeId, CancellationToken ct) =>
        await db.Contracts.AsNoTracking()
            .AnyAsync(c => c.ContractNumber == number && (excludeId == null || c.Id != excludeId), ct);

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"HD-{year}-";
        var lastNumber = await db.Contracts
            .AsNoTracking()
            .Where(c => c.ContractNumber.StartsWith(prefix))
            .OrderByDescending(c => c.ContractNumber)
            .Select(c => c.ContractNumber)
            .FirstOrDefaultAsync(ct);
        var nextSeq = 1;
        if (lastNumber != null)
        {
            var tail = lastNumber.Substring(prefix.Length);
            if (int.TryParse(tail, out var seq))
            {
                nextSeq = seq + 1;
            }
        }
        return $"{prefix}{nextSeq:0000}";
    }

    private static ContractResponse MapToResponse(
        Contract entity,
        string? customerName,
        string? opportunityTitle,
        string? quoteCode,
        string? ownerName) => new()
        {
            Id = entity.Id,
            ContractNumber = entity.ContractNumber,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            OpportunityId = entity.OpportunityId,
            OpportunityTitle = opportunityTitle,
            QuoteId = entity.QuoteId,
            QuoteCode = quoteCode,
            OwnerUserId = entity.OwnerUserId,
            OwnerName = ownerName,
            Status = entity.Status,
            SignedDate = entity.SignedDate,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Value = entity.Value,
            ScopeOfWork = entity.ScopeOfWork,
            Note = entity.Note,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
}
