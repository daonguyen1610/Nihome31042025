using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        CrmConcurrency.Apply(db, lead, request.RowVersion);

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

        await CrmConcurrency.SaveChangesAsync(db, ct);

        if (newOwnerId.HasValue && newOwnerId != previousOwnerId)
        {
            await FireLeadAssignedAsync(lead, newOwnerId.Value, languageCode);
        }

        var ownerName = newOwnerId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == newOwnerId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return MapLead(lead, ownerName, activities: null);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default, string? rowVersion = null)
    {
        if (!canManage)
        {
            throw new LeadOperationException("Caller does not have permission to delete leads.");
        }

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return false;

        // Owner scoping — Sales users can only delete their own leads. Mirror
        // Get/List behaviour and return false rather than throwing so the
        // lead's existence is not leaked to unauthorised callers.
        if (!canSeeAll && lead.OwnerUserId != callerUserId) return false;

        CrmConcurrency.Apply(db, lead, rowVersion);

        db.Leads.Remove(lead);
        await CrmConcurrency.SaveChangesAsync(db, ct);
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

        var lead = await db.Leads
            .Include(l => l.Owner)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return null;

        if (lead.Status == LeadStatus.Converted)
        {
            throw new LeadOperationException("Lead is already converted.");
        }

        if (lead.Status == LeadStatus.Junk || lead.Status == LeadStatus.NotInterested)
        {
            throw new LeadOperationException("Discarded leads (Junk / NotInterested) cannot be converted.");
        }

        // One timestamp for all three rows. UnconvertAsync identifies the rows this
        // convert created by matching CreatedAt against the lead's ConvertedAt, so
        // do not replace this with several DateTime.UtcNow calls.
        var now = DateTime.UtcNow;

        // Convert writes two aggregates before stamping the lead, so it has to be
        // atomic. The in-memory provider used by unit tests rejects transactions,
        // hence the provider guard: SQL Server gets one, tests skip it.
        //
        // `await using` matters here: the validation below throws for a missing
        // opportunity, a customer mismatch, a duplicate customer or a company lead
        // without its required fields, and the controller turns those into 400 or
        // 409. Disposal rolls the transaction back on every one of those paths.
        await using IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // An existing opportunity already belongs to a customer, so creating a new
        // customer and attaching it to that opportunity is incoherent.
        int? customerIdFromOpportunity = null;
        if (request.OpportunityId.HasValue)
        {
            var existingOpportunity = await db.Opportunities.AsNoTracking()
                .Where(o => o.Id == request.OpportunityId.Value)
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync(ct);

            if (existingOpportunity is null)
            {
                throw new LeadOperationException($"Opportunity #{request.OpportunityId} not found.");
            }

            if (request.CustomerId.HasValue && request.CustomerId.Value != existingOpportunity.CustomerId)
            {
                throw new LeadOperationException(
                    "CustomerId must match the opportunity's customer, or be omitted.");
            }

            customerIdFromOpportunity = existingOpportunity.CustomerId;
        }

        var linkedCustomerId = request.CustomerId ?? customerIdFromOpportunity;

        Customer? createdCustomer = null;
        Opportunity? createdOpportunity = null;

        if (linkedCustomerId.HasValue)
        {
            var customerExists = await db.Customers
                .AnyAsync(c => c.Id == linkedCustomerId.Value, ct);
            if (!customerExists)
            {
                throw new LeadOperationException($"Customer #{linkedCustomerId} not found.");
            }
        }
        else
        {
            await EnsureNoDuplicateCustomerAsync(lead, request, ct);
            createdCustomer = BuildCustomerFromLead(lead, request, callerUserId, now);
            db.Customers.Add(createdCustomer);
        }

        if (!request.OpportunityId.HasValue)
        {
            createdOpportunity = new Opportunity
            {
                Name = string.IsNullOrWhiteSpace(lead.CompanyName)
                    ? $"Cơ hội từ lead {lead.Name}"
                    : $"Cơ hội từ lead {lead.CompanyName}",
                Stage = OpportunityStage.Prospecting,
                OwnerUserId = lead.OwnerUserId,
                EstimatedValue = 0m,
                WinProbability = 0,
                CreatedAt = now,
                CreatedByUserId = callerUserId,
                UpdatedAt = now,
                UpdatedByUserId = callerUserId,
            };

            // Assign through the navigation property when the customer is new too,
            // so EF inserts the customer first within the same SaveChanges.
            if (createdCustomer is not null)
            {
                createdOpportunity.Customer = createdCustomer;
            }
            else
            {
                createdOpportunity.CustomerId = linkedCustomerId!.Value;
            }

            db.Opportunities.Add(createdOpportunity);
        }

        // Save #1 — customer and opportunity. The lead is stamped afterwards, never
        // before, so a failure cannot leave it pointing at rows that never landed.
        if (createdCustomer is not null || createdOpportunity is not null)
        {
            await db.SaveChangesAsync(ct);
        }

        // Carried over from main: the care history recorded against the lead
        // belongs to the customer it became, otherwise it is stranded.
        if (createdCustomer is not null)
        {
            foreach (var leadActivity in lead.Activities)
            {
                db.CustomerActivities.Add(new CustomerActivity
                {
                    CustomerId = createdCustomer.Id,
                    Type = (CustomerActivityType)leadActivity.Type, // same enum values
                    Content = leadActivity.Content,
                    OccurredAt = leadActivity.CreatedAt,
                    CreatedByUserId = leadActivity.CreatedByUserId,
                    CreatedAt = now,
                });
            }
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedAt = now;
        lead.ConvertedCustomerId = createdCustomer?.Id ?? linkedCustomerId;
        lead.ConvertedOpportunityId = createdOpportunity?.Id ?? request.OpportunityId;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = callerUserId;

        var summary =
            $"[Convert] customerId={lead.ConvertedCustomerId}, " +
            $"opportunityId={lead.ConvertedOpportunityId}";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            summary += $" — {request.Note.Trim()}";
        }

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityType.Note,
            Content = summary,
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        });

        // Save #2 — the lead only.
        await CrmConcurrency.SaveChangesAsync(db, ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

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

    private const int UnconvertWindowHours = 24;

    public async Task<UnconvertLeadResponse?> UnconvertAsync(
        int id,
        int callerUserId,
        bool canConvert,
        CancellationToken ct = default,
        string? rowVersion = null)
    {
        if (!canConvert)
        {
            throw new LeadOperationException("Caller does not have permission to convert leads.");
        }

        var lead = await db.Leads.Include(l => l.Owner).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return null;

        if (lead.Status != LeadStatus.Converted || lead.ConvertedAt is null)
        {
            throw new LeadOperationException("Only a converted lead can be unconverted.");
        }

        var convertedAt = lead.ConvertedAt.Value;
        var now = DateTime.UtcNow;
        var withinWindow = (now - convertedAt).TotalHours < UnconvertWindowHours;

        var customer = lead.ConvertedCustomerId is null
            ? null
            : await db.Customers
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.Id == lead.ConvertedCustomerId, ct);

        var opportunity = lead.ConvertedOpportunityId is null
            ? null
            : await db.Opportunities
                .FirstOrDefaultAsync(o => o.Id == lead.ConvertedOpportunityId, ct);

        // Auto-created rows are recognised by a CreatedAt matching the convert
        // stamp exactly. ConvertAsync writes all three from one timestamp for
        // precisely this reason; a unit test pins that contract.
        var customerAutoCreated = customer is not null && customer.CreatedAt == convertedAt;
        var opportunityAutoCreated = opportunity is not null && opportunity.CreatedAt == convertedAt;

        var opportunityClean = opportunity is not null
            && withinWindow
            && opportunity.Stage == OpportunityStage.Prospecting
            && !await db.Quotes.AnyAsync(q => q.OpportunityId == opportunity.Id, ct)
            && !await db.Surveys.AnyAsync(sv => sv.LinkedOpportunityId == opportunity.Id, ct)
            && !await db.Contracts.AnyAsync(c => c.OpportunityId == opportunity.Id, ct)
            && !await db.Tenders.AnyAsync(tn => tn.WonOpportunityId == opportunity.Id, ct);

        // Customer activities and documents both cascade on delete, so removing the
        // customer would take them silently — and for documents the row goes while
        // the file on disk stays. One child record is enough to stop.
        var customerHasOtherWork = customer is not null
            && (await db.Opportunities.AnyAsync(
                    o => o.CustomerId == customer.Id && o.Id != lead.ConvertedOpportunityId, ct)
                || await db.Contracts.AnyAsync(c => c.CustomerId == customer.Id, ct)
                // Convert itself migrates the lead's activities onto the customer,
                // so those carry the convert stamp and must not count as work
                // somebody else did — otherwise nothing is ever deletable.
                || await db.CustomerActivities.AnyAsync(
                    a => a.CustomerId == customer.Id && a.CreatedAt != convertedAt, ct)
                || await db.CustomerDocuments.AnyAsync(d => d.CustomerId == customer.Id, ct));

        var outcome = UnconvertOutcome.UnlinkedOnly;

        if (opportunityAutoCreated && opportunityClean)
        {
            if (customerAutoCreated && !customerHasOtherWork)
            {
                db.Opportunities.Remove(opportunity!);
                db.CustomerContacts.RemoveRange(customer!.Contacts);
                db.Customers.Remove(customer);
                outcome = UnconvertOutcome.DeletedBoth;
            }
            else
            {
                db.Opportunities.Remove(opportunity!);
                outcome = UnconvertOutcome.DeletedOpportunity;
            }
        }

        var keptCustomerId = outcome == UnconvertOutcome.DeletedBoth ? null : lead.ConvertedCustomerId;
        var keptOpportunityId = outcome == UnconvertOutcome.UnlinkedOnly ? lead.ConvertedOpportunityId : null;

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityType.Note,
            Content = $"[Unconvert] outcome={outcome}",
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        });

        lead.Status = LeadStatus.Interested;
        lead.ConvertedAt = null;
        lead.ConvertedCustomerId = null;
        lead.ConvertedOpportunityId = null;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = callerUserId;

        CrmConcurrency.Apply(db, lead, rowVersion);
        await CrmConcurrency.SaveChangesAsync(db, ct);

        return new UnconvertLeadResponse
        {
            Outcome = outcome,
            KeptCustomerId = keptCustomerId,
            KeptOpportunityId = keptOpportunityId,
            Lead = MapLead(lead, lead.Owner?.FullName, activities: null),
        };
    }

    // ---------- helpers ----------

    /// <summary>
    /// Builds the customer a convert creates. A lead with a company name yields a
    /// Company customer, and <c>CustomerService.ValidateForType</c> requires an
    /// address and representative for those. Tax id is deliberately optional:
    /// a prospect may not have supplied one at conversion time (NIH-448).
    /// </summary>
    private static Customer BuildCustomerFromLead(
        Lead lead,
        ConvertLeadRequest request,
        int callerUserId,
        DateTime now)
    {
        var isCompany = !string.IsNullOrWhiteSpace(lead.CompanyName);

        if (isCompany &&
            (string.IsNullOrWhiteSpace(request.Address)
             || string.IsNullOrWhiteSpace(request.RepresentativeName)))
        {
            throw new LeadOperationException(
                "Company leads require Address and RepresentativeName to convert.");
        }

        return new Customer
        {
            Type = isCompany ? CustomerType.Company : CustomerType.Individual,
            Name = isCompany ? lead.CompanyName!.Trim() : lead.Name.Trim(),
            TaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            RepresentativeName = isCompany ? request.RepresentativeName!.Trim() : null,
            SourceCode = lead.SourceCode,
            RelationshipStatus = CustomerRelationshipStatus.Prospect,
            OwnerUserId = lead.OwnerUserId,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
            Contacts = new List<CustomerContact>
            {
                new()
                {
                    FullName = lead.Name.Trim(),
                    Phone = lead.Phone,
                    Email = lead.Email,
                    IsPrimary = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            },
        };
    }

    /// <summary>
    /// Mirrors <c>CustomerService.EnsureNoDuplicateAsync</c>: Company matches on
    /// TaxId, Individual on the primary contact phone. Throws the same
    /// <see cref="CustomerDuplicateException"/> so the controller can answer 409
    /// with the conflicting record and let the caller link to it instead.
    /// </summary>
    private async Task EnsureNoDuplicateCustomerAsync(
        Lead lead,
        ConvertLeadRequest request,
        CancellationToken ct)
    {
        var isCompany = !string.IsNullOrWhiteSpace(lead.CompanyName);
        Customer? conflict = null;
        var field = string.Empty;
        var value = string.Empty;

        if (isCompany && !string.IsNullOrWhiteSpace(request.TaxId))
        {
            var taxId = request.TaxId.Trim();
            conflict = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TaxId == taxId, ct);
            if (conflict is not null)
            {
                field = "TaxId";
                value = taxId;
            }
        }
        else if (!isCompany && !string.IsNullOrWhiteSpace(lead.Phone))
        {
            var phone = lead.Phone.Trim();
            conflict = await db.Customers.AsNoTracking()
                .Include(c => c.Contacts)
                .Where(c => c.Type == CustomerType.Individual)
                .FirstOrDefaultAsync(c => c.Contacts.Any(x => x.IsPrimary && x.Phone == phone), ct);
            if (conflict is not null)
            {
                field = "Phone";
                value = phone;
            }
        }

        if (conflict is null) return;

        throw new CustomerDuplicateException(new CustomerDuplicateResponse
        {
            Field = field,
            Value = value,
            ExistingCustomerId = conflict.Id,
            ExistingCustomerName = conflict.Name,
            Message =
                $"Khách hàng có {field} '{value}' đã tồn tại (#{conflict.Id} — {conflict.Name}). "
                + "Hãy chuyển đổi bằng cách gắn vào khách hàng này.",
        });
    }

    private static void ValidateContact(string? phone, string? email)
    {
        // Shared with customers, so a lead cannot carry a phone that its own
        // converted customer would later reject.
        var error = ContactValidation.Validate(phone, email);
        if (error is not null)
        {
            throw new LeadOperationException(error);
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
            RowVersion = CrmConcurrency.Encode(lead.RowVersion),
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
