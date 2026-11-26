namespace NihomeBackend.Models;

public enum DriveTreeMigrationStatus { Running, Completed, Failed, RolledBack }

public enum DriveTreeMigrationItemKind { Binding, Document, UntrackedFile }

public class DriveTreeMigration
{
    public int Id { get; set; }
    public DriveTreeMigrationStatus Status { get; set; } = DriveTreeMigrationStatus.Running;
    public string OldFoldersJson { get; set; } = string.Empty;
    public string NewFoldersJson { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int StartedByUserId { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Error { get; set; }
    /// <summary>Set before the first undo move; a half-undone migration must not be resumed forward.</summary>
    public DateTime? UndoStartedAt { get; set; }
    public List<DriveTreeMigrationItem> Items { get; set; } = [];
}

public class DriveTreeMigrationItem
{
    public long Id { get; set; }
    public int MigrationId { get; set; }
    public DriveTreeMigration Migration { get; set; } = null!;
    public int OperationalProjectId { get; set; }
    public ProjectDocumentCategory Category { get; set; }
    public DriveTreeMigrationItemKind Kind { get; set; }
    public long? ProjectDocumentId { get; set; }
    public string? DriveFileId { get; set; }
    public string FromFolderId { get; set; } = string.Empty;
    public string? ToFolderId { get; set; }
    public DateTime? MovedAt { get; set; }
    public string? SkipReason { get; set; }
    public DateTime? RevertedAt { get; set; }
}
