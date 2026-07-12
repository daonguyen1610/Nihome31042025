using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload for POST /api/leads/{id}/convert.
/// While Customer and Opportunity entities are not yet implemented the ids
/// are optional — the endpoint still transitions the lead to
/// <see cref="LeadStatus.Converted"/> so downstream reporting works.
/// </summary>
public class ConvertLeadRequest
{
    /// <summary>Id of an already-existing customer to link the lead to.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Id of an already-created opportunity spawned from this lead.</summary>
    public int? OpportunityId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
