using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class SetConstructionTaskDrawingsRequest
{
    [Required]
    public Guid? Version { get; set; }

    [Required, MaxLength(200)]
    public List<int>? IfcReleaseItemIds { get; set; }
}
