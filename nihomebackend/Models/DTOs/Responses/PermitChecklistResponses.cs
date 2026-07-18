namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Detail shape for a single permit checklist row.</summary>
public class PermitChecklistItemResponse
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string? DesignProjectName { get; set; }

    public string PermitTypeCode { get; set; } = string.Empty;
    /// <summary>Localised label resolved from master data (VD "Giấy phép xây dựng (GPXD)").</summary>
    public string? PermitTypeLabel { get; set; }

    public string? IssuingAgency { get; set; }

    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public DateTime? TargetDeadline { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public string? SubmittedFilePath { get; set; }
    public string? IssuedFilePath { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    // ---- Derived risk hints (server-computed so the FE doesn't have to) ----

    /// <summary>True when <c>TargetDeadline &lt; now</c> and status is not Issued.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>True when <c>TargetDeadline</c> is within 7 days and status is not Issued.</summary>
    public bool IsDueSoon { get; set; }

    /// <summary>True when the permit is Issued and <c>ExpiresAt</c> is within 30 days.</summary>
    public bool IsExpiringSoon { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PermitChecklistListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<PermitChecklistItemResponse> Items { get; set; } = new();

    // Convenience aggregate for the BGD risk-overview card. Server computes
    // these across the *unfiltered* base so the FE always knows the true
    // company-wide risk regardless of what filter is currently active.
    public PermitChecklistRiskSummary Risk { get; set; } = new();
}

public class PermitChecklistRiskSummary
{
    public int Overdue { get; set; }
    public int DueSoon { get; set; }
    public int ExpiringSoon { get; set; }
    public int TotalOpen { get; set; }
}
