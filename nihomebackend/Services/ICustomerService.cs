using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Owns the full lifecycle of CRM Customers, including their contact
/// persons and follow-up timeline. Enforces owner-scoped access (Sales see
/// only their own; Sales Manager / Accountant / BOD / Admin see all via
/// the <c>crm.customers.view.all</c> permission), duplicate detection with
/// override + audit, and delete-guard against downstream FKs.
///
/// Downstream FK check is a stub for this slice (Opportunity / Contract
/// entities land in later stories NIH-83 / NIH-87). Once those exist the
/// service will refuse deletion when any open Opportunity or non-Closed
/// Contract references the customer.
/// </summary>
public interface ICustomerService
{
    Task<CustomerListResponse> ListAsync(
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
        CancellationToken ct = default);

    Task<CustomerResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    /// <summary>
    /// Creates a customer plus its mandatory primary contact. Returns the
    /// created customer on success, throws <see cref="CustomerDuplicateException"/>
    /// when a matching TaxId (Company) or primary Phone (Individual) exists
    /// and no <c>DuplicateOverrideReason</c> was supplied on the request.
    /// </summary>
    Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default);

    Task<CustomerResponse?> UpdateAsync(
        int id,
        UpdateCustomerRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);

    // Contacts

    Task<CustomerContactResponse?> UpsertContactAsync(
        int customerId,
        UpsertCustomerContactRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteContactAsync(
        int customerId,
        int contactId,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    // Activities

    Task<CustomerActivityResponse?> AddActivityAsync(
        int customerId,
        CreateCustomerActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);
}

/// <summary>Thrown when a business rule is violated (state transition, delete guard…).</summary>
public sealed class CustomerOperationException(string message) : InvalidOperationException(message);

/// <summary>
/// Thrown when a duplicate customer is detected and the caller has not
/// supplied a <c>DuplicateOverrideReason</c>. Controllers translate this
/// to HTTP 409 with a <see cref="CustomerDuplicateResponse"/> payload so
/// the FE can prompt for a justification before retrying.
/// </summary>
public sealed class CustomerDuplicateException(CustomerDuplicateResponse detail)
    : InvalidOperationException(detail.Message)
{
    public CustomerDuplicateResponse Detail { get; } = detail;
}
