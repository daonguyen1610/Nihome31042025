using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload for POST /api/leads/{id}/convert.
///
/// <see cref="CustomerId"/> and <see cref="OpportunityId"/> mean "link to this
/// existing record"; leaving them empty means "create a new one".
///
/// The address and representative fields are required only when the lead carries a
/// CompanyName and <see cref="CustomerId"/> is empty, because
/// <c>CustomerService.ValidateForType</c> demands an address and legal
/// representative for Company customers and the Lead model holds neither.
/// Tax id is optional because it may not yet have been supplied (NIH-448).
/// </summary>
public class ConvertLeadRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    /// <summary>Id of an already-existing customer to link the lead to.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Id of an already-created opportunity spawned from this lead.</summary>
    public int? OpportunityId { get; set; }

    [StringLength(50)]
    public string? TaxId { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? RepresentativeName { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
