using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class CreateOperationalProjectAssignmentCommentRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}
