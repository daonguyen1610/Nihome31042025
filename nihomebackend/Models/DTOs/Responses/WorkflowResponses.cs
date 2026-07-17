namespace NihomeBackend.Models.DTOs.Responses;

public class WorkflowStepResponse
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApproverRoleCode { get; set; } = string.Empty;
    public int SlaHours { get; set; }
    public bool RequireAllApprovers { get; set; }
    public string? ConditionExpression { get; set; }
}

public class WorkflowConfigResponse
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public List<WorkflowStepResponse> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
