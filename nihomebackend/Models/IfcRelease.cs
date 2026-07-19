namespace NihomeBackend.Models;

/// <summary>
/// M2 IFC Release (Phiếu phát hành IFC — Issued For Construction) — NIH-118.
///
/// Bundles N approved <see cref="ShopDrawing"/> rows into a single
/// release packet that goes to the site team (nhà thầu chính, giám sát,
/// chủ đầu tư…). Once Released:
/// <list type="bullet">
///   <item>Every drawing in the bundle flips to
///     <see cref="ShopDrawingStatus.Released"/> (the only path there —
///     the /status endpoint refuses that transition by design).</item>
///   <item>The release header itself becomes immutable.</item>
///   <item>Any further change to a released drawing must go through a
///     new <see cref="DrawingRevision"/> from NIH-117.</item>
/// </list>
///
/// Slice 1 (NIH-118): CRUD metadata + item/recipient management + the
/// atomic Release action. Slice 2 (deferred): PDF watermark
/// ("ISSUED FOR CONSTRUCTION"), index.pdf zip export, email fanout,
/// dual-signature workflow (Design Lead + PM).
/// </summary>
public class IfcRelease
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Human-friendly unique code inside the project (IFC-YYYY-###).</summary>
    public string ReleaseNumber { get; set; } = string.Empty;

    /// <summary>Working title (free text — e.g. "Bàn giao tầng 1").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>When the packet was actually released — null while Draft.</summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Who signed the release — null while Draft.</summary>
    public int? IssuedByUserId { get; set; }
    public ApplicationUser? IssuedBy { get; set; }

    public IfcReleaseStatus Status { get; set; } = IfcReleaseStatus.Draft;

    /// <summary>Free-text working note.</summary>
    public string? Note { get; set; }

    public List<IfcReleaseItem> Items { get; set; } = new();
    public List<IfcReleaseRecipient> Recipients { get; set; } = new();

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Lifecycle of an <see cref="IfcRelease"/>.</summary>
public enum IfcReleaseStatus
{
    /// <summary>Being assembled — items and recipients editable.</summary>
    Draft = 0,
    /// <summary>Released to the site — immutable. Its drawings are now Released.</summary>
    Released = 1,
    /// <summary>Aborted before release — items untouched, header locked.</summary>
    Cancelled = 2,
}

/// <summary>A single Shop Drawing bundled into a release.</summary>
public class IfcReleaseItem
{
    public int Id { get; set; }
    public int IfcReleaseId { get; set; }
    public IfcRelease IfcRelease { get; set; } = null!;

    public int ShopDrawingId { get; set; }
    public ShopDrawing ShopDrawing { get; set; } = null!;
}

/// <summary>
/// One recipient on the release distribution list (main contractor,
/// site supervisor, client…) with per-row acknowledgement tracking.
/// </summary>
public class IfcReleaseRecipient
{
    public int Id { get; set; }
    public int IfcReleaseId { get; set; }
    public IfcRelease IfcRelease { get; set; } = null!;

    /// <summary>Free-text recipient name (org or individual).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Master-data code from <c>ifc_recipient_type</c>.</summary>
    public string RecipientTypeCode { get; set; } = string.Empty;

    /// <summary>Timestamp of ack — null while unacknowledged.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Free-text notes about the ack (email id, biên bản filename…).</summary>
    public string? AcknowledgementNote { get; set; }
}
