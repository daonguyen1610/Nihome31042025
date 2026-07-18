namespace NihomeBackend.Models.DTOs.Responses;

public class BasicDesignDocResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }

    public string DisciplineCode { get; set; } = string.Empty;
    /// <summary>Localised label resolved from <c>design_discipline</c> master data.</summary>
    public string? DisciplineLabel { get; set; }

    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    /// <summary>Enum name — InProgress / SubmittedForReview / InternallyApproved / SubmittedForPermit / PermitApproved / Rejected.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BasicDesignDocListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<BasicDesignDocResponse> Items { get; set; } = new();

    /// <summary>
    /// Per-discipline approval count so the Basic-Design tab can render
    /// the "3 disciplines internally approved → unlock Shop Drawing" gate
    /// without a second round-trip.
    /// </summary>
    public BasicDesignReadiness Readiness { get; set; } = new();
}

public class BasicDesignReadiness
{
    /// <summary>Disciplines required for Shop Drawing unlock (currently arch + struct + mep).</summary>
    public List<string> RequiredDisciplineCodes { get; set; } = new();

    /// <summary>Discipline codes that currently have ≥1 InternallyApproved doc.</summary>
    public List<string> InternallyApprovedDisciplineCodes { get; set; } = new();

    /// <summary>True when every required discipline has ≥1 InternallyApproved doc.</summary>
    public bool ReadyForShopDrawing { get; set; }
}
