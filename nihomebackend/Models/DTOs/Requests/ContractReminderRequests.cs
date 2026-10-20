using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ContractReminderPreviewRequest
{
    [Required, RegularExpression("^(Renewal|End)$")]
    public string Kind { get; set; } = "";
    [Required]
    public DateOnly? ReminderDate { get; set; }
    [Range(1, int.MaxValue)]
    public int? ContactId { get; set; }
}

public sealed class ScheduleContractReminderRequest
{
    [Required, StringLength(16000, MinimumLength = 1)]
    public string PreviewToken { get; set; } = "";
}

public sealed class ContractReminderTemplatesRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string RenewalSubject { get; set; } = "";
    [Required, StringLength(8000, MinimumLength = 1)]
    public string RenewalBody { get; set; } = "";
    [Required, StringLength(200, MinimumLength = 1)]
    public string EndSubject { get; set; } = "";
    [Required, StringLength(8000, MinimumLength = 1)]
    public string EndBody { get; set; } = "";
}
