namespace NihomeBackend.Models;

public class ProjectDocument
{
    public long Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public ProjectDocumentCategory Category { get; set; } = ProjectDocumentCategory.Unclassified;
    public ProjectDocumentSourceModule SourceModule { get; set; } = ProjectDocumentSourceModule.General;
    public ProjectDocumentSourceType SourceType { get; set; } = ProjectDocumentSourceType.ManualUpload;
    public string? SourceEntityType { get; set; }
    public string? SourceSlot { get; set; }
    public long? SourceRecordId { get; set; }
    public int? CustomerId { get; set; }
    public int? ContractId { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public ProjectDocumentOrigin Origin { get; set; } = ProjectDocumentOrigin.Nicon;
    public long Generation { get; set; } = 1;
    public ProjectDocumentDesiredOperation DesiredOperation { get; set; } = ProjectDocumentDesiredOperation.Upsert;
    public ProjectDocumentSyncStatus SyncStatus { get; set; } = ProjectDocumentSyncStatus.Pending;
    public int SyncAttemptCount { get; set; }
    public string? SyncError { get; set; }
    public DateTime? NextSyncAttemptAt { get; set; }
    public DateTime? LastSyncAttemptAt { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public string? DriveFileId { get; set; }
    public string? DriveFolderId { get; set; }
    public string? DriveWebViewLink { get; set; }
    public string? DriveVersion { get; set; }
    public DateTime? DriveModifiedAt { get; set; }
    public bool IsDownloadable { get; set; } = true;
    public string? UnsupportedReason { get; set; }
    public string? ConflictObservedDriveFileId { get; set; }
    public string? ConflictObservedDriveVersion { get; set; }
    public ProjectDocumentConflictState ConflictState { get; set; } = ProjectDocumentConflictState.None;
    public long? ConflictWithDocumentId { get; set; }
    public ProjectDocument? ConflictWithDocument { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class ProjectDriveFolder
{
    public long Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public ProjectDocumentCategory Category { get; set; }
    public string DriveFolderId { get; set; } = string.Empty;
    public string? DriveWebViewLink { get; set; }
    public DateTime? LastReconciledAt { get; set; }
    public Guid? ReconciliationClaimToken { get; set; }
    public DateTime? ReconciliationClaimExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum ProjectDocumentCategory
{
    Unclassified = 0,
    CrmPreDesign = 1,
    DesignConcept = 2,
    DesignBasic = 3,
    DesignShopDrawing = 4,
    LegalPermits = 5,
    ConstructionAcceptance = 6,
    Procurement = 7,
    FinanceContracts = 8,
}

public enum ProjectDocumentSourceModule
{
    General = 0,
    Crm = 1,
    Survey = 2,
    Design = 3,
    Construction = 4,
    Acceptance = 5,
    Handover = 6,
}

public enum ProjectDocumentSourceType
{
    ManualUpload = 0,
    ExistingManagedFile = 1,
    GoogleDriveImport = 2,
}

public enum ProjectDocumentOrigin { Nicon = 0, GoogleDrive = 1 }
public enum ProjectDocumentDesiredOperation { None = 0, Upsert = 1, Delete = 2 }
public enum ProjectDocumentSyncStatus { Pending = 0, Processing = 1, Synced = 2, Failed = 3, Deleted = 4, Conflict = 5 }
public enum ProjectDocumentConflictState { None = 0, PendingConfirmation = 1 }
