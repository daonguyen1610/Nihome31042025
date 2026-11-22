using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Services;

public class CustomerService(
    AppDbContext db,
    ILogger<CustomerService> logger,
    ICrmHardDeletePlanService hardDeletePlans,
    IHardDeleteOperationService hardDeleteOperations) : ICustomerService
{
    private const int MaxPageSize = 100;
    private const string SourceCategory = "customer_source";
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ContactMutationGates = new();

    public async Task<CustomerListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        CustomerType? type = null,
        CustomerRelationshipStatus? status = null,
        int? ownerUserId = null,
        string? sourceCode = null,
        string? businessSectorCode = null,
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

        if (!string.IsNullOrWhiteSpace(businessSectorCode))
        {
            var sector = businessSectorCode.Trim();
            query = query.Where(c => c.BusinessSectorCode == sector);
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
                LegalRepresentativeContactId = c.Contacts
                    .Where(contact => contact.IsLegalRepresentative)
                    .Select(contact => (int?)contact.Id)
                    .SingleOrDefault(),
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
                mapped.LegalRepresentativeContactId = r.LegalRepresentativeContactId;
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
            throw new CustomerOperationException("Bạn chưa có quyền tạo Khách hàng. Hãy liên hệ quản trị viên nếu cần quyền này.");
        }

        ValidateContactContact(request.PrimaryContact.Phone, request.PrimaryContact.Email);
        await EnsureBirthYearAsync(request.Type, request.RepresentativeBirthYear, ct);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);
        var businessSectorCode = await ValidateBusinessSectorAsync(request.BusinessSectorCode, null, ct);

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
        // Tax id, address and representative may be filled in later; the contact person is the
        // legal representative only when one was named (contracts check this before signing).
        var representativeName = request.Type == CustomerType.Company &&
            !string.IsNullOrWhiteSpace(request.RepresentativeName)
                ? request.RepresentativeName.Trim()
                : null;
        var contactIsRepresentative = representativeName is not null && string.Equals(
            request.PrimaryContact.FullName.Trim(), representativeName, StringComparison.OrdinalIgnoreCase);
        var customer = new Customer
        {
            Type = request.Type,
            Name = request.Name.Trim(),
            TaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            RepresentativeName = representativeName,
            RepresentativeBirthYear = request.Type == CustomerType.Company ? request.RepresentativeBirthYear : null,
            RepresentativeAddress = request.Type == CustomerType.Company ? TrimOrNull(request.RepresentativeAddress) : null,
            EnglishName = request.Type == CustomerType.Company ? TrimOrNull(request.EnglishName) : null,
            SourceCode = sourceCode,
            BusinessSectorCode = businessSectorCode,
            BusinessDetail = string.IsNullOrWhiteSpace(request.BusinessDetail) ? null : request.BusinessDetail.Trim(),
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
                    IsLegalRepresentative = contactIsRepresentative,
                    LegalRepresentativeSince = contactIsRepresentative ? now : null,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            },
        };

        if (representativeName is not null)
        {
            if (!contactIsRepresentative)
            {
                customer.Contacts.Add(new CustomerContact
                {
                    FullName = representativeName,
                    IsLegalRepresentative = true,
                    LegalRepresentativeSince = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            customer.Activities.Add(LegalRepresentativeActivity(representativeName, callerUserId, now, "assigned"));
        }

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
        if (!canManage) throw new CustomerOperationException("Bạn chưa có quyền sửa Khách hàng. Hãy liên hệ quản trị viên nếu cần quyền này.");

        CrmConcurrency.Apply(db, customer, request.RowVersion);

        // Suspended is a dead-end status — Sales cannot suspend, only manager/BOD.
        if (request.RelationshipStatus == CustomerRelationshipStatus.Suspended &&
            customer.RelationshipStatus != CustomerRelationshipStatus.Suspended &&
            !canSeeAll)
        {
            throw new CustomerOperationException("Chỉ Trưởng phòng Kinh doanh hoặc Ban Giám đốc được tạm ngừng Khách hàng.");
        }

        // Sales users cannot re-assign the customer to somebody else.
        if (!canSeeAll && request.OwnerUserId != customer.OwnerUserId && request.OwnerUserId != callerUserId)
        {
            throw new CustomerOperationException("Chỉ Trưởng phòng Kinh doanh được giao lại Khách hàng cho người khác.");
        }

        await EnsureBirthYearAsync(request.Type, request.RepresentativeBirthYear, ct);
        var sourceCode = await ValidateSourceCodeAsync(request.SourceCode, ct);
        var businessSectorCode = await ValidateBusinessSectorAsync(
            request.BusinessSectorCode, customer.BusinessSectorCode, ct);

        var primaryPhone = customer.Contacts.FirstOrDefault(c => c.IsPrimary)?.Phone;
        await EnsureNoDuplicateAsync(
            request.Type,
            request.TaxId,
            primaryPhone,
            existingCustomerId: customer.Id,
            request.DuplicateOverrideReason,
            callerUserId,
            ct);

        var previousRepresentative = customer.Contacts.SingleOrDefault(contact => contact.IsLegalRepresentative);
        var previousRepresentativeName = previousRepresentative?.FullName;
        customer.Type = request.Type;
        customer.Name = request.Name.Trim();
        customer.TaxId = TrimOrNull(request.TaxId);
        customer.Address = TrimOrNull(request.Address);
        if (request.Type == CustomerType.Company && !string.IsNullOrWhiteSpace(request.RepresentativeName))
        {
            var requestedName = request.RepresentativeName.Trim();
            // A representative named for the first time is a new person unless they are already
            // a contact; never rename the existing contact into them.
            var representative = previousRepresentative
                ?? customer.Contacts.FirstOrDefault(contact =>
                    string.Equals(contact.FullName, requestedName, StringComparison.OrdinalIgnoreCase));
            if (representative is null)
            {
                representative = new CustomerContact { CreatedAt = DateTime.UtcNow };
                customer.Contacts.Add(representative);
            }
            foreach (var contact in customer.Contacts)
            {
                contact.IsLegalRepresentative = contact == representative;
                contact.LegalRepresentativeSince = contact == representative
                    ? contact.LegalRepresentativeSince ?? DateTime.UtcNow
                    : null;
            }
            representative.FullName = requestedName;
            representative.UpdatedAt = DateTime.UtcNow;
            customer.RepresentativeName = representative.FullName;
            if (previousRepresentative?.Id != representative.Id ||
                !string.Equals(previousRepresentativeName, representative.FullName, StringComparison.Ordinal))
            {
                db.CustomerActivities.Add(LegalRepresentativeActivity(
                    representative.FullName, callerUserId, DateTime.UtcNow, "assigned", customer.Id));
            }
        }
        else
        {
            foreach (var contact in customer.Contacts)
            {
                contact.IsLegalRepresentative = false;
                contact.LegalRepresentativeSince = null;
            }
            customer.RepresentativeName = null;
            if (previousRepresentative is not null)
            {
                db.CustomerActivities.Add(LegalRepresentativeActivity(
                    previousRepresentative.FullName, callerUserId, DateTime.UtcNow, "cleared", customer.Id));
            }
        }
        var isCompany = request.Type == CustomerType.Company;
        customer.RepresentativeBirthYear = isCompany ? request.RepresentativeBirthYear : null;
        customer.RepresentativeAddress = isCompany ? TrimOrNull(request.RepresentativeAddress) : null;
        customer.EnglishName = isCompany ? TrimOrNull(request.EnglishName) : null;
        customer.SourceCode = sourceCode;
        customer.BusinessSectorCode = businessSectorCode;
        customer.BusinessDetail = TrimOrNull(request.BusinessDetail);
        customer.RelationshipStatus = request.RelationshipStatus;
        customer.OwnerUserId = request.OwnerUserId;
        customer.Note = TrimOrNull(request.Note);
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedByUserId = callerUserId;

        await CrmConcurrency.SaveChangesAsync(db, ct);

        var ownerName = customer.OwnerUserId.HasValue
            ? await db.Users.AsNoTracking().Where(u => u.Id == customer.OwnerUserId.Value)
                .Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return MapCustomer(customer, ownerName, customer.Contacts, activities: null);
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(
        int id,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) throw new CustomerOperationException("Không có quyền xoá khách hàng.");
        var visible = await db.Customers.AsNoTracking()
            .AnyAsync(item => item.Id == id && (canSeeAll || item.OwnerUserId == callerUserId), ct);
        return visible ? (await hardDeletePlans.ForCustomerAsync(id, ct))?.Impact : null;
    }

    public async Task<HardDeleteOperationResult?> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) throw new CustomerOperationException("Không có quyền xoá khách hàng.");
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && (canSeeAll || item.OwnerUserId == callerUserId), ct);
        if (customer is null) return null;
        var plan = await hardDeletePlans.ForCustomerAsync(id, ct);
        if (plan is null) return null;
        if (!plan.Impact.CanDelete)
            throw new CustomerOperationException(
                "Không thể xoá khách hàng khi còn cơ hội, gói thầu, hợp đồng hoặc dự án liên quan.");
        if (!string.Equals(request.PlanToken?.Trim(), plan.Impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException(
                "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!string.Equals(request.Confirmation, plan.Impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new CustomerOperationException(
                $"Mã xác nhận không đúng. Vui lòng nhập chính xác '{plan.Impact.RequiredConfirmation}'.");
        CrmConcurrency.EnsureMatches(customer.RowVersion, request.RowVersion);

        var operation = await hardDeleteOperations.CreateAsync(new CreateHardDeleteOperationRequest(
            EntityTypes.Customer,
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.Impact.ResourceLabel,
            plan.Impact.PlanToken,
            request.Confirmation,
            callerUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.Items), ct);
        var result = await hardDeleteOperations.ProcessAsync(operation.OperationId, ct);
        logger.LogInformation("Customer {Id} durable hard-delete is {Status}", id, result.Status);
        return result;
    }

    public async Task<CustomerContactResponse?> UpsertContactAsync(
        int customerId,
        UpsertCustomerContactRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var gate = ContactMutationGates.GetOrAdd(customerId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await UpsertContactCoreAsync(
                customerId, request, callerUserId, canManage, canSeeAll, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CustomerContactResponse?> UpsertContactCoreAsync(
        int customerId,
        UpsertCustomerContactRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AcquireContactMutationLockAsync(customerId, transaction is not null, ct);

        var customer = await db.Customers
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return null;
        if (!canSeeAll && customer.OwnerUserId != callerUserId) return null;
        if (!canManage) throw new CustomerOperationException("Bạn chưa có quyền sửa người liên hệ của Khách hàng. Hãy liên hệ quản trị viên nếu cần quyền này.");

        ValidateContactContact(request.Phone, request.Email);

        var now = DateTime.UtcNow;
        var previousRepresentative = customer.Contacts.SingleOrDefault(item => item.IsLegalRepresentative);
        var previousRepresentativeName = previousRepresentative?.FullName;
        CustomerContact contact;
        if (request.Id is int id)
        {
            contact = customer.Contacts.FirstOrDefault(c => c.Id == id)
                ?? throw new CustomerOperationException("Không tìm thấy người liên hệ này trong Khách hàng. Hãy tải lại trang để xem danh sách mới nhất.");
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

        if (customer.Type == CustomerType.Company && request.IsLegalRepresentative)
        {
            var representativeChanged = previousRepresentative is not null && previousRepresentative != contact;
            foreach (var other in customer.Contacts.Where(item => item != contact))
            {
                other.IsLegalRepresentative = false;
                other.LegalRepresentativeSince = null;
            }
            if (representativeChanged)
            {
                await CrmConcurrency.SaveChangesAsync(db, ct);
            }
            contact.IsLegalRepresentative = true;
            contact.LegalRepresentativeSince ??= now;
        }
        else if (customer.Type == CustomerType.Company && previousRepresentative == contact)
        {
            contact.IsLegalRepresentative = true;
        }
        else if (customer.Type == CustomerType.Individual)
        {
            contact.IsLegalRepresentative = false;
            contact.LegalRepresentativeSince = null;
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
        var currentRepresentative = customer.Contacts.SingleOrDefault(item => item.IsLegalRepresentative);
        if (customer.Type == CustomerType.Company && currentRepresentative is null)
        {
            throw new CustomerOperationException(
                "Company customers must have exactly one legal representative contact.");
        }
        customer.RepresentativeName = currentRepresentative?.FullName;
        if (previousRepresentative?.Id != currentRepresentative?.Id ||
            !string.Equals(previousRepresentativeName, currentRepresentative?.FullName, StringComparison.Ordinal))
        {
            db.CustomerActivities.Add(LegalRepresentativeActivity(
                currentRepresentative?.FullName ?? previousRepresentativeName ?? string.Empty,
                callerUserId,
                now,
                currentRepresentative is null ? "cleared" : "assigned",
                customer.Id));
        }

        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapContact(contact);
    }

    private async Task AcquireContactMutationLockAsync(
        int customerId,
        bool hasTransaction,
        CancellationToken ct)
    {
        if (!hasTransaction || !db.Database.IsSqlServer()) return;
        var resource = $"customer-contacts:{customerId}";
        var result = new SqlParameter("@lockResult", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output,
        };
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            EXEC {result} = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            """, ct);
        if ((int)result.Value < 0)
            throw new CrmConcurrencyException(
                "Người khác đang cập nhật danh sách người liên hệ. Hãy tải lại trang rồi thử lại.");
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
        if (!canManage) throw new CustomerOperationException("Bạn chưa có quyền sửa người liên hệ của Khách hàng. Hãy liên hệ quản trị viên nếu cần quyền này.");

        var contact = customer.Contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact is null) return false;

        if (customer.Contacts.Count == 1)
        {
            throw new CustomerOperationException("A customer must have at least one contact — delete the customer instead.");
        }
        if (customer.Type == CustomerType.Company && contact.IsLegalRepresentative)
        {
            throw new CustomerOperationException(
                "Assign another legal representative before deleting this contact.");
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
        var today = await DisplaySettings.TodayAsync(db, ct);
        if (request.FollowUpDate < today)
            throw new CustomerOperationException("Ngày nhắc gọi lại/thăm phải từ hôm nay trở đi.");
        // The care date is when the call or visit happened (NICON feedback row 23): never later
        // than today or now, and not before EarliestCareDate (a half-typed year such as 0026).
        if (request.OccurredOn > today)
            throw new CustomerOperationException("Ngày chăm sóc không được sau hôm nay.");
        if (request.OccurredOn < EarliestCareDate)
            throw new CustomerOperationException("Ngày chăm sóc phải từ 01/01/2000 trở đi.");
        // A timestamp without an offset is UTC, whatever the server's own time zone is.
        var occurredAt = request.OccurredAt is { } at
            ? at.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : at.ToUniversalTime()
            : (DateTime?)null;
        if (request.OccurredOn is null && occurredAt > DateTime.UtcNow.AddMinutes(1))
            throw new CustomerOperationException("Ngày chăm sóc không được sau hôm nay.");
        // Today means now. An earlier day is stored at its midday in the display time zone so it
        // sorts within that day; shortcut: changing the display zone by 12h+ can move such entries
        // a day, store the DateOnly itself if NICON ever switches zones.
        var dateOnly = request.OccurredOn is { } day && day < today;
        if (dateOnly)
            occurredAt = TimeZoneInfo.ConvertTimeToUtc(
                request.OccurredOn!.Value.ToDateTime(new TimeOnly(12, 0)), await DisplaySettings.ZoneAsync(db, ct));
        else if (request.OccurredOn is not null)
            occurredAt = DateTime.UtcNow;

        var activity = new CustomerActivity
        {
            CustomerId = customerId,
            Type = request.Type,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            IsDateOnly = dateOnly,
            Content = request.Content.Trim(),
            FollowUpDate = request.FollowUpDate,
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

        return MapActivity(activity, creator);
    }

    private static CustomerActivityResponse MapActivity(CustomerActivity activity, string? creatorName) => new()
    {
        Id = activity.Id,
        Type = activity.Type,
        OccurredAt = activity.OccurredAt,
        IsDateOnly = activity.IsDateOnly,
        Content = activity.Content,
        CreatedByUserId = activity.CreatedByUserId,
        CreatedByName = creatorName,
        CreatedAt = activity.CreatedAt,
        FollowUpDate = activity.FollowUpDate,
    };

    // ---------- helpers ----------

    private const int EarliestBirthYear = 1900;
    private static readonly DateOnly EarliestCareDate = new(2000, 1, 1);

    /// <summary>A company representative's birth year is from EarliestBirthYear to this year (display time zone).</summary>
    private async Task EnsureBirthYearAsync(CustomerType type, int? year, CancellationToken ct)
    {
        if (type != CustomerType.Company || year is null) return;
        if (year < EarliestBirthYear || year > (await DisplaySettings.TodayAsync(db, ct)).Year)
            throw new CustomerOperationException(
                "Năm sinh người đại diện phải là năm từ 1900 đến năm nay, ví dụ 1975.");
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerActivity LegalRepresentativeActivity(
        string representativeName,
        int callerUserId,
        DateTime occurredAt,
        string action,
        int customerId = 0) => new()
        {
            CustomerId = customerId,
            Type = action == "cleared"
                ? CustomerActivityType.LegalRepresentativeCleared
                : CustomerActivityType.LegalRepresentativeAssigned,
            OccurredAt = occurredAt,
            Content = representativeName,
            CreatedByUserId = callerUserId,
            CreatedAt = occurredAt,
        };

    private static void ValidateContactContact(string? phone, string? email)
    {
        // Shape as well as presence — see ContactValidation for why the DTO
        // attributes were not enough on their own.
        var error = ContactValidation.Validate(phone, email);
        if (error is not null)
        {
            throw new CustomerOperationException(error);
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

    private async Task<string?> ValidateBusinessSectorAsync(string? code, string? current, CancellationToken ct)
    {
        var (allowed, normalized) = await BusinessSectors.CheckAsync(db, code, current, ct);
        if (!allowed) throw new CustomerOperationException(BusinessSectors.NotInListMessage);
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
            RepresentativeBirthYear = customer.RepresentativeBirthYear,
            RepresentativeAddress = customer.RepresentativeAddress,
            EnglishName = customer.EnglishName,
            LegalRepresentativeContactId = contacts?
                .SingleOrDefault(contact => contact.IsLegalRepresentative)?.Id,
            SourceCode = customer.SourceCode,
            BusinessSectorCode = customer.BusinessSectorCode,
            BusinessDetail = customer.BusinessDetail,
            RelationshipStatus = customer.RelationshipStatus,
            OwnerUserId = customer.OwnerUserId,
            OwnerName = ownerName,
            Note = customer.Note,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            RowVersion = CrmConcurrency.Encode(customer.RowVersion),
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
                    .Select(a => MapActivity(a, a.CreatedBy?.FullName))
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
        IsLegalRepresentative = contact.IsLegalRepresentative,
        LegalRepresentativeSince = contact.LegalRepresentativeSince,
        CreatedAt = contact.CreatedAt,
        UpdatedAt = contact.UpdatedAt,
    };
}
