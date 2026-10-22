namespace NihomeBackend.Models.DTOs.Responses;

public sealed class LegalActivationPreviewResponse
{
    public int ContractId { get; set; }
    public int OperationalProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int ProjectManagerUserId { get; set; }
    public bool Eligible { get; set; }
    public bool AlreadyActivated { get; set; }
    public int? ExistingWorkPackageId { get; set; }
    public string? WorkPackageRowVersion { get; set; }
    public int? ExistingLegalLeadUserId { get; set; }
    public DateTime? ExistingPlannedStart { get; set; }
    public DateTime? ExistingPlannedEnd { get; set; }
    public LegalChecklistTemplateResponse? AppliedTemplate { get; set; }
    public DateTime? TemplateAppliedAt { get; set; }
    public int? TemplateAppliedByUserId { get; set; }
    public List<LegalChecklistTemplateResponse> Templates { get; set; } = new();
    public string? ReasonCode { get; set; }
    public string? Message { get; set; }
    public List<LegalPermitTypeOptionResponse> PermitTypes { get; set; } = new();
    public List<LegalProjectMemberOptionResponse> ProjectMembers { get; set; } = new();
}

public sealed class LegalPermitTypeOptionResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool AlreadyExists { get; set; }
    public bool RequiresSetup { get; set; }
}

public sealed class LegalProjectMemberOptionResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleGroupId { get; set; }
    public string RoleGroupCode { get; set; } = string.Empty;
    public bool CanLeadLegal { get; set; }
}

public sealed class LegalActivationResultResponse
{
    public ContractResponse Contract { get; set; } = new();
    public int WorkPackageId { get; set; }
    public List<int> PermitIds { get; set; } = new();
    public bool ReusedExistingPackage { get; set; }
}
