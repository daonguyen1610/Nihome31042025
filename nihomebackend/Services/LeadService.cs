using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class LeadService(
    AppDbContext db,
    IPermissionService permissions,
    INotificationService notifications,
    ILogger<LeadService> logger) : ILeadService
{
    private const int MaxPageSize = 100;
    private const string LeadAssignedTemplate = "lead.assigned";
    private const string SourceMasterDataCategory = "customer_source";
    private const string StatusMasterDataCategory = "lead_status";
    private const string ManageLeadsPermission = "crm.leads.manage";

    public async Task<LeadListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        LeadStatus? status = null,
        string? sourceCode = null,
        int? ownerUserId = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = db.Leads.AsNoTracking().AsQueryable();

        if (!canSeeAll)
        {
            // Sales sees only leads assigned to themselves — DoD "Sales chỉ thấy lead của mình".
            query = query.Where(l => l.OwnerUserId == callerUserId);
        }
        else if (ownerUserId.HasValue)
        {
            query = query.Where(l => l.OwnerUserId == ownerUserId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(sourceCode))
        {
            var normalized = sourceCode.Trim();
            query = query.Where(l => l.SourceCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term}%";
            query = query.Where(l =>
                EF.Functions.Like(l.Name, like) ||
                (l.CompanyName != null && EF.Functions.Like(l.CompanyName, like)) ||
                (l.Phone != null && EF.Functions.Like(l.Phone, like)) ||
                (l.Email != null && EF.Functions.Like(l.Email, like)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                Lead = l,
                OwnerName = l.Owner != null ? l.Owner.FullName : null,
            })
            .ToListAsync(ct);

        return new LeadListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => MapLead(r.Lead, r.OwnerName, activities: null)).ToList(),
        };
    }

    public async Task<LeadResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var lead = await db.Leads
            .AsNoTracking()
            .Include(l => l.Owner)
            .Include(l => l.Activities)
                .ThenInclude(a => a.CreatedBy)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lead is null) return null;

        if (!canSeeAll && lead.OwnerUserId != callerUserId)
        {
            return null; // hide existence from other Sales users
        }

        return MapLead(lead, lead.Owner?.FullName, lead.Activities);
    }

    public async Task<LeadResponse> CreateAsync(
        CreateLeadRequest request,
        int callerUserId,
        bool canManage,
        string languageCode = "vi",
        CancellationToken ct = default)
    {
        if (!canManage)
        {
            throw new LeadOperationException("Caller does not have permission to create leads.");
        }

        ValidateContact(request.Phone, request.Email);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);

        int? ownerId = request.OwnerUserId;
        if (ownerId is null)
        {
            // Two-tier fallback:
            //   1. Sales/manager creating manually → assign to themselves so
            //      they immediately see the lead in their own list.
            //   2. System / marketing import path (caller lacks the
            //      management permission) → distribute round-robin across
            //      the pool of users who DO have it.
            if (await permissions.HasAsync(callerUserId, ManageLeadsPermission, ct))
            {
                ownerId = callerUserId;
            }
            else
            {
                ownerId = await PickOwnerViaRoundRobinAsync(ct);
            }
        }
        else
        {
            await EnsureOwnerCanManageLeadsAsync(ownerId.Value, ct);
        }

        var now = DateTime.UtcNow;
        var lead = new Lead
        {
            Name = request.Name.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            SourceCode = sourceCode,
            Status = LeadStatus.New,
            OwnerUserId = ownerId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        if (ownerId.HasValue)
        {
            await FireLeadAssignedAsync(lead, ownerId.Value, languageCode);
        }

        var owner = ownerId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == ownerId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return MapLead(lead, owner, activities: null);
    }

    public async Task<LeadResponse?> UpdateAsync(
        int id,
        UpdateLeadRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        string languageCode = "vi",
        CancellationToken ct = default)
    {
        var lead = await db.Leads.Include(l => l.Owner).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return null;

        // Sales users can only update leads they own.
        if (!canSeeAll && lead.OwnerUserId != callerUserId)
        {
            return null;
        }

        if (!canManage)
        {
            throw new LeadOperationException("Caller does not have permission to modify leads.");
        }

        if (lead.Status == LeadStatus.Converted)
        {
            throw new LeadOperationException("Converted leads cannot be edited.");
        }

        // Only Sales Manager (canSeeAll proxy) may transition to NotInterested or Junk —
        // DoD says these are irreversible dead-end statuses.
        if ((request.Status == LeadStatus.NotInterested || request.Status == LeadStatus.Junk) &&
            lead.Status != request.Status &&
            !canSeeAll)
        {
            throw new LeadOperationException("Only Sales Manager can move a lead to NotInterested or Junk.");
        }

        // /convert is the only entry point to LeadStatus.Converted — reject direct writes.
        if (request.Status == LeadStatus.Converted && lead.Status != LeadStatus.Converted)
        {
            throw new LeadOperationException("Use POST /api/leads/{id}/convert to mark a lead as converted.");
        }

        ValidateContact(request.Phone, request.Email);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);

        var previousOwnerId = lead.OwnerUserId;
        int? newOwnerId = request.OwnerUserId;

        // Sales user cannot re-assign the lead to somebody else; managers can.
        if (!canSeeAll && newOwnerId != previousOwnerId && newOwnerId != callerUserId)
        {
            throw new LeadOperationException("Only Sales Manager can reassign a lead.");
        }

        if (newOwnerId.HasValue && newOwnerId != previousOwnerId)
        {
            await EnsureOwnerCanManageLeadsAsync(newOwnerId.Value, ct);
        }

        lead.Name = request.Name.Trim();
        lead.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
        lead.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        lead.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        lead.SourceCode = sourceCode;
        lead.Status = request.Status;
        lead.OwnerUserId = newOwnerId;
        lead.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        if (newOwnerId.HasValue && newOwnerId != previousOwnerId)
        {
            await FireLeadAssignedAsync(lead, newOwnerId.Value, languageCode);
        }

        var ownerName = newOwnerId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == newOwnerId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return MapLead(lead, ownerName, activities: null);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, CancellationToken ct = default)
    {
        if (!canManage)
        {
            throw new LeadOperationException("Caller does not have permission to delete leads.");
        }

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return false;

        if (lead.Status == LeadStatus.Converted)
        {
            throw new LeadOperationException("Converted leads cannot be deleted — they seed downstream customer records.");
        }

        db.Leads.Remove(lead);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LeadResponse?> ConvertAsync(
        int id,
        ConvertLeadRequest request,
        int callerUserId,
        bool canConvert,
        CancellationToken ct = default)
    {
        if (!canConvert)
        {
            throw new LeadOperationException("Caller does not have permission to convert leads.");
        }

        var lead = await db.Leads.Include(l => l.Owner).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return null;

        if (lead.Status == LeadStatus.Converted)
        {
            throw new LeadOperationException("Lead is already converted.");
        }

        if (lead.Status == LeadStatus.Junk || lead.Status == LeadStatus.NotInterested)
        {
            throw new LeadOperationException("Discarded leads (Junk / NotInterested) cannot be converted.");
        }

        var now = DateTime.UtcNow;
        lead.Status = LeadStatus.Converted;
        lead.ConvertedAt = now;
        lead.ConvertedCustomerId = request.CustomerId;
        lead.ConvertedOpportunityId = request.OpportunityId;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = callerUserId;

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityType.Note,
                Content = $"[Convert] {request.Note.Trim()}",
                CreatedByUserId = callerUserId,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);

        return MapLead(lead, lead.Owner?.FullName, activities: null);
    }

    public async Task<LeadActivityResponse?> AddActivityAsync(
        int leadId,
        CreateLeadActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return null;

        if (!canSeeAll && lead.OwnerUserId != callerUserId)
        {
            return null;
        }

        var activity = new LeadActivity
        {
            LeadId = leadId,
            Type = request.Type,
            Content = request.Content.Trim(),
            CreatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.LeadActivities.Add(activity);
        await db.SaveChangesAsync(ct);

        var creator = await db.Users.AsNoTracking()
            .Where(u => u.Id == callerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        return new LeadActivityResponse
        {
            Id = activity.Id,
            Type = activity.Type,
            Content = activity.Content,
            CreatedByUserId = activity.CreatedByUserId,
            CreatedByName = creator,
            CreatedAt = activity.CreatedAt,
        };
    }

    // ---------- helpers ----------

    private static void ValidateContact(string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            throw new LeadOperationException("A lead must have at least one of Phone or Email.");
        }
    }

    private async Task<string> ValidateSourceCodeAsync(string sourceCode, CancellationToken ct)
    {
        var normalized = (sourceCode ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            throw new LeadOperationException("SourceCode is required.");
        }

        var exists = await db.MasterDataOptions
            .AsNoTracking()
            .AnyAsync(o => o.Category == SourceMasterDataCategory && o.Code == normalized && o.IsActive, ct);

        if (!exists)
        {
            throw new LeadOperationException($"SourceCode '{normalized}' is not an active option in master data '{SourceMasterDataCategory}'.");
        }

        return normalized;
    }

    private async Task<int?> PickOwnerViaRoundRobinAsync(CancellationToken ct)
    {
        // Candidates = users whose effective permission set includes crm.leads.manage
        // AND who are active. Load user ids + assigned-lead count (open leads only),
        // then pick the one with the smallest workload; tie-break by user id for
        // deterministic behavior.
        var activeUsers = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var eligible = new List<int>();
        foreach (var uid in activeUsers)
        {
            if (await permissions.HasAsync(uid, ManageLeadsPermission, ct))
            {
                eligible.Add(uid);
            }
        }

        if (eligible.Count == 0)
        {
            logger.LogWarning("Lead round-robin fallback: no active user has {Perm}; lead will be created unassigned.", ManageLeadsPermission);
            return null;
        }

        var workloads = await db.Leads
            .AsNoTracking()
            .Where(l => l.OwnerUserId != null && eligible.Contains(l.OwnerUserId.Value) && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Junk)
            .GroupBy(l => l.OwnerUserId!.Value)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count, ct);

        return eligible
            .OrderBy(uid => workloads.TryGetValue(uid, out var c) ? c : 0)
            .ThenBy(uid => uid)
            .First();
    }

    private async Task EnsureOwnerCanManageLeadsAsync(int ownerUserId, CancellationToken ct)
    {
        var userActive = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(ct);

        if (userActive is null || userActive == false)
        {
            throw new LeadOperationException($"Owner user #{ownerUserId} does not exist or is inactive.");
        }

        if (!await permissions.HasAsync(ownerUserId, ManageLeadsPermission, ct))
        {
            throw new LeadOperationException($"Owner user #{ownerUserId} does not have permission '{ManageLeadsPermission}'.");
        }
    }

    private async Task FireLeadAssignedAsync(Lead lead, int ownerUserId, string languageCode)
    {
        try
        {
            var sourceName = await db.MasterDataOptions
                .AsNoTracking()
                .Where(o => o.Category == SourceMasterDataCategory && o.Code == lead.SourceCode)
                .Select(o => o.Name)
                .FirstOrDefaultAsync();

            await notifications.NotifyFromTemplateAsync(
                ownerUserId,
                LeadAssignedTemplate,
                new Dictionary<string, string>
                {
                    ["leadName"] = string.IsNullOrWhiteSpace(lead.CompanyName)
                        ? lead.Name
                        : $"{lead.Name} ({lead.CompanyName})",
                    ["leadSource"] = sourceName ?? lead.SourceCode,
                },
                refEntityType: EntityTypes.Lead,
                refEntityId: lead.Id,
                linkUrl: $"/admin/leads/{lead.Id}",
                languageCode: languageCode);
        }
        catch (Exception ex)
        {
            // Notification failure must NOT block the lead operation itself.
            logger.LogWarning(ex, "Lead {LeadId} assigned to user {OwnerId} but notification dispatch failed.", lead.Id, ownerUserId);
        }
    }

    private static LeadResponse MapLead(Lead lead, string? ownerName, IEnumerable<LeadActivity>? activities)
    {
        return new LeadResponse
        {
            Id = lead.Id,
            Name = lead.Name,
            CompanyName = lead.CompanyName,
            Phone = lead.Phone,
            Email = lead.Email,
            SourceCode = lead.SourceCode,
            Status = lead.Status,
            OwnerUserId = lead.OwnerUserId,
            OwnerName = ownerName,
            Note = lead.Note,
            ConvertedAt = lead.ConvertedAt,
            ConvertedCustomerId = lead.ConvertedCustomerId,
            ConvertedOpportunityId = lead.ConvertedOpportunityId,
            CreatedAt = lead.CreatedAt,
            UpdatedAt = lead.UpdatedAt,
            Activities = activities is null
                ? new List<LeadActivityResponse>()
                : activities
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new LeadActivityResponse
                    {
                        Id = a.Id,
                        Type = a.Type,
                        Content = a.Content,
                        CreatedByUserId = a.CreatedByUserId,
                        CreatedByName = a.CreatedBy?.FullName,
                        CreatedAt = a.CreatedAt,
                    })
                    .ToList(),
        };
    }
}
