namespace NihomeBackend.Models;

/// <summary>
/// M2 Design Project (Dự án thiết kế) — the umbrella record for a design
/// engagement covering the three canonical stages Concept → Basic Design →
/// Shop Drawing. Auto-created from a <see cref="Contract"/> when it moves
/// to <see cref="ContractStatus.InProgress"/> (NIH-113 AC #1) but can also
/// be created by hand for internal / non-contract work.
///
/// This slice ships the overview record + CRUD; per-stage documents,
/// team roster, revisions and IFC releases land in NIH-114..118.
/// </summary>
public class DesignProject
{
    public int Id { get; set; }

    /// <summary>Human-friendly project code (e.g. DP-2026-0001) — unique.</summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>Project name — free text, usually mirrors the contract subject.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Owning customer — hard FK. Every design project belongs to a customer.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Source contract (nullable — internal projects may not have one).</summary>
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    /// <summary>Project Manager assignment (nullable while unassigned).</summary>
    public int? ProjectManagerUserId { get; set; }
    public ApplicationUser? ProjectManager { get; set; }

    /// <summary>Design Lead assignment (nullable while unassigned).</summary>
    public int? DesignLeadUserId { get; set; }
    public ApplicationUser? DesignLead { get; set; }

    /// <summary>Kick-off date. Nullable so an auto-created row can be tightened later.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Overall deadline for the design engagement.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Aggregate stage — see <see cref="DesignProjectStage"/>.</summary>
    public DesignProjectStage CurrentStage { get; set; } = DesignProjectStage.Concept;

    /// <summary>Overall status — see <see cref="DesignProjectStatus"/>.</summary>
    public DesignProjectStatus Status { get; set; } = DesignProjectStatus.Active;

    /// <summary>Free-text working note.</summary>
    public string? Note { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// The three canonical design stages, in order. NIH-113 stores the value
/// but does not yet enforce transitions — those land alongside the
/// per-stage tabs (NIH-114 Concept → Basic, NIH-115 Basic → Shop Drawing).
/// </summary>
public enum DesignProjectStage
{
    Concept = 0,
    BasicDesign = 1,
    ShopDrawing = 2,
    Completed = 3,
}

/// <summary>
/// Overall lifecycle of a <see cref="DesignProject"/>. Distinct from
/// <see cref="DesignProjectStage"/>: a project can be paused or cancelled
/// mid-stage. Only Active projects surface on the default filtered list.
/// </summary>
public enum DesignProjectStatus
{
    Active = 0,
    OnHold = 1,
    Completed = 2,
    Cancelled = 3,
}
