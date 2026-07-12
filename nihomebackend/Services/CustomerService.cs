using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class CustomerService(
    AppDbContext db,
    ILogger<CustomerService> logger) : ICustomerService
{
    private const int MaxPageSize = 100;
    private const string SourceCategory = "customer_source";

    public async Task<CustomerListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        CustomerType? type = null,
        CustomerRelationshipStatus? status = null,
        int? ownerUserId = null,
        string? sourceCode = null,
        string? search = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = db.Customers.AsNoTracking().AsQueryable();

        if (!canSeeAll)
        {
            query = query.Where(c => c.OwnerUserId == callerUserId);
        }
        else if (ownerUserId.HasValue)
        {
            query = query.Where(c => c.OwnerUserId == ownerUserId.Value);
        }

        if (type.HasValue) query = query.Where(c => c.Type == type.Value);
        if (status.HasValue) query = query.Where(c => c.RelationshipStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(sourceCode))
        {
            var normalized = sourceCode.Trim();
            query = query.Where(c => c.SourceCode == normalized);
        }

        if (createdFrom.HasValue) query = query.Where(c => c.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue) query = query.Where(c => c.CreatedAt <= createdTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Name, like) ||
                (c.TaxId != null && EF.Functions.Like(c.TaxId, like)) ||
                c.Contacts.Any(ct2 =>
                    (ct2.Phone != null && EF.Functions.Like(ct2.Phone, like)) ||
                    (ct2.Email != null && EF.Functions.Like(ct2.Email, like))));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                Customer = c,
                OwnerName = c.Owner != null ? c.Owner.FullName : null,
                PrimaryContact = c.Contacts.Where(ct2 => ct2.IsPrimary).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return new CustomerListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r =>
            {
                var mapped = MapCustomer(r.Customer, r.OwnerName, contacts: null, activities: null);
                if (r.PrimaryContact != null)
                {
                    // List view surfaces just the primary contact so callers
                    // can render "Nguyen Van A · 0900…" without an extra fetch.
                    mapped.Contacts = new List<CustomerContactResponse> { MapContact(r.PrimaryContact) };
                }
                return mapped;
            }).ToList(),
        };
    }

    public async Task<CustomerResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Contacts)
            .Include(c => c.Activities)
                .ThenInclude(a => a.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null) return null;

        if (!canSeeAll && customer.OwnerUserId != callerUserId)
        {
            return null; // hide existence from other Sales
        }

        return MapCustomer(customer, customer.Owner?.FullName, customer.Contacts, customer.Activities);
    }

    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default)
    {
        if (!canManage)
        {
            throw new CustomerOperationException("Caller does not have permission to create customers.");
        }

        ValidateForType(request.Type, request.TaxId, request.Address, request.RepresentativeName);
        ValidateContactContact(request.PrimaryContact.Phone, request.PrimaryContact.Email);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);

        // Duplicate detection — TaxId (Company) or primary Phone (Individual).
        await EnsureNoDuplicateAsync(
            request.Type,
            request.TaxId,
            request.PrimaryContact.Phone,
            existingCustomerId: null,
            request.DuplicateOverrideReason,
            callerUserId,
            ct);

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Type = request.Type,
            Name = request.Name.Trim(),
            TaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            RepresentativeName = string.IsNullOrWhiteSpace(request.RepresentativeName) ? null : request.RepresentativeName.Trim(),
            SourceCode = sourceCode,
            RelationshipStatus = CustomerRelationshipStatus.Prospect,
            OwnerUserId = request.OwnerUserId ?? callerUserId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
            Contacts = new List<CustomerContact>
            {
                new()
                {
                    FullName = request.PrimaryContact.FullName.Trim(),
                    Position = TrimOrNull(request.PrimaryContact.Position),
                    Phone = TrimOrNull(request.PrimaryContact.Phone),
                    Email = TrimOrNull(request.PrimaryContact.Email),
                    IsPrimary = true, // forced true — Create requires one primary contact
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            },
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return MapCustomer(customer, ownerName: null, customer.Contacts, activities: null);
    }

    public async Task<CustomerResponse?> UpdateAsync(
        int id,
        UpdateCustomerRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var customer = await db.Customers
            .Include(c => c.Owner)
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null) return null;
        if (!canSeeAll && customer.OwnerUserId != callerUserId) return null;
        if (!canManage) throw new CustomerOperationException("Caller does not have permission to modify customers.");

        // Suspended is a dead-end status — Sales cannot suspend, only manager/BOD.
        if (request.RelationshipStatus == CustomerRelationshipStatus.Suspended &&
            customer.RelationshipStatus != CustomerRelationshipStatus.Suspended &&
            !canSeeAll)
        {
            throw new CustomerOperationException("Only Sales Manager / BOD can suspend a customer.");
        }

        // Sales users cannot re-assign the customer to somebody else.
        if (!canSeeAll && request.OwnerUserId != customer.OwnerUserId && request.OwnerUserId != callerUserId)
        {
            throw new CustomerOperationException("Only Sales Manager can reassign a customer.");
        }

        ValidateForType(request.Type, request.TaxId, request.Address, request.RepresentativeName);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);

        var primaryPhone = customer.Contacts.FirstOrDefault(c => c.IsPrimary)?.Phone;
        await EnsureNoDuplicateAsync(
            request.Type,
            request.TaxId,
            primaryPhone,
            existingCustomerId: customer.Id,
            request.DuplicateOverrideReason,
            callerUserId,
            ct);

        customer.Type = request.Type;
        customer.Name = request.Name.Trim();
        customer.TaxId = TrimOrNull(request.TaxId);
        customer.Address = TrimOrNull(request.Address);
        customer.RepresentativeName = TrimOrNull(request.RepresentativeName);
        customer.SourceCode = sourceCode;
        customer.RelationshipStatus = request.RelationshipStatus;
        customer.OwnerUserId = request.OwnerUserId;
        customer.Note = TrimOrNull(request.Note);
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        var ownerName = customer.OwnerUserId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == customer.OwnerUserId.Value)
                .Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return MapCustomer(customer, ownerName, customer.Contacts, activities: null);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, CancellationToken ct = default)
    {
        if (!canManage) throw new CustomerOperationException("Caller does not have permission to delete customers.");

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return false;

        // Delete guard: downstream FKs. Opportunity + Contract entities land
        // in later stories; until they exist there's nothing to check here.
        // When they do, this becomes:
        //   var openOpps = await db.Opportunities.CountAsync(o => o.CustomerId == id && o.Status != Closed, ct);
        //   if (openOpps > 0) throw new CustomerOperationException($"…");

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CustomerContactResponse?> UpsertContactAsync(
        int customerId,
        UpsertCustomerContactRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var customer = await db.Customers
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return null;
        if (!canSeeAll && customer.OwnerUserId != callerUserId) return null;
        if (!canManage) throw new CustomerOperationException("Caller does not have permission to modify contacts.");

        ValidateContactContact(request.Phone, request.Email);

        var now = DateTime.UtcNow;
        CustomerContact contact;
        if (request.Id is int id)
        {
            contact = customer.Contacts.FirstOrDefault(c => c.Id == id)
                ?? throw new CustomerOperationException("Contact not found on this customer.");
            contact.FullName = request.FullName.Trim();
            contact.Position = TrimOrNull(request.Position);
            contact.Phone = TrimOrNull(request.Phone);
            contact.Email = TrimOrNull(request.Email);
            contact.UpdatedAt = now;
        }
        else
        {
            contact = new CustomerContact
            {
                CustomerId = customerId,
                FullName = request.FullName.Trim(),
                Position = TrimOrNull(request.Position),
                Phone = TrimOrNull(request.Phone),
                Email = TrimOrNull(request.Email),
                CreatedAt = now,
                UpdatedAt = now,
            };
            customer.Contacts.Add(contact);
        }

        if (request.IsPrimary)
        {
            // Exactly one primary. Demote every other contact first.
            foreach (var other in customer.Contacts.Where(c => c != contact))
            {
                if (other.IsPrimary)
                {
                    other.IsPrimary = false;
                    other.UpdatedAt = now;
                }
            }
            contact.IsPrimary = true;
        }
        else if (!customer.Contacts.Any(c => c != contact && c.IsPrimary))
        {
            // The caller is trying to unset the only primary — keep this
            // contact primary so the invariant holds.
            contact.IsPrimary = true;
        }

        customer.UpdatedAt = now;
        customer.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        return MapContact(contact);
    }

    public async Task<bool> DeleteContactAsync(
        int customerId,
        int contactId,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var customer = await db.Customers
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return false;
        if (!canSeeAll && customer.OwnerUserId != callerUserId) return false;
        if (!canManage) throw new CustomerOperationException("Caller does not have permission to modify contacts.");

        var contact = customer.Contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact is null) return false;

        if (customer.Contacts.Count == 1)
        {
            throw new CustomerOperationException("A customer must have at least one contact — delete the customer instead.");
        }

        var wasPrimary = contact.IsPrimary;
        customer.Contacts.Remove(contact);
        db.CustomerContacts.Remove(contact);

        if (wasPrimary)
        {
            // Promote the oldest remaining contact so exactly one primary remains.
            var next = customer.Contacts.OrderBy(c => c.Id).First();
            next.IsPrimary = true;
            next.UpdatedAt = DateTime.UtcNow;
        }

        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CustomerActivityResponse?> AddActivityAsync(
        int customerId,
        CreateCustomerActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null) return null;
        if (!canSeeAll && customer.OwnerUserId != callerUserId) return null;

        var activity = new CustomerActivity
        {
            CustomerId = customerId,
            Type = request.Type,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            Content = request.Content.Trim(),
            CreatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.CustomerActivities.Add(activity);

        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        var creator = await db.Users.AsNoTracking()
            .Where(u => u.Id == callerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        return new CustomerActivityResponse
        {
            Id = activity.Id,
            Type = activity.Type,
            OccurredAt = activity.OccurredAt,
            Content = activity.Content,
            CreatedByUserId = activity.CreatedByUserId,
            CreatedByName = creator,
            CreatedAt = activity.CreatedAt,
        };
    }

    // ---------- helpers ----------

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateForType(CustomerType type, string? taxId, string? address, string? representative)
    {
        if (type == CustomerType.Company)
        {
            if (string.IsNullOrWhiteSpace(taxId))
                throw new CustomerOperationException("Company customers must have a TaxId (MST).");
            if (string.IsNullOrWhiteSpace(address))
                throw new CustomerOperationException("Company customers must have a registered address.");
            if (string.IsNullOrWhiteSpace(representative))
                throw new CustomerOperationException("Company customers must have a legal representative name.");
        }
    }

    private static void ValidateContactContact(string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            throw new CustomerOperationException("A customer contact must have at least one of Phone or Email.");
        }
    }

    private async Task<string> ValidateSourceCodeAsync(string sourceCode, CancellationToken ct)
    {
        var normalized = (sourceCode ?? string.Empty).Trim();
        if (normalized.Length == 0) throw new CustomerOperationException("SourceCode is required.");

        var exists = await db.MasterDataOptions.AsNoTracking().AnyAsync(
            o => o.Category == SourceCategory && o.Code == normalized && o.IsActive, ct);
        if (!exists)
        {
            throw new CustomerOperationException(
                $"SourceCode '{normalized}' is not an active option in master data '{SourceCategory}'.");
        }
        return normalized;
    }

    private async Task EnsureNoDuplicateAsync(
        CustomerType type,
        string? taxId,
        string? primaryPhone,
        int? existingCustomerId,
        string? overrideReason,
        int callerUserId,
        CancellationToken ct)
    {
        Customer? conflict = null;
        string field = string.Empty;
        string value = string.Empty;

        if (type == CustomerType.Company && !string.IsNullOrWhiteSpace(taxId))
        {
            var t = taxId.Trim();
            conflict = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TaxId == t && c.Id != (existingCustomerId ?? -1), ct);
            if (conflict != null) { field = "TaxId"; value = t; }
        }
        else if (type == CustomerType.Individual && !string.IsNullOrWhiteSpace(primaryPhone))
        {
            var p = primaryPhone.Trim();
            conflict = await db.Customers.AsNoTracking()
                .Include(c => c.Contacts)
                .Where(c => c.Type == CustomerType.Individual && c.Id != (existingCustomerId ?? -1))
                .FirstOrDefaultAsync(c => c.Contacts.Any(ct2 => ct2.IsPrimary && ct2.Phone == p), ct);
            if (conflict != null) { field = "Phone"; value = p; }
        }

        if (conflict == null) return;

        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new CustomerDuplicateException(new CustomerDuplicateResponse
            {
                Field = field,
                Value = value,
                ExistingCustomerId = conflict.Id,
                ExistingCustomerName = conflict.Name,
                Message = $"A {type} customer with the same {field} '{value}' already exists (#{conflict.Id} — {conflict.Name}). Provide DuplicateOverrideReason to save anyway.",
            });
        }

        logger.LogWarning(
            "Customer duplicate override by user {UserId}: {Field}='{Value}' matches existing customer #{ExistingId}. Reason: {Reason}",
            callerUserId, field, value, conflict.Id, overrideReason);
    }

    // ---------- mappers ----------

    private static CustomerResponse MapCustomer(
        Customer customer,
        string? ownerName,
        IEnumerable<CustomerContact>? contacts,
        IEnumerable<CustomerActivity>? activities)
    {
        return new CustomerResponse
        {
            Id = customer.Id,
            Type = customer.Type,
            Name = customer.Name,
            TaxId = customer.TaxId,
            Address = customer.Address,
            RepresentativeName = customer.RepresentativeName,
            SourceCode = customer.SourceCode,
            RelationshipStatus = customer.RelationshipStatus,
            OwnerUserId = customer.OwnerUserId,
            OwnerName = ownerName,
            Note = customer.Note,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            Contacts = contacts is null
                ? new List<CustomerContactResponse>()
                : contacts
                    .OrderByDescending(c => c.IsPrimary)
                    .ThenBy(c => c.Id)
                    .Select(MapContact)
                    .ToList(),
            Activities = activities is null
                ? new List<CustomerActivityResponse>()
                : activities
                    .OrderByDescending(a => a.OccurredAt)
                    .Select(a => new CustomerActivityResponse
                    {
                        Id = a.Id,
                        Type = a.Type,
                        OccurredAt = a.OccurredAt,
                        Content = a.Content,
                        CreatedByUserId = a.CreatedByUserId,
                        CreatedByName = a.CreatedBy?.FullName,
                        CreatedAt = a.CreatedAt,
                    })
                    .ToList(),
        };
    }

    private static CustomerContactResponse MapContact(CustomerContact contact) => new()
    {
        Id = contact.Id,
        FullName = contact.FullName,
        Position = contact.Position,
        Phone = contact.Phone,
        Email = contact.Email,
        IsPrimary = contact.IsPrimary,
        CreatedAt = contact.CreatedAt,
        UpdatedAt = contact.UpdatedAt,
    };
}
